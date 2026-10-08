using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using EFT;
using HarmonyLib;
using TacticalDeviceWheel.Compatibility;
using TacticalDeviceWheel.Devices;
using TacticalDeviceWheel.Input;
using TacticalDeviceWheel.UI;
using UnityEngine;

namespace TacticalDeviceWheel;

[BepInPlugin("com.xunhuaizhuo.tdw", "Tactical Device Wheel", "0.4.2")]
[BepInProcess("EscapeFromTarkov.exe")]
[BepInDependency("com.SPT.core")]
public sealed class Plugin : BaseUnityPlugin
{
	internal static Plugin Instance;

	/// <summary>
	/// Module version id of the Assembly-CSharp build this port was verified against
	/// (EFT 0.16.9.40743). This is the strict half of the startup check: the patches bind to the
	/// game's API surface, so a different game build is refused outright.
	/// </summary>
	internal const string VerifiedGameMvid = "cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7";

	internal Configuration Options;

	internal InputController Input;

	internal DeviceDatabase Database;

	internal RadialMenuController Menu;

	internal RadialMenuUI UI;

	internal IconManager Icons;

	internal StrobeController Strobes;

	internal string RootPath;

	private Harmony harmony;

	private bool failed;

	private string lastMode;

	private float modeCheck;

	private int faultCount;

	private int transientFaultCount;

	private void Awake()
	{
		Instance = this;
		Options = new Configuration(Config);
		RootPath = Path.GetFullPath(Path.GetDirectoryName(Info.Location));
		try
		{
			// Every SPT 4.1.x ships the same EFT 0.16.9.40743 game build, so the API surface this
			// port binds to is identical across the series; only the server patch level differs.
			// The game side is therefore pinned strictly by the module version id below, while the
			// SPT check only has to confirm the 4.1 series rather than one exact patch release.
			var sptCoreVersion = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "spt-core")?.GetName().Version;
			if (sptCoreVersion == null || sptCoreVersion.Major != 4 || sptCoreVersion.Minor != 1 || typeof(Player).Module.ModuleVersionId != new Guid(VerifiedGameMvid))
			{
				Warning("TDW disabled: requires SPT 4.1.x on EFT 0.16.9.40743 (spt-core found: " + (sptCoreVersion?.ToString() ?? "not loaded") + ").");
				return;
			}
			Icons = new IconManager(this);
			Icons.Preload();
			Database = new DeviceDatabase(this, Path.Combine(RootPath, "devices.json"));
			Database.ReloadIfChanged();
			Strobes = new StrobeController(this);
			UI = new RadialMenuUI(this);
			Menu = new RadialMenuController(this);
			Input = new InputController(this);
			harmony = new Harmony("com.xunhuaizhuo.tdw");
			harmony.PatchAll(typeof(Plugin).Assembly);
			LogInfo("TDW 0.4.2 loaded. SPT " + sptCoreVersion + " / EFT 0.16.9.40743. Native Hold bypass enabled.");
			LogInfo("TDW root: " + RootPath + ". Scan reports are always enabled. LMB toggles without closing; RMB closes; CloseOnTRelease is configurable.");
			LogInfo("TDW DLL: " + Info.Location + "; assembly=" + typeof(Plugin).Assembly.GetName().Version?.ToString() + "; MVID=" + typeof(Plugin).Module.ModuleVersionId);
			LogInfo("TDW FEATURES: 功能图标=" + Options.FunctionIconScale.Value + "; 设备图标=" + Options.DeviceIconScale.Value + "; 手电爆闪选项=" + Options.EnableStrobe.Value + " / " + Options.StrobeFrequency.Value + " Hz; 动态测距=" + Options.EnableRangeReadout.Value + " / " + Options.RangeReadoutFrequency.Value + " Hz. F12 新增 6 项配置。");
			ServerAnnouncement.SendAsync(LogInfo, Warning);
		}
		catch (Exception e)
		{
			Fault(e);
		}
	}

	private void Update()
	{
		if (failed || Input == null)
		{
			return;
		}
		try
		{
			Input.Update();
			Strobes.Update();
			if (Time.unscaledTime >= modeCheck)
			{
				modeCheck = Time.unscaledTime + 1f;
				string mode = NativeSettings.Mode;
				if (mode != lastMode)
				{
					lastMode = mode;
					LogInfo("EFT TacticalInputMode=" + mode);
				}
			}
		}
		catch (Exception e)
		{
			// Transient failure only. This used to call Fault(), which after enough hits set
			// failed=true, nulled Input and ran Harmony.UnpatchSelf(), permanently disabling the
			// plugin. Update runs every frame, so a per-frame exception reached that point in well
			// under two seconds - which is why the wheel stopped opening after working once.
			TransientFault("Update", e);
		}
	}

	private void OnApplicationFocus(bool focus)
	{
		if (!focus)
		{
			Input?.Cancel(suppressUntilRelease: true);
			Strobes?.YieldToNative("focus lost");
		}
	}

	private void OnDestroy()
	{
		Strobes?.YieldToNative("plugin destroyed");
		Input?.Cancel(suppressUntilRelease: false);
		Harmony obj = harmony;
		if (obj != null)
		{
			obj.UnpatchSelf();
		}
		UI?.Dispose();
		Icons?.Dispose();
		if ((object)Instance == (object)this)
		{
			Instance = null;
		}
	}

	internal void Debug(string message)
	{
		if (Options.DebugLogging.Value)
		{
			Logger.LogInfo(message);
		}
	}

	internal void LogInfo(string message)
	{
		Logger.LogInfo(message);
	}

	internal void Warning(string message)
	{
		Logger.LogWarning(message);
	}

	internal void Error(string message)
	{
		Logger.LogError(message);
	}

	/// <summary>
	///     记录一次**非致命**的运行期异常，绝不因此禁用插件。
	///     <para>
	///         原版走的是 <see cref="Fault" />：累计到阈值就 <c>failed = true</c>、把
	///         <c>Input</c> 置空并 <c>Harmony.UnpatchSelf()</c>。而 <c>Update</c> 与两个
	///         Harmony 前缀都在每帧路径上，一个每帧都抛的异常会在两秒内达标 ——
	///         表现就是"轮盘呼出过一次之后再也呼不出来"，而且只在恰好触发那个异常时才发生。
	///     </para>
	///     <para>
	///         限流：每帧异常不做限流会把日志刷爆，所以前 5 次和之后每 100 次各记一条。
	///     </para>
	/// </summary>
	internal void TransientFault(string where, Exception e)
	{
		transientFaultCount++;
		if (transientFaultCount <= 5 || transientFaultCount % 100 == 0)
		{
			Logger.LogError("[TDW] " + where + " failed (non-fatal, #" + transientFaultCount + "): " + e);
		}
	}

	internal void Fault(Exception e)
	{
		// 0.4.1: 瞬时异常（UI/扫描）不再一次性永久熔断，避免一次偶发 NRE 让整个 mod 停摆。
		// 连续 50 次才硬禁用并卸载补丁，既保留兜底也避免日志刷屏。
		faultCount++;
		if (faultCount <= 3 || faultCount % 10 == 0)
		{
			Logger.LogError("TDW fault #" + faultCount + ": " + e);
		}
		try
		{
			Strobes?.YieldToNative("plugin fault");
			Input?.Cancel(suppressUntilRelease: false);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
		if (faultCount < 50 || failed)
		{
			return;
		}
		failed = true;
		Logger.LogError("TDW disabled after 50 faults.");
		try
		{
			Harmony obj = harmony;
			if (obj != null)
			{
				obj.UnpatchSelf();
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
		Input = null;
	}
}
