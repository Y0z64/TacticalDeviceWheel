using System.Linq;
using TacticalDeviceWheel.Core;

namespace TacticalDeviceWheel.Devices;

internal sealed class TacticalCapability
{
	public string Id;

	public string Type;

	public string Label;

	public string LabelEn;

	public string IconKey;

	public int Mode;

	public ulong Mask;

	public TacticalDevice Device;

	public string TargetKind = "CapabilitySet";

	public string UniqueId => Device.InstanceId + ":" + Id;

	public bool IsOn
	{
		get
		{
			if (TargetKind == "Strobe")
			{
				return Device.Strobes?.IsRunning(Device.InstanceId) ?? false;
			}
			if (TargetKind == "NativeMode")
			{
				if (Device.Component != null && Device.Component.IsActive)
				{
					return Device.Component.SelectedMode == Mode;
				}
				return false;
			}
			if (Device.Component != null && Device.ModeMap != null && Device.ModeMap.TryCurrent(Device.Component.IsActive, Device.Component.SelectedMode, out var mask))
			{
				return (mask & Mask) != 0;
			}
			return false;
		}
	}

	public bool CanToggle
	{
		get
		{
			StrobeController strobes = Device.Strobes;
			if (strobes != null && strobes.IsRestoring(Device.InstanceId))
			{
				return false;
			}
			if (TargetKind == "Strobe")
			{
				if (!IsOn)
				{
					return Device.Strobes?.CanStart(Device) ?? false;
				}
				return true;
			}
			if (TargetKind == "NativeMode")
			{
				return true;
			}
			if (Type == "WhiteLight")
			{
				StrobeController strobes2 = Device.Strobes;
				if (strobes2 != null && strobes2.IsRunning(Device.InstanceId))
				{
					return true;
				}
			}
			NativeTarget target;
			return TryTarget(out target);
		}
	}

	public bool IsLinked
	{
		get
		{
			if (TargetKind == "CapabilitySet" && Device.ModeMap != null && (Device.ModeMap.RequiredWhileActiveMask & Mask) != 0L && IsOn)
			{
				return !CanToggle;
			}
			return false;
		}
	}

	public string ActionHint(UiLanguage language)
	{
		StrobeController strobes = Device.Strobes;
		if (strobes != null && strobes.IsRestoring(Device.InstanceId))
		{
			return Localization.Pick(language, "Restoring previous state\nWaiting for the weapon action to finish", "正在恢复原状态\n等待原版武器操作结束");
		}
		if (TargetKind == "Strobe")
		{
			if (!IsOn)
			{
				if (!CanToggle)
				{
					return Localization.Pick(language, "Cannot strobe while keeping the other functions on", "无法在保留其他功能时爆闪");
				}
				return Localization.Pick(language, "LMB: start strobe\nKeeps going after the wheel closes", "左键：启动爆闪\n关闭轮盘后继续");
			}
			return Localization.Pick(language, "LMB: stop strobe\nRestores the previous state", "左键：停止爆闪\n恢复启动前状态");
		}
		bool flag = Device.Strobes?.IsRunning(Device.InstanceId) ?? false;
		if (flag && Type == "WhiteLight")
		{
			return Localization.Pick(language, "LMB: stop strobe and stay on", "左键：停止爆闪并常亮");
		}
		string text = (IsOn ? Localization.Pick(language, "LMB: turn off", "左键：关闭") : Localization.Pick(language, "LMB: turn on", "左键：开启"));
		if (flag)
		{
			text = Localization.Pick(language, "LMB: stop strobe, then toggle", "左键：停止爆闪后切换");
		}
		if (TargetKind == "NativeMode")
		{
			return text + Localization.Pick(language, "\nThis device has no function mapping yet", "\n该设备尚未建立功能映射");
		}
		if (!TryTarget(out var target))
		{
			if (!IsLinked)
			{
				return Localization.Pick(language, "No matching vanilla mode", "原版无对应组合");
			}
			return Localization.Pick(language, "Rangefinder is linked to the device\nTurn off the laser/illuminator first", "测距随设备联动\n先关闭激光/补光");
		}
		if (target.AutomaticallyDisabledMask != 0L)
		{
			text = text + Localization.Pick(language, "\nWill turn off: ", "\n将关闭：") + Names(target.AutomaticallyDisabledMask, language);
		}
		if (target.AutomaticallyEnabledMask != 0L)
		{
			text = text + Localization.Pick(language, "\nAlso turns on: ", "\n联动开启：") + Names(target.AutomaticallyEnabledMask, language);
		}
		return text;
	}

	public string DisplayLabel(UiLanguage language)
	{
		return Localization.Capability(language, Type, Label, LabelEn);
	}

	private bool TryTarget(out NativeTarget target)
	{
		bool active = Device.Component.IsActive;
		int mode = Device.Component.SelectedMode;
		Device.Strobes?.ReadStableState(Device, out active, out mode);
		target = default(NativeTarget);
		string reason;
		if (Device.ModeMap != null)
		{
			return Device.ModeMap.TryToggle(active, mode, Mask, out target, out reason);
		}
		return false;
	}

	private string Names(ulong mask, UiLanguage language)
	{
		return string.Join(Localization.Pick(language, ", ", "、"), from c in Device.Capabilities
			where (c.Mask & mask) != 0
			select c.DisplayLabel(language));
	}
}
