using System;
using System.Collections.Generic;
using System.Text;
using Comfort.Common;
using EFT.InputSystem;
using EFT.Settings;

namespace TacticalDeviceWheel.Compatibility;

/// <summary>
/// 4.1.5 port: reads the vanilla "tactical device input mode" and whether T is bound plainly.
/// Original 4.0.13 accessed Singleton&lt;SharedGameSettingsClass&gt;; 4.1.5 uses EFT.Settings.SettingsManager.
/// </summary>
internal static class NativeSettings
{
	private const int TacticalGameKey = 42;

	public static string Mode
	{
		get
		{
			if (!Singleton<SettingsManager>.Instantiated)
			{
				return "Unknown";
			}
			var setting = Singleton<SettingsManager>.Instance?.Game?.Controller?.Group?.TacticalInputMode;
			if (setting == null)
			{
				return "Unknown";
			}
			int value = (int)setting.Value;
			if (value == 0)
			{
				return "Toggle";
			}
			if (value == 1)
			{
				return "Hold";
			}
			return "Unknown";
		}
	}

	private static bool modeLogged;

	/// <summary>
	/// 0.4.1 诊断：Awake 阶段设置还没加载完时 Mode 会是 Unknown；
	/// 这里在第一次真正解析出结果时记一条日志，方便确认战局内是否生效。
	/// </summary>
	public static bool IsToggle
	{
		get
		{
			string mode = Mode;
			if (!modeLogged && mode != "Unknown")
			{
				modeLogged = true;
				Plugin.Instance?.LogInfo("TDW resolved EFT TacticalInputMode=" + mode + " (IsToggle=" + (mode == "Toggle") + ")");
			}
			return mode == "Toggle";
		}
	}

	public static bool HasPlainTBinding()
	{
		BindingsDiag();
		if (!Singleton<SettingsManager>.Instantiated)
		{
			return false;
		}
		var list = Singleton<SettingsManager>.Instance?.Control?.Controller?.Group?.UserKeyBindings?.Value;
		if (list == null)
		{
			return false;
		}
		foreach (KeyGroup item in list)
		{
			if ((int)item.keyName != TacticalGameKey || ((int)item.pressType != 2 && (int)item.pressType != 0))
			{
				continue;
			}
			foreach (InputSource variant in item.variants)
			{
				if (!variant.isAxis && variant.keyCode.Count == 1 && (int)variant.keyCode[0] == 116)
				{
					return true;
				}
			}
		}
		return false;
	}
	private static bool bindingsDiagDone;

	/// <summary>0.4.1 诊断：把按键绑定路径逐层打出来，便于定位 HasPlainTBinding 失败原因。</summary>
	private static void BindingsDiag()
	{
		if (bindingsDiagDone)
		{
			return;
		}
		bindingsDiagDone = true;
		try
		{
			bool inst = Singleton<SettingsManager>.Instantiated;
			SettingsManager mgr = (inst ? Singleton<SettingsManager>.Instance : null);
			var control = mgr?.Control;
			var controller = control?.Controller;
			var group = controller?.Group;
			var ukn = group?.UserKeyBindings;
			var list = ukn?.Value;
			Plugin.Instance?.LogInfo("[DIAG] bindings: instantiated=" + inst + " mgr=" + (mgr != null) + " control=" + (control != null) + " controller=" + (controller != null) + " group=" + (group != null) + " ukn=" + (ukn != null) + " count=" + ((list == null) ? (-1) : list.Count));
			if (list == null)
			{
				return;
			}
			int found = 0;
			foreach (KeyGroup kg in list)
			{
				if ((int)kg.keyName != TacticalGameKey)
				{
					continue;
				}
				found++;
				StringBuilder vs = new StringBuilder();
				if (kg.variants != null)
				{
					foreach (InputSource v in kg.variants)
					{
						vs.Append(" [axis=").Append(v.isAxis).Append(" n=").Append((v.keyCode == null) ? (-1) : v.keyCode.Count);
						if (v.keyCode != null && v.keyCode.Count > 0)
						{
							vs.Append(" k0=").Append((int)v.keyCode[0]);
						}
						vs.Append(']');
					}
				}
				Plugin.Instance?.LogInfo("[DIAG] Tactical binding #" + found + ": pressType=" + (int)kg.pressType + " variants=" + ((kg.variants == null) ? (-1) : kg.variants.Count) + vs.ToString());
			}
			Plugin.Instance?.LogInfo("[DIAG] Tactical bindings found: " + found + " of " + list.Count);
		}
		catch (Exception e)
		{
			Plugin.Instance?.LogInfo("[DIAG] bindings diag failed: " + e.Message);
		}
	}
}
