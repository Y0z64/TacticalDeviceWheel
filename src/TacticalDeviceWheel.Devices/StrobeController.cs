using FirearmController = EFT.Player.FirearmController;
using System;
using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using TacticalDeviceWheel.Compatibility;
using TacticalDeviceWheel.Core;
using UnityEngine;

namespace TacticalDeviceWheel.Devices;

internal sealed class StrobeController
{
	private sealed class Entry
	{
		public TacticalDevice Device;

		public StrobeLease Lease;
	}

	private readonly Plugin plugin;

	private readonly List<Entry> entries = new List<Entry>();

	private readonly PeriodicGate clock = new PeriodicGate();

	private EFT.LightsState[] batch = Array.Empty<EFT.LightsState>();

	private Player player;

	private FirearmController firearm;

	private bool phaseOn = true;

	private bool HeldAndAlive
	{
		get
		{
			if ((object)player != null && player.IsYourPlayer && player.HealthController != null && player.HealthController.IsAlive && (object)firearm != null)
			{
				return player.HandsController == firearm;
			}
			return false;
		}
	}

	public StrobeController(Plugin plugin)
	{
		this.plugin = plugin;
	}

	private Entry Find(string id)
	{
		for (int i = 0; i < entries.Count; i++)
		{
			if (entries[i].Device.InstanceId == id)
			{
				return entries[i];
			}
		}
		return null;
	}

	public bool IsRunning(string id)
	{
		Entry entry = Find(id);
		if (entry != null)
		{
			return !entry.Lease.Restoring;
		}
		return false;
	}

	public bool IsRestoring(string id)
	{
		return Find(id)?.Lease.Restoring ?? false;
	}

	public bool HasSession(string id)
	{
		return Find(id) != null;
	}

	public void ReadStableState(TacticalDevice device, out bool active, out int mode)
	{
		Entry entry = Find(device.InstanceId);
		LightComponent component = device.Component;
		if (entry != null && entry.Lease.Owns(component.IsActive, component.SelectedMode))
		{
			active = entry.Lease.Plan.Original.IsActive;
			mode = entry.Lease.Plan.Original.Mode;
		}
		else
		{
			active = component.IsActive;
			mode = component.SelectedMode;
		}
	}

	public bool CanStart(TacticalDevice device)
	{
		if (!plugin.Options.EnableStrobe.Value || device.ModeMap == null)
		{
			return false;
		}
		StrobePlan plan;
		return TryPlan(device, out plan);
	}

	private static bool TryPlan(TacticalDevice device, out StrobePlan plan)
	{
		ulong whiteLight = 0uL;
		foreach (TacticalCapability capability in device.Capabilities)
		{
			if (capability.TargetKind == "CapabilitySet" && capability.Type == "WhiteLight")
			{
				whiteLight = capability.Mask;
				break;
			}
		}
		return StrobePlan.TryCreate(device.ModeMap, device.Component.IsActive, device.Component.SelectedMode, whiteLight, out plan);
	}

	public bool Toggle(Player actor, FirearmController controller, TacticalDevice device)
	{
		if (Find(device.InstanceId) != null)
		{
			return StopDevice(device.InstanceId);
		}
		if (!plugin.Options.Enabled.Value || !plugin.Options.EnableStrobe.Value || !NativeSettings.IsToggle || (object)actor == null || !actor.IsYourPlayer || actor.HealthController == null || !actor.HealthController.IsAlive || actor.HandsController != controller || !TryPlan(device, out var plan))
		{
			return false;
		}
		if (entries.Count > 0 && controller != firearm)
		{
			Clear();
		}
		player = actor;
		firearm = controller;
		bool num = entries.Count == 0;
		if (num)
		{
			phaseOn = true;
		}
		Entry item = new Entry
		{
			Device = device,
			Lease = new StrobeLease(plan)
		};
		entries.Add(item);
		bool flag = Flush(phaseOn);
		if (!flag)
		{
			entries.Remove(item);
		}
		if (num)
		{
			clock.Reset();
			clock.Take(Time.realtimeSinceStartupAsDouble, plugin.Options.StrobeFrequency.Value * 2f);
		}
		plugin.Debug("STROBE START instance=" + device.InstanceId + " hz=" + plugin.Options.StrobeFrequency.Value + " accepted=" + flag);
		if (entries.Count == 0)
		{
			Clear();
		}
		return flag;
	}

	public bool StopDevice(string id)
	{
		Entry entry = Find(id);
		if (entry == null)
		{
			return true;
		}
		entry.Lease.Stop();
		bool result = NativeSettings.IsToggle && HeldAndAlive && Flush(phaseOn);
		plugin.Debug("STROBE STOP instance=" + id + " restored=" + result);
		if (entries.Count == 0)
		{
			Clear();
		}
		return result;
	}

