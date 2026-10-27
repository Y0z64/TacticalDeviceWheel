using FirearmController = EFT.Player.FirearmController;
using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using Newtonsoft.Json;
using TacticalDeviceWheel.Compatibility;
using TacticalDeviceWheel.Core;
using UnityEngine;

namespace TacticalDeviceWheel.Devices;

internal sealed class WeaponDeviceScanner
{
	private readonly Plugin plugin;

	private readonly ScanDiagnostics diagnostics;

	private readonly HashSet<string> warned = new HashSet<string>();

	public WeaponDeviceScanner(Plugin plugin)
	{
		this.plugin = plugin;
		diagnostics = new ScanDiagnostics(plugin);
	}

	public List<TacticalCapability> Scan(FirearmController firearm)
	{
		plugin.Database.ReloadIfChanged();
		List<TacticalCapability> list = new List<TacticalCapability>();
		List<TacticalDevice> list2 = new List<TacticalDevice>();
		List<string> list3 = new List<string>();
		VisualSnapshot visualSnapshot = ModeDiagnostics.Snapshot(firearm, plugin.Database.ShouldIgnoreStatusIndicators);
		Dictionary<string, string> dictionary = (from s in ((CompoundItem)firearm.Item).AllSlots
			where s.ContainedItem != null
			group s by s.ContainedItem.Id).ToDictionary((IGrouping<string, Slot> g) => g.Key, (IGrouping<string, Slot> g) => g.First().ID);
		Dictionary<string, LightComponent> currentLightStatus = firearm.GetCurrentLightStatus();
		foreach (KeyValuePair<string, LightComponent> pair in currentLightStatus.OrderBy<KeyValuePair<string, LightComponent>, string>((KeyValuePair<string, LightComponent> p) => p.Key, StringComparer.Ordinal))
		{
			try
			{
				LightComponent value = pair.Value;
				EFT.InventoryLogic.ILightComponentTemplate ginterface396_ = value._template;
				int num = ((ginterface396_ != null) ? ginterface396_.ModesCount : 0);
				string tpl = ((EFT.InventoryLogic.LightComponent)value).Item.StringTemplateId;
				string value2;
				TacticalDevice tacticalDevice = new TacticalDevice
				{
					InstanceId = pair.Key,
					TemplateId = tpl,
					Component = value,
					ModeCount = num,
					Strobes = plugin.Strobes,
					DisplayName = DisplayName.ParseDisplayName(EFT.LocalizationExtensions.Localized(((EFT.InventoryLogic.LightComponent)value).Item.ShortName, (string)null)),
					Ordinal = list2.Count + 1,
					Slot = (dictionary.TryGetValue(pair.Key, out value2) ? value2 : "")
				};
				list2.Add(tacticalDevice);
				bool flag = false;
				if (num <= 0 || num > 64)
				{
					tacticalDevice.MappingReason = "Unsupported native mode count: " + num;
				}
				else
				{
					flag = plugin.Database.Populate(tacticalDevice, visualSnapshot);
					if (!flag && plugin.Options.GenericModes.Value)
					{
						// Not in devices.json. Name each sector from what the mode actually does,
						// instead of labelling it unmapped.
						List<VisualMode> modeVisuals = visualSnapshot?.Devices?.Where((DeviceVisuals v) => v.InstanceId == pair.Key && v.TemplateId == tpl).SelectMany((DeviceVisuals v) => v.Modes).ToList();
						for (int i = 0; i < num; i++)
						{
							DeviceDatabase.ModeLabel(modeVisuals?.FirstOrDefault((VisualMode m) => m.Mode == i), i, out var text, out var text2);
							tacticalDevice.Capabilities.Add(DeviceDatabase.Generic(tacticalDevice, i, text, text2));
						}
					}
				}
				if (flag && tacticalDevice.Capabilities.Any((TacticalCapability c) => c.Type == "Rangefinder"))
				{
					tacticalDevice.RangeReadout = new RangeReadout(tacticalDevice, (from m in visualSnapshot.Devices.Where((DeviceVisuals v) => v.InstanceId == pair.Key && v.TemplateId == tpl).SelectMany((DeviceVisuals v) => v.Modes)
						group m by m.Mode).ToDictionary((IGrouping<int, VisualMode> g) => g.Key, (IGrouping<int, VisualMode> g) => g.SelectMany((VisualMode m) => m.RangeSources).ToArray()));
					tacticalDevice.RangeReadout.Poll(Time.realtimeSinceStartupAsDouble, plugin.Options.EnableRangeReadout.Value, plugin.Options.RangeReadoutFrequency.Value, plugin.Options.Language.Value);
				}
				if (flag && plugin.Options.EnableStrobe.Value && tacticalDevice.Capabilities.Any((TacticalCapability c) => c.Type == "WhiteLight"))
				{
					tacticalDevice.Capabilities.Add(new TacticalCapability
					{
						Device = tacticalDevice,
						Id = "white-light-strobe",
						Type = "WhiteLightStrobe",
						Label = "白光爆闪",
						LabelEn = "White-light strobe",
						IconKey = "WhiteLightStrobe",
						TargetKind = "Strobe",
						Mode = -1
					});
				}
				if (!flag && warned.Add(tpl + ":" + tacticalDevice.MappingReason))
				{
					plugin.Warning("No capability mapping: tpl=" + tpl + ", ModesCount=" + num + ". " + tacticalDevice.MappingReason);
					plugin.LogInfo("TDW MAPPING EVIDENCE " + JsonConvert.SerializeObject((object)new
					{
						templateId = tpl,
						instanceId = pair.Key,
						visualError = visualSnapshot.Error,
						reason = tacticalDevice.MappingReason,
						visuals = visualSnapshot.Devices.Where((DeviceVisuals v) => v.TemplateId == tpl && v.InstanceId == pair.Key).ToArray()
					}));
				}
				try
				{
					tacticalDevice.ItemIcon = ItemViewFactory.LoadItemIcon(((EFT.InventoryLogic.LightComponent)value).Item, 1, false);
				}
				catch (Exception ex)
				{
					plugin.Debug("Device icon unavailable: " + ex);
				}
				list.AddRange(tacticalDevice.Capabilities);
				plugin.Debug($"SCAN device={tacticalDevice.Name} tpl={tpl} instance={pair.Key} slot={tacticalDevice.Slot} modes={num} selected={value.SelectedMode} active={value.IsActive} mapping={tacticalDevice.MappingSource}");
			}
			catch (Exception ex2)
			{
				list3.Add("instance=" + pair.Key + ": " + ex2);
				plugin.Error("Device scan failed: instance=" + pair.Key + ": " + ex2);
			}
		}
		diagnostics.Write(firearm, currentLightStatus.Count, list2, visualSnapshot, list3);
		return list;
	}
}
