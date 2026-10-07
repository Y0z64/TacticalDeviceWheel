using System;
using EFT;
using EFT.InputSystem;
using HarmonyLib;

namespace TacticalDeviceWheel.Input;

[HarmonyPatch(typeof(GamePlayerOwner), "TranslateCommand")]
internal static class CommandPatch
{
	[HarmonyPrefix]
	private static bool Prefix(GamePlayerOwner __instance, ECommand command, ref InputNode.ETranslateResult __result)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		Plugin instance = Plugin.Instance;
		if (instance?.Input == null)
		{
			return true;
		}
		try
		{
			if (instance.Input.BeforeCommand(__instance, command))
			{
				return true;
			}
			__result = (InputNode.ETranslateResult)1;
			return false;
		}
		catch (Exception e)
		{
			// This prefix runs on every translated command. Calling Fault() here could disable the
			// plugin permanently, so a failure only gets logged and the original method runs.
			instance.TransientFault("CommandPatch", e);
			return true;
		}
	}
}
