using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TacticalDeviceWheel.Compatibility;
using TacticalDeviceWheel.Core;

namespace TacticalDeviceWheel.Devices;

internal sealed class DeviceDatabase
{
	private readonly Plugin plugin;

	private readonly string path;

	private DateTime lastWrite = DateTime.MinValue;

	private bool missingReported;

	private Dictionary<string, DeviceDefinition> definitions = new Dictionary<string, DeviceDefinition>();

	private readonly HashSet<string> reportedMaps = new HashSet<string>();

	public DeviceDatabase(Plugin plugin, string path)
	{
		this.plugin = plugin;
		this.path = path;
	}

	public void ReloadIfChanged()
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected O, but got Unknown
		try
		{
			if (!File.Exists(path))
			{
				if (!missingReported)
				{
					missingReported = true;
					plugin.Warning("Device database missing: " + path);
				}
				return;
			}
			missingReported = false;
			DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
			if (lastWrite == lastWriteTimeUtc)
			{
				return;
			}
			lastWrite = lastWriteTimeUtc;
			if (new FileInfo(path).Length > 2097152)
			{
				throw new InvalidDataException("Database exceeds 2 MiB.");
			}
			DatabaseFile databaseFile = JsonConvert.DeserializeObject<DatabaseFile>(File.ReadAllText(path), new JsonSerializerSettings
			{
				MaxDepth = 20,
				TypeNameHandling = (TypeNameHandling)0
			});
			if (databaseFile == null || (databaseFile.SchemaVersion != 1 && databaseFile.SchemaVersion != 2) || databaseFile.SptVersion != "4.1.5" || databaseFile.Devices == null)
			{
				throw new InvalidDataException("Expected schemaVersion=2, sptVersion=4.1.5 and devices object.");
			}
			Dictionary<string, DeviceDefinition> dictionary = new Dictionary<string, DeviceDefinition>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, DeviceDefinition> device in databaseFile.Devices)
			{
				DeviceDefinition value = device.Value;
				if (databaseFile.SchemaVersion == 1 && value?.Capabilities != null)
				{
					value.Modes = (from c in value.Capabilities
						where c != null && c.Mode.HasValue && c.Type != "Combined"
						select new NativeModeDefinition
						{
							Mode = c.Mode.Value,
							Capabilities = new List<string> { c.Type }
						}).ToList();
				}
				if (DefinitionValidator.TryBuild(device.Key, value, out var _, out var reason))
				{
					dictionary[device.Key] = value;
				}
				else
				{
					plugin.Warning("Invalid capability definition skipped: " + device.Key + ": " + reason);
				}
			}
			definitions = dictionary;
			plugin.LogInfo("Device database loaded: " + definitions.Count + " capability definitions. schema=" + databaseFile.SchemaVersion);
		}
		catch (Exception ex)
		{
			plugin.Warning("Database read failed; retaining last valid data. " + ex);
		}
	}

	public bool ShouldIgnoreStatusIndicators(string templateId)
	{
		if (definitions.TryGetValue(templateId, out var value))
		{
			return value.IgnoreStatusIndicators;
		}
		return false;
	}

	public bool Populate(TacticalDevice device, VisualSnapshot visuals)
	{
		if (!definitions.TryGetValue(device.TemplateId, out var value))
		{
			device.MappingReason = "Template is not in devices.json.";
			return false;
		}
		device.DatabaseMatched = true;
		device.MappingEvidence = value.Evidence;
		if (value.ExpectedModes != 0 && value.ExpectedModes != device.ModeCount)
		{
			device.MappingReason = "Mode count mismatch: expected " + value.ExpectedModes + ", actual " + device.ModeCount;
			return false;
		}
		if (!DefinitionValidator.TryBuild(device.TemplateId, value, out var map, out var reason))
		{
			device.MappingReason = reason;
			return false;
		}
		if (value.ModeResolver == "VanillaNativeVisuals")
		{
			DeviceVisuals[] array = visuals.Devices.Where((DeviceVisuals d) => d.InstanceId == device.InstanceId && d.TemplateId == device.TemplateId).ToArray();
			if (visuals.Error != null || array.Length == 0)
			{
				device.MappingReason = visuals.Error ?? "No live visual controller for this vanilla device instance.";
				return false;
			}
			DeviceVisuals[] array2 = array;
			foreach (DeviceVisuals deviceVisuals in array2)
			{
				if (deviceVisuals.Error != null)
				{
					device.MappingReason = deviceVisuals.Error;
					return false;
				}
				IEnumerable<VanillaModeSignature> signatures = deviceVisuals.Modes.Select((VisualMode m) => new VanillaModeSignature(m.Mode, m.VisibleLaserCount, m.InfraredLaserCount, m.UnknownLaserCount, m.WhiteLightCount, m.InfraredLightCount, m.Error != null || m.UnsupportedLightCount != 0, m.RangefinderCount, m.Error ?? ((m.UnsupportedLightCount > 0) ? ("Unsupported independent Light(s): " + string.Join(", ", m.UnsupportedLights ?? Array.Empty<string>())) : null)));
				if (!VanillaModeResolver.TryResolve(device.ModeCount, value.LaserSpectrum, signatures, value.Capabilities, out var map2, out reason))
				{
					device.MappingReason = reason;
					return false;
				}
				if (map != null && !map.Modes.SequenceEqual(map2.Modes))
				{
					device.MappingReason = "Conflicting visual controllers for the same vanilla instance.";
					return false;
				}
				map = map2;
			}
			device.MappingSource = "Database+VanillaRuntimeVisuals";
		}
		else if (value.ModeResolver == "X400NativeVisuals")
		{
			DeviceVisuals[] array3 = visuals.Devices.Where((DeviceVisuals d) => d.InstanceId == device.InstanceId && d.TemplateId == device.TemplateId).ToArray();
			if (visuals.Error != null || array3.Length == 0)
			{
				device.MappingReason = visuals.Error ?? "No live visual controller for this X400 instance.";
				return false;
			}
			ulong whiteBit = (ulong)(1L << value.Capabilities.FindIndex((CapabilityDefinition c) => c.Type == "WhiteLight"));
			ulong laserBit = (ulong)(1L << value.Capabilities.FindIndex((CapabilityDefinition c) => c.Type == "VisibleLaser"));
			DeviceVisuals[] array2 = array3;
			foreach (DeviceVisuals deviceVisuals2 in array2)
			{
				if (deviceVisuals2.Error != null)
				{
					device.MappingReason = deviceVisuals2.Error;
					return false;
				}
				IEnumerable<ModeSignature> signatures2 = deviceVisuals2.Modes.Select((VisualMode m) => new ModeSignature(m.Mode, m.LaserCount, m.IndependentSpotLightCount, m.Error != null || m.UnsupportedLightCount != 0));
				if (!X400ModeResolver.TryResolve(device.TemplateId, device.ModeCount, signatures2, whiteBit, laserBit, out var map3, out reason))
				{
					device.MappingReason = reason;
					return false;
				}
				if (map != null && !map.Modes.SequenceEqual(map3.Modes))
				{
					device.MappingReason = "Conflicting visual controllers for the same X400 instance.";
					return false;
				}
				map = map3;
			}
			device.MappingSource = "Database+X400RuntimeVisuals";
		}
		else
		{
			device.MappingSource = "Database";
		}
		if (!NativeControlPolicy.TryApply(value, map, out var map4, out reason))
		{
			device.MappingReason = reason;
			return false;
		}
		map = map4;
		device.ModeMap = map;
		int i;
		for (i = 0; i < value.Capabilities.Count; i++)
		{
			CapabilityDefinition capabilityDefinition = value.Capabilities[i];
			if (map.Modes.Values.Any((ulong mask) => (mask & (ulong)(1L << i)) != 0))
			{
				device.Capabilities.Add(new TacticalCapability
				{
					Device = device,
					Id = capabilityDefinition.Id,
					Type = capabilityDefinition.Type,
					Label = capabilityDefinition.Label,
					LabelEn = capabilityDefinition.LabelEn,
					IconKey = (string.IsNullOrWhiteSpace(capabilityDefinition.IconKey) ? capabilityDefinition.Type : capabilityDefinition.IconKey),
					Mask = (ulong)(1L << i),
					Mode = -1
				});
			}
		}
		string text = string.Join("; ", map.Modes.Select((KeyValuePair<int, ulong> m) => "Mode " + m.Key + "=" + string.Join("+", from c in device.Capabilities
			where (m.Value & c.Mask) != 0
			select c.Type)));
		if (reportedMaps.Add(device.TemplateId + ":" + text))
		{
			plugin.Debug("Capability mapping " + device.TemplateId + " [" + device.MappingSource + "]: " + text);
		}
		return true;
	}

	public static TacticalCapability Generic(TacticalDevice device, int mode, string label = null, string labelEn = null)
	{
		return new TacticalCapability
		{
			Device = device,
			Mode = mode,
			Id = "mode-" + mode,
			Type = "GenericMode",
			IconKey = "GenericMode",
			// Neutral fallback: an unmapped device still gets a usable sector name rather than a
			// placeholder that only says the definition is missing.
			Label = string.IsNullOrEmpty(label) ? "模式 " + mode : label,
			LabelEn = string.IsNullOrEmpty(labelEn) ? "Mode " + mode : labelEn,
			TargetKind = "NativeMode"
		};
	}

	/// <summary>
	///     按该模式的实际拓扑证据拼出扇区名（白光 / 可见激光 / IR 激光 / IR 补光 / 测距，组合用 + 连接）。
	///     没有证据时回退到中性的"模式 N"。
	/// </summary>
	public static void ModeLabel(VisualMode visualMode, int mode, out string label, out string labelEn)
	{
		if (visualMode == null)
		{
			label = "模式 " + mode;
			labelEn = "Mode " + mode;
			return;
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		if (visualMode.WhiteLightCount > 0)
		{
			list.Add("白光");
			list2.Add("White light");
		}
		if (visualMode.VisibleLaserCount > 0)
		{
			list.Add("可见激光");
			list2.Add("Visible laser");
		}
		if (visualMode.InfraredLaserCount > 0)
		{
			list.Add("IR 激光");
			list2.Add("IR laser");
		}
		if (visualMode.InfraredLightCount > 0)
		{
			list.Add("IR 补光");
			list2.Add("IR illuminator");
		}
		if (visualMode.RangefinderCount > 0)
		{
			list.Add("测距");
			list2.Add("Rangefinder");
		}
		if (list.Count == 0)
		{
			label = "模式 " + mode;
			labelEn = "Mode " + mode;
			return;
		}
		label = string.Join("+", list);
		labelEn = string.Join(" + ", list2);
	}
}
