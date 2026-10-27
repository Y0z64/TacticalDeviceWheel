using FirearmController = EFT.Player.FirearmController;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using EFT;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using TacticalDeviceWheel.Compatibility;
using TacticalDeviceWheel.Core;

namespace TacticalDeviceWheel.Devices;

internal sealed class ScanDiagnostics
{
	private readonly Plugin plugin;

	private bool reportedPath;

	public ScanDiagnostics(Plugin plugin)
	{
		this.plugin = plugin;
	}

	public void Write(FirearmController firearm, int count, List<TacticalDevice> devices, VisualSnapshot visuals, List<string> issues)
	{
		if (!plugin.Options.WriteScanReport.Value)
		{
			return;
		}
		string fullPath = Path.GetFullPath(Path.Combine(plugin.RootPath, "diagnostics", "last-scan.json"));
		try
		{
			DisplayName displayName = DisplayName.ParseDisplayName(EFT.LocalizationExtensions.Localized(((Item)firearm.Item).ShortName, (string)null));
			ulong mask;
			string json = JsonConvert.SerializeObject((object)new
			{
				schemaVersion = 5,
				tdwVersion = "0.4.3",
				target = "SPT 4.1.x / EFT 40743",
				timestampUtc = DateTime.UtcNow.ToString("O"),
				pluginPath = ((BaseUnityPlugin)plugin).Info.Location,
				assemblyVersion = typeof(Plugin).Assembly.GetName().Version.ToString(),
				pluginMvid = typeof(Plugin).Module.ModuleVersionId,
				serverAnnouncement = ServerAnnouncement.Status,
				tacticalInputMode = NativeSettings.Mode,
				diagnosticsPath = fullPath,
				debugLogging = plugin.Options.DebugLogging.Value,
				closeOnTRelease = plugin.Options.CloseOnTRelease.Value,
				uiLanguage = plugin.Options.Language.Value.ToString(),
				functionIconScale = plugin.Options.FunctionIconScale.Value,
				deviceIconScale = plugin.Options.DeviceIconScale.Value,
				strobeEnabled = plugin.Options.EnableStrobe.Value,
				strobeFrequencyHz = plugin.Options.StrobeFrequency.Value,
				rangeReadoutEnabled = plugin.Options.EnableRangeReadout.Value,
				rangeReadoutRefreshHz = plugin.Options.RangeReadoutFrequency.Value,
				weapon = new
				{
					rawName = displayName.RawText,
					plainName = displayName.PlainText,
					displayColor = displayName.ColorHex,
					instanceId = ((Item)firearm.Item).Id,
					templateId = ((Item)firearm.Item).StringTemplateId
				},
				tacticalDeviceCount = count,
				radialItemCount = devices.Sum((TacticalDevice d) => d.Capabilities.Count),
				scanIssues = issues,
				devices = devices.Select((TacticalDevice d) => new
				{
					instanceId = d.InstanceId,
					templateId = d.TemplateId,
					slot = d.Slot,
					ordinal = d.Ordinal,
					rawName = d.DisplayName.RawText,
					plainName = d.DisplayName.PlainText,
					displayColor = d.DisplayName.ColorHex,
					isActive = d.Component.IsActive,
					selectedMode = d.Component.SelectedMode,
					modeCount = d.ModeCount,
					strobing = (d.Strobes?.IsRunning(d.InstanceId) ?? false),
					strobeAvailable = d.Capabilities.Any((TacticalCapability c) => c.TargetKind == "Strobe" && c.CanToggle),
					rangeReadoutSources = (d.RangeReadout?.SourceCount ?? 0),
					rangeReadout = d.RangeReadout?.Value,
					databaseMatched = d.DatabaseMatched,
					mappingResolved = (d.ModeMap != null),
					mappingSource = d.MappingSource,
					mappingReason = d.MappingReason,
					evidence = d.MappingEvidence,
					nativeControlPolicy = ((d.ModeMap == null) ? null : new
					{
						requiredWhileActive = (from c in d.Capabilities
							where (d.ModeMap.RequiredWhileActiveMask & c.Mask) != 0
							select c.Type).ToArray(),
						exclusiveGroups = d.ModeMap.ExclusiveGroups.Select((ulong g) => (from c in d.Capabilities
							where (g & c.Mask) != 0
							select c.Type).ToArray()).ToArray()
					}),
					currentCapabilities = ((d.ModeMap != null && d.ModeMap.TryCurrent(d.Component.IsActive, d.Component.SelectedMode, out mask)) ? (from c in d.Capabilities
						where (mask & c.Mask) != 0
						select c.Type).ToArray() : null),
					nativeModes = d.ModeMap?.Modes.Select((KeyValuePair<int, ulong> m) => new
					{
						mode = m.Key,
						capabilityMask = "0x" + m.Value.ToString("X"),
						capabilities = (from c in d.Capabilities
							where (m.Value & c.Mask) != 0
							select c.Type).ToArray()
					}).ToArray(),
					capabilities = d.Capabilities.Select((TacticalCapability c) => new
					{
						id = c.Id,
						uniqueId = c.UniqueId,
						type = c.Type,
						label = c.Label,
						labelEn = c.LabelEn,
						iconKey = c.IconKey,
						targetKind = c.TargetKind,
						capabilityMask = "0x" + c.Mask.ToString("X"),
						fallbackMode = ((c.TargetKind == "NativeMode") ? new int?(c.Mode) : null),
						isOn = c.IsOn,
						canToggle = c.CanToggle
					}).ToArray(),
					unknownFields = UnknownFields(d, visuals)
				}).ToArray(),
				visualModes = visuals
			}, (Formatting)1);
			ReportFile.Write(fullPath, json);
			if (!reportedPath)
			{
				reportedPath = true;
				plugin.Debug("Scan report saved: " + fullPath + " (updated on every scan; DebugLogging is not required)");
			}
			else
			{
				plugin.Debug("Scan report updated: " + fullPath);
			}
			if (visuals.Error != null)
			{
				plugin.Warning("Visual scan incomplete; basic report still saved. " + visuals.Error);
			}
		}
		catch (Exception ex)
		{
			plugin.Error("Could not write scan report. Path=" + fullPath + ". " + ex);
		}
	}

	private static string[] UnknownFields(TacticalDevice d, VisualSnapshot visuals)
	{
		List<string> list = new List<string>();
		if (d.ModeMap == null)
		{
			list.Add("NativeModeCapabilityMapping: " + d.MappingReason);
		}
		if (d.ModeMap != null && !d.ModeMap.TryCurrent(d.Component.IsActive, d.Component.SelectedMode, out var _))
		{
			list.Add("CurrentCapabilities: current native mode is unmapped.");
		}
		if (string.IsNullOrEmpty(d.Slot))
		{
			list.Add("AttachmentSlot");
		}
		if (visuals.Error != null || !visuals.Devices.Any((DeviceVisuals v) => v.InstanceId == d.InstanceId && v.Error == null))
		{
			list.Add("VisualModeNodes");
		}
		return list.ToArray();
	}
}
