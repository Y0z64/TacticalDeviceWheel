using System.ComponentModel;
using BepInEx.Configuration;

namespace TacticalDeviceWheel;

internal sealed class Configuration
{
	public readonly ConfigEntry<bool> Enabled;

	public readonly ConfigEntry<bool> DebugLogging;
	public readonly ConfigEntry<bool> WriteScanReport;

	public readonly ConfigEntry<bool> GenericModes;

	public readonly ConfigEntry<bool> CloseOnTRelease;

	public readonly ConfigEntry<float> HoldThreshold;

	public readonly ConfigEntry<float> DeadZone;

	public readonly ConfigEntry<float> Sensitivity;

	public readonly ConfigEntry<float> Scale;

	public readonly ConfigEntry<float> FunctionIconScale;

	public readonly ConfigEntry<float> DeviceIconScale;

	public readonly ConfigEntry<float> StrobeFrequency;

	public readonly ConfigEntry<float> RangeReadoutFrequency;

	public readonly ConfigEntry<bool> EnableStrobe;

	public readonly ConfigEntry<bool> EnableRangeReadout;

	private static ConfigDescription Description(string category, string name, string chinese, AcceptableValueBase range = null)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		return new ConfigDescription(chinese, range, new object[2]
		{
			new DisplayNameAttribute(name),
			new CategoryAttribute(category)
		});
	}

	public Configuration(ConfigFile file)
	{
		Enabled = file.Bind<bool>("General", "Enabled", true, Description("常规", "启用 TDW", "仅在 EFT 战术设备设置为切换时启用轮盘；按住模式始终使用原版输入。"));
		DebugLogging = file.Bind<bool>("General", "DebugLogging", false, Description("常规", "调试日志", "输出详细输入、设备映射、图标和操作日志。扫描报告由“写入扫描报告”单独控制。"));
		WriteScanReport = file.Bind<bool>("General", "WriteScanReport", false, Description("常规", "写入扫描报告", "每次打开轮盘时把完整设备扫描结果写入 diagnostics/last-scan.json。默认关闭，避免每次开轮盘都产生磁盘写入。"));
		GenericModes = file.Bind<bool>("Compatibility", "ShowUnknownModes", true, Description("兼容性", "显示未映射模式", "未完成映射的设备按其实际功能显示模式扇区；关闭则隐藏这些设备。Mod 新增设备保持此兼容方式。"));
		CloseOnTRelease = file.Bind<bool>("Input", "CloseOnTRelease", true, Description("输入", "松开 T 关闭菜单", "开启：松开 T 关闭菜单，不执行选择。关闭：松开 T 后菜单继续保持。两种模式下左键切换后均不关闭，可连续操作；右键或 Esc 关闭。短按 T 保持原版行为。"));
		HoldThreshold = file.Bind<float>("Input", "HoldThresholdSeconds", 0.2f, Description("输入", "长按阈值（秒）", "长按 T 达到此时间后打开轮盘；0.20 秒即 200 毫秒。提前松开仅执行一次原版操作。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.05f, 1f)));
		DeadZone = file.Bind<float>("Input", "CenterDeadZone", 0.2f, Description("输入", "中心死区", "虚拟方向向量的初始死区（半径比例），防止刚打开时误选；回到中心保留最后一个有效选择。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.05f, 0.8f)));
		Sensitivity = file.Bind<float>("Input", "MouseSensitivity", 0.1f, Description("输入", "鼠标灵敏度", "鼠标移动到虚拟选择向量的倍率。数值越高，选择所需的鼠标位移越少。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.01f, 0.5f)));
		Scale = file.Bind<float>("UI", "Scale", 1f, Description("界面", "轮盘大小", "调整轮盘大小，并限制在屏幕范围内。下次打开轮盘时生效。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.6f, 1.4f)));
		FunctionIconScale = file.Bind<float>("UI", "FunctionIconScale", 1f, Description("界面", "功能图标大小", "白光、激光、补光、测距等功能图标的大小倍率；受扇区空间限制，下次打开生效。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.5f, 2f)));
		DeviceIconScale = file.Bind<float>("UI", "DeviceIconScale", 1f, Description("界面", "设备图标大小", "EFT 原版物品缩略图及其备用图标的大小倍率；与功能图标独立，受扇区空间限制，下次打开生效。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.5f, 2f)));
		EnableStrobe = file.Bind<bool>("Flashlight", "EnableStrobe", false, Description("手电爆闪", "显示手电爆闪选项", "给已识别白光功能的设备增加独立爆闪扇区。点击启动/停止，关闭轮盘后继续；关闭后轮盘不再显示爆闪扇区。不会自动启动。"));
		StrobeFrequency = file.Bind<float>("Flashlight", "StrobeFrequencyHz", 4f, Description("手电爆闪", "爆闪频率（赫兹）", "每秒完整亮灭循环次数；4 表示每秒亮灭 4 次。多个设备合并提交，每周期两次状态更新，不触发切换动画，不补执行卡帧期间错过的闪烁。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 8f)));
		EnableRangeReadout = file.Bind<bool>("Rangefinder", "ShowReadout", true, Description("测距读数", "显示动态测距读数", "在轮盘测距扇区及选中提示中显示原版测距仪当前读值。只在轮盘打开时读取缓存文本组件，不额外发射射线；关闭设备显示关闭，无目标显示无读数。"));
		RangeReadoutFrequency = file.Bind<float>("Rangefinder", "ReadoutRefreshHz", 4f, Description("测距读数", "读数刷新频率（赫兹）", "TDW 每秒读取原版测距显示的次数。不会提高原版实际测量频率（默认约每 0.5 秒测量一次）；仅文字变化时更新 UI。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 10f)));
	}
}
