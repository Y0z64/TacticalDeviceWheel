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

[BepInPlugin("com.xunhuaizhuo.tdw", "Tactical Device Wheel", "0.4.1")]
[BepInProcess("EscapeFromTarkov.exe")]
[BepInDependency("com.SPT.core")]
public sealed class Plugin : BaseUnityPlugin
{
	internal static Plugin Instance;

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
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Expected O, but got Unknown
		Instance = this;
		Options = new Configuration(Config);
		RootPath = Path.GetFullPath(Path.GetDirectoryName(Info.Location));
		try
		{
			if (AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "spt-core")?.GetName().Version != new System.Version(4, 1, 5, 0) || typeof(Player).Module.ModuleVersionId != new Guid("cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7"))
			{
				Warning("TDW disabled: requires the verified SPT 4.1.5 / EFT 40743 assembly build.");
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
			LogInfo("TDW 0.4.1 loaded. SPT 4.1.5 / EFT 0.16.9.40743. Native Hold bypass enabled.");
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
			Logger.LogInfo((object)message);
		}
	}

	internal void LogInfo(string message)
	{
		Logger.LogInfo((object)message);
	}

	internal void Warning(string message)
	{
		Logger.LogWarning((object)message);
	}

	internal void Error(string message)
	{
		Logger.LogError((object)message);
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
			Logger.LogError((object)("[TDW] " + where + " failed (non-fatal, #" + transientFaultCount + "): " + e));
		}
	}

	internal void Fault(Exception e)
	{
		// 0.4.1: 瞬时异常（UI/扫描）不再一次性永久熔断，避免一次偶发 NRE 让整个 mod 停摆。
		// 连续 50 次才硬禁用并卸载补丁，既保留兜底也避免日志刷屏。
		faultCount++;
		if (faultCount <= 3 || faultCount % 10 == 0)
		{
			Logger.LogError((object)("TDW fault #" + faultCount + ": " + e));
		}
		try
		{
			Strobes?.YieldToNative("plugin fault");
			Input?.Cancel(suppressUntilRelease: false);
		}
		catch (Exception ex)
		{
			Logger.LogError((object)ex);
		}
		if (faultCount < 50 || failed)
		{
			return;
		}
		failed = true;
		Logger.LogError((object)"TDW disabled after 50 faults.");
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
			Logger.LogError((object)ex);
		}
		Input = null;
	}
}
