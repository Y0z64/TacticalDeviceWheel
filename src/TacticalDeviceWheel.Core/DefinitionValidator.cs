using System;
using System.Collections.Generic;
using System.Linq;

namespace TacticalDeviceWheel.Core;

public static class DefinitionValidator
{
	public const string X400Template = "56def37dd2720bec348b456a";

	public static bool TryBuild(string templateId, DeviceDefinition definition, out NativeModeMap map, out string reason)
	{
		map = null;
		reason = "Invalid definition.";
		// VanillaNativeVisuals is the one resolver allowed to declare zero expected modes; every
		// other resolver needs at least one. Written out rather than as a negated ternary.
		int minimumModes = definition?.ModeResolver == "VanillaNativeVisuals" ? 0 : 1;
		if (templateId == null || templateId.Length != 24 || !templateId.All(Uri.IsHexDigit) || definition == null || definition.ExpectedModes < minimumModes || definition.ExpectedModes > 64 || definition.Capabilities == null || definition.Capabilities.Count < 1 || definition.Capabilities.Count > 64)
		{
			return false;
		}
		Dictionary<string, ulong> dictionary = new Dictionary<string, ulong>(StringComparer.Ordinal);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < definition.Capabilities.Count; i++)
		{
			CapabilityDefinition capabilityDefinition = definition.Capabilities[i];
			if (capabilityDefinition == null || string.IsNullOrWhiteSpace(capabilityDefinition.Type) || string.IsNullOrWhiteSpace(capabilityDefinition.Id) || string.IsNullOrWhiteSpace(capabilityDefinition.Label) || dictionary.ContainsKey(capabilityDefinition.Type) || !hashSet.Add(capabilityDefinition.Id))
			{
				return false;
			}
			dictionary.Add(capabilityDefinition.Type, (ulong)(1L << i));
		}
		if (!NativeControlPolicy.TryMasks(definition, out var _, out var _, out reason))
		{
			return false;
		}
		if (!string.IsNullOrEmpty(definition.ModeResolver))
		{
			if (definition.ModeResolver == "VanillaNativeVisuals")
			{
				// This was four doubly-negated comparisons. It means "the spectrum is one of the
				// four known values", which is what it now says.
				bool knownSpectrum = definition.LaserSpectrum == "None" || definition.LaserSpectrum == "VisibleOnly" || definition.LaserSpectrum == "InfraredOnly" || definition.LaserSpectrum == "Mixed";
				if (knownSpectrum)
				{
					List<NativeModeDefinition> modes = definition.Modes;
					if ((modes == null || modes.Count <= 0) && !dictionary.Keys.Any((string t) => t != "WhiteLight" && t != "VisibleLaser" && t != "IRLaser" && t != "IRIlluminator" && t != "Rangefinder"))
					{
						reason = null;
						return true;
					}
				}
				reason = "Invalid vanilla visual profile; mode indices must come from runtime nodes.";
				return false;
			}
			// Same double-negation rewrite as above. X400Template already holds this literal.
			if (definition.ModeResolver == "X400NativeVisuals" && templateId == X400Template && definition.ExpectedModes == 3 && dictionary.Count == 2 && dictionary.ContainsKey("WhiteLight") && dictionary.ContainsKey("VisibleLaser"))
			{
				List<NativeModeDefinition> modes2 = definition.Modes;
				if (modes2 == null || modes2.Count <= 0)
				{
					reason = null;
					return true;
				}
			}
			reason = "Only the exact X400 two-capability runtime resolver is supported.";
			return false;
		}
		if (definition.Modes == null || definition.Modes.Count != definition.ExpectedModes)
		{
			reason = "Every native mode must be mapped, including combination modes.";
			return false;
		}
		Dictionary<int, ulong> dictionary2 = new Dictionary<int, ulong>();
		ulong num = 0uL;
		foreach (NativeModeDefinition mode in definition.Modes)
		{
			if (mode == null || mode.Mode < 0 || mode.Mode >= definition.ExpectedModes || dictionary2.ContainsKey(mode.Mode) || mode.Capabilities == null || mode.Capabilities.Count == 0)
			{
				return false;
			}
			ulong num2 = 0uL;
			foreach (string capability in mode.Capabilities)
			{
				if (capability == null || !dictionary.TryGetValue(capability, out var value) || (num2 & value) != 0L)
				{
					return false;
				}
				num2 |= value;
			}
			dictionary2.Add(mode.Mode, num2);
			num |= num2;
		}
		if (num != dictionary.Values.Aggregate(0uL, (ulong a, ulong b) => a | b))
		{
			return false;
		}
		return NativeControlPolicy.TryApply(definition, new NativeModeMap(dictionary2, 0uL), out map, out reason);
	}
}