	public void YieldToNative(string reason)
	{
		if (entries.Count == 0)
		{
			return;
		}
		foreach (Entry entry in entries)
		{
			entry.Lease.Stop();
		}
		bool flag = NativeSettings.IsToggle && HeldAndAlive && Flush(phaseOn);
		plugin.Debug("STROBE END reason=" + reason + " restored=" + flag);
		Clear();
	}

	public void Update()
	{
		if (entries.Count == 0)
		{
			return;
		}
		if (!NativeSettings.IsToggle || !HeldAndAlive)
		{
			Clear();
			return;
		}
		if (!plugin.Options.Enabled.Value || !plugin.Options.EnableStrobe.Value || !Application.isFocused || (player != null && player.IsInventoryOpened) || Time.timeScale <= 0f)
		{
			foreach (Entry entry in entries)
			{
				entry.Lease.Stop();
			}
		}
		if (clock.Take(Time.realtimeSinceStartupAsDouble, plugin.Options.StrobeFrequency.Value * 2f))
		{
			if (Flush(!phaseOn))
			{
				phaseOn = !phaseOn;
			}
			if (entries.Count == 0)
			{
				Clear();
			}
		}
	}

	private bool Flush(bool nextOn)
	{
		for (int num = entries.Count - 1; num >= 0; num--)
		{
			Entry entry = entries[num];
			LightComponent component = entry.Device.Component;
			if (component == null || component._template == null || component._template.ModesCount != entry.Device.ModeCount || !entry.Lease.Owns(component.IsActive, component.SelectedMode))
			{
				plugin.Debug("STROBE RELEASE external change instance=" + entry.Device.InstanceId);
				entries.RemoveAt(num);
			}
			else if (entry.Lease.Restoring && Matches(component.IsActive, component.SelectedMode, entry.Lease.Plan.Original))
			{
				entries.RemoveAt(num);
			}
		}
		if (entries.Count == 0)
		{
			return true;
		}
		if (batch.Length != entries.Count)
		{
			batch = (EFT.LightsState[])(object)new EFT.LightsState[entries.Count];
		}
		bool flag = false;
		for (int i = 0; i < entries.Count; i++)
		{
			Entry entry2 = entries[i];
			NativeTarget target = entry2.Lease.Desired(nextOn);
			batch[i] = new EFT.LightsState
			{
				Id = entry2.Device.InstanceId,
				IsActive = target.IsActive,
				LightMode = target.Mode
			};
			flag |= !Matches(entry2.Device.Component.IsActive, entry2.Device.Component.SelectedMode, target);
		}
		// Same reason as DeviceController.Toggle: with force=false the native path dereferences
		// CurrentOperation, which is null when no weapon operation is in progress.
		if (firearm.CurrentOperation == null)
		{
			Clear();
			plugin.Warning("No weapon operation in progress; strobe stopped.");
			return false;
		}
		try
		{
			if (flag && !firearm.SetLightsState(batch, false, false))
			{
				return false;
			}
		}
		catch (Exception ex)
		{
			// A third-party visual postfix can throw after the component state has already been
			// switched. Verify the actual state before tearing the strobe down: if every entry
			// reached its target for this phase, the strobe can continue.
			bool flag3 = true;
			for (int num3 = 0; num3 < entries.Count; num3++)
			{
				Entry entry4 = entries[num3];
				NativeTarget target3 = entry4.Lease.Desired(nextOn);
				if (!Matches(entry4.Device.Component.IsActive, entry4.Device.Component.SelectedMode, target3))
				{
					flag3 = false;
					break;
				}
			}
			if (!flag3)
			{
				Clear();
				plugin.Warning("STROBE stopped after native operation error: " + ex);
				return false;
			}
			plugin.Debug("STROBE continuing: the native call threw but every component reached its target. " + ex.Message);
		}
		for (int num2 = entries.Count - 1; num2 >= 0; num2--)
		{
			Entry entry3 = entries[num2];
			NativeTarget target2 = entry3.Lease.Desired(nextOn);
			if (!Matches(entry3.Device.Component.IsActive, entry3.Device.Component.SelectedMode, target2))
			{
				plugin.Debug("STROBE RELEASE target not applied instance=" + entry3.Device.InstanceId);
				entries.RemoveAt(num2);
			}
			else
			{
				entry3.Lease.Accepted(target2);
				if (entry3.Lease.Restoring)
				{
					entries.RemoveAt(num2);
				}
			}
		}
		return true;
	}

	private static bool Matches(bool active, int mode, NativeTarget target)
	{
		if (active == target.IsActive)
		{
			return mode == target.Mode;
		}
		return false;
	}

	private void Clear()
	{
		entries.Clear();
		batch = Array.Empty<EFT.LightsState>();
		player = null;
		firearm = null;
		clock.Reset();
		phaseOn = true;
	}
}
