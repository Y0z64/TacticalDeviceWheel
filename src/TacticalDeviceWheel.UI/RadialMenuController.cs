using FirearmController = EFT.Player.FirearmController;
using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using TacticalDeviceWheel.Core;
using TacticalDeviceWheel.Devices;
using UnityEngine;

namespace TacticalDeviceWheel.UI;

internal sealed class RadialMenuController
{
	private readonly Plugin plugin;

	private readonly WeaponDeviceScanner scanner;

	private readonly DeviceController devices;

	private readonly SelectionMemory selection = new SelectionMemory();

	private List<TacticalCapability> entries = new List<TacticalCapability>();

	private readonly List<RangeReadout> readouts = new List<RangeReadout>();

	private Vector2 vector;

	private FirearmController firearm;

	public bool IsOpen { get; private set; }

	public bool HasSelection
	{
		get
		{
			if (selection.LastValidSelection >= 0)
			{
				return selection.LastValidSelection < entries.Count;
			}
			return false;
		}
	}

	public RadialMenuController(Plugin plugin)
	{
		this.plugin = plugin;
		scanner = new WeaponDeviceScanner(plugin);
		devices = new DeviceController(plugin);
	}

	public void Open(Player player, FirearmController controller)
	{
		Close();
		firearm = controller;
		entries = scanner.Scan(controller);
		vector = Vector2.zero;
		selection.Reset();
		foreach (TacticalCapability entry in entries)
		{
			if (entry.Type == "Rangefinder" && entry.Device.RangeReadout != null)
			{
				readouts.Add(entry.Device.RangeReadout);
			}
		}
		PollReadouts();
		plugin.UI.Show(entries, DisplayName.ParseDisplayName(EFT.LocalizationExtensions.Localized(((Item)controller.Item).ShortName, (string)null)));
		IsOpen = true;
	}

	public void Move(float x, float y)
	{
		if (IsOpen)
		{
			PollReadouts();
			vector = Vector2.ClampMagnitude(vector + new Vector2(x, y) * plugin.Options.Sensitivity.Value, 1f);
			int selected = selection.Update(vector.x, vector.y, entries.Count, plugin.Options.DeadZone.Value);
			plugin.UI.Refresh(selected, vector);
		}
	}

	public bool Commit(Player player, FirearmController controller)
	{
		if (!IsOpen || firearm != controller || !HasSelection)
		{
			return false;
		}
		bool result = devices.Toggle(player, controller, entries[selection.LastValidSelection]);
		PollReadouts();
		plugin.UI.Refresh(selection.LastValidSelection, vector);
		return result;
	}

	private void PollReadouts()
	{
		double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
		foreach (RangeReadout readout in readouts)
		{
			readout.Poll(realtimeSinceStartupAsDouble, plugin.Options.EnableRangeReadout.Value, plugin.Options.RangeReadoutFrequency.Value, plugin.Options.Language.Value);
		}
	}

	public void Close()
	{
		IsOpen = false;
		firearm = null;
		entries.Clear();
		readouts.Clear();
		selection.Reset();
		vector = Vector2.zero;
		plugin.UI?.Hide();
	}
}
