using System;
using EFT;
using HarmonyLib;

namespace TacticalDeviceWheel.Input;

[HarmonyPatch(typeof(PlayerOwner), "TranslateAxes")]
internal static class AxesPatch
{
	[HarmonyPrefix]
	private static void Prefix(PlayerOwner __instance, ref float[] axes)
	{
		Plugin instance = Plugin.Instance;
		if (instance?.Input == null)
		{
			return;
		}
		try
		{
			instance.Input.ObserveAxes(__instance, axes);
		}
		catch (Exception e)
		{
			// This prefix is attached to PlayerOwner.TranslateAxes, which runs every frame, so a
			// failure here could reach Fault()'s threshold in about two seconds and permanently
			// disable the plugin. Log it and carry on instead.
			instance.TransientFault("AxesPatch", e);
		}
	}
}
