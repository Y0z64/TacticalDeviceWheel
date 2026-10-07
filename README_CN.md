# Tactical Device Wheel (TDW) — SPT 4.1.5 移植版

[English](README.md) | **中文说明**

客户端 BepInEx 插件，为手中武器上已安装的战术设备提供轮盘菜单。**长按 T** 呼出轮盘，鼠标移向某个功能，
左键切换，右键关闭。

轮盘内容来自**当前武器上实际存在的设备实例**（运行时读取武器的 `TacticalComboVisualController` 实例
及其视觉模式节点），并把原生模式映射为白光、可见激光、IR 激光、IR 补光、测距、白光爆闪等功能。
同时包含多个功能的原生组合模式会被映射到对应功能上，而不是额外显示成一个"组合"条目。

| | |
|---|---|
| 目标版本 | **SPT 4.1.5 / EFT 0.16.9.40743** |
| 运行环境 | BepInEx 5.4.23.x，.NET Standard 2.1 |
| 端别 | 仅客户端（无服务端 mod） |

## 本仓库的来源与范围

本仓库是 **Tactical Device Wheel 0.4.0**（目标 SPT 4.0.13 / EFT 0.16.9.40087，作者 **xunhuaizhuo**，
最初发布于 ODDBA 社区）的移植版。原版发布时即声明可自由使用、修改、转载与整合，且不要求署名。

0.4.0 的程序集经 ILSpy 反编译后，其 EFT/SPT API 调用面被重新映射到 4.1.5 的程序集，再自源码重新编译。
反编译代码另外经过一轮审计，修复了若干缺陷；所有改动逐条列在下面。

本仓库**只包含源码**，不含原版分发包，也不含 0.4.0 随附的文档集。

## 安装

1. 把 `TacticalDeviceWheel` 整个文件夹放进 `<SPT>/BepInEx/plugins/`。
   `TacticalDeviceWheel.dll`、`devices.json` 与 `resources/` 必须保持在同一文件夹内——插件是相对自身
   所在目录解析这些文件的。
2. 启动游戏。配置会生成在 `BepInEx/config/com.xunhuaizhuo.tdw.cfg`（游戏内可用 ConfigurationManager / F12 修改）。

插件带有版本门闸：只有 `spt-core` 报告版本 `4.1.5.0`，且 `Assembly-CSharp` 的模块版本 id 为
`cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7` 时才会初始化。其它版本上只记录一条警告并保持停用状态，
因为插件读取的是私有/混淆成员，这些名称与布局在不同游戏构建之间会变化。

## 编译

```
dotnet build -c Release -p:GameDir="D:\SPT"
```

`GameDir` 指向 SPT 安装目录（含 `EscapeFromTarkov.exe` 的那一层），默认值是作者的路径。所有引用都取自
本机已安装的游戏/SPT 程序集，**不需要任何 NuGet 包**。构建输出（`dist/`）里已包含 DLL、`devices.json`
与 `resources/`，即一个可直接放进 `BepInEx/plugins/` 的文件夹。

源码使用了 C# 13 特性（`field` 关键字、`params ReadOnlySpan<T>`），因此需要支持 C# 13 的编译器：
.NET SDK 9+，或当前 Visual Studio 自带的 Roslyn。使用较旧的 SDK 会以 `CS0501` / `CS8652` 失败。
若要用 SDK 构建但替换为新版 Roslyn：

```
dotnet build -c Release -p:GameDir="D:\SPT" ^
  -p:CscToolPath="<VS>\MSBuild\Current\Bin\Roslyn" -p:CscToolExe=csc.exe -p:UseSharedCompilation=false
```

## 仓库结构

```
TacticalDeviceWheel.csproj     构建定义（以 GameDir 参数化）
devices.json                   设备/模式数据库，schema 2（25 条定义）
resources/icons/               运行时加载的 PNG + SVG 图标
src/
  TacticalDeviceWheel/         插件入口、配置、版本信息
  TacticalDeviceWheel.Core/    纯逻辑：轮盘数学、手势、原生模式映射、校验
  TacticalDeviceWheel.Compatibility/  唯一接触 EFT/SPT API 的层（反射、设置读取）
  TacticalDeviceWheel.Devices/ 设备扫描、图标、切换、爆闪、测距读数
  TacticalDeviceWheel.Input/   两个 Harmony 前缀 + 输入状态机
  TacticalDeviceWheel.UI/      轮盘绘制（扇区网格、Canvas、图标缓存）
```

## 0.4.1 变更

### API 移植：SPT 4.0.13 → SPT 4.1.5

| 0.4.0 使用的（4.0.13） | 4.1.5 的替代 |
|---|---|
| `GClass3379`（光模组件） | `EFT.InventoryLogic.LightComponent` |
| `GClass929`（物品图标） | `ItemIcon`（全局命名空间） |
| `GClass2348.Localized(string, string)` | `EFT.LocalizationExtensions.Localized(string, string)` |
| `GClass1673.SetMonospaceText(...)` | `EFT.StringExtensions.SetMonospaceText(...)` |
| `FirearmLightStateStruct` | `EFT.LightsState` |
| `EFT.FirearmController`（顶层类型） | `EFT.Player.FirearmController`（现在是嵌套类） |
| `FirearmController.weaponManagerClass` → `WeaponManagerClass.TacticalComboVisualController_0` | `FirearmController.Firearms._tacticalComboVisualControllers`（两者都是 public 字段，已去掉反射） |
| `TacticalComboVisualController.list_0`（私有模式节点列表） | `TacticalComboVisualController._ligthbeamsTransforms` |
| `PlayerOwner.method_13(ECommand)` | `PlayerOwner.TranslatePlayerInput(ECommand)`（见修复 1——该调用点已移除） |
| `SharedGameSettingsClass` + `GClass2389<…>` 强转 | `EFT.Settings.SettingsManager` → `Game.Controller.Group.TacticalInputMode` / `Control.Controller.Group.UserKeyBindings` |
| `ETranslateResult`（顶层） | `EFT.InputSystem.InputNode.ETranslateResult`（嵌套） |
| 版本门闸：`spt-core` 4.0.13 + 旧 `Assembly-CSharp` MVID | `spt-core` 4.1.5 + MVID `cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7` |

`[BepInDependency]` 的参数从程序集元数据中恢复（`com.SPT.core`）；反编译过程丢失了该参数。

### 修复

1. **轮盘完全不弹出**（`Input/InputController.cs:Valid`）。4.0.13 中该判定的最后一句是
   `PlayerOwner.method_13((ECommand)38)`，一个**只校验、无副作用**的方法。4.1.5 中名字对应的
   `PlayerOwner.TranslatePlayerInput(ECommand)` 是**静态方法且会真正执行命令**；当作判定使用时，
   `Valid()` 每帧都返回 false，于是轮盘永不触发。现已移除该调用，改为显式状态判断（本地玩家、存活、
   背包未打开、手持枪械、窗口聚焦、鼠标被锁定）。这同时消除了"每帧重复下发原生战术设备命令"的副作用。
2. **永久熔断**（`Plugin.Fault`）。任何未捕获异常——包括某一帧 UI 的偶发异常——都会置 `failed = true`
   并调用 `UnpatchSelf()`，使 mod 直到进程重启前都失效。现在改为计数：前三次与每第十次记录日志，
   **累计 50 次**才自我禁用并卸载 Harmony 补丁。
3. **空引用风险**（轮盘打开路径）：`TacticalCapability`（2 处）与 `RadialMenuUI` 中读取选中条目时
   直接解引用 `Device.Component` 而未判空；`StrobeController` 在存活判断之前就求值了
   `player.IsInventoryOpened`。
4. **鼠标捕获卡死**（`InputController`）。`mouseOwner` 只在轮盘关闭且不在排空时清空，而 `Cancel()`
   从不清它。若该对象被销毁（换枪、阵亡），插件会在此后整个会话里持续吞掉开火/ADS 命令。现在
   `Cancel()` 会清空它，赋值处也加了守卫。
5. **失效的校验**（`Core/DefinitionValidator.cs`）。一个由四个"取反的字符串比较"组成的条件恒为真，
   导致 `laserSpectrum` 的取值从未被校验。
6. **插件被 BepInEx 跳过**（`Plugin.cs`）。`BepInPlugin.Version` 是 `System.Version` 类型，带语义化
   后缀的字符串会让 BepInEx 5.4.23.5 直接丢弃该插件（`Skipping type [...] because its version is invalid`）。
   该属性现在使用纯数字版本（`0.4.1`），可读版本号保留在插件名与程序集元数据中。

### 性能 / 行为

- 新增配置项 `General / WriteScanReport`，**默认关闭**。开启时每次打开轮盘都会写出
  `diagnostics/last-scan.json`（完整的逐设备视觉证据）。此前这是每次打开都执行、且包含两次同步文件
  操作，是开轮盘路径上最大的一次停顿。
- 每次打开时的信息级日志（`SCAN device=`、`Capability mapping`、`Capability icon cached`、
  `Loading capability icon`、`Icon loaded successfully`、`Scan report saved`）改为受 `DebugLogging` 控制。
- `Flashlight / EnableStrobe` 默认值改为**关闭**，且只有在该项开启时才会给设备添加白光爆闪扇区。
  默认情况下轮盘里不存在爆闪条目，无法误选；若在爆闪进行中关闭该项，正在进行的爆闪会立即停止并恢复
  此前状态。
- 一次性诊断信息（受 `DebugLogging` 控制）：输入门槛的各项取值、每个 `ECommand` 首次出现、
  解析出的战术设备输入模式、以及战术按键绑定路径的完整转储。

## 本版本未处理的已知问题

以下问题来自对反编译 0.4.0 代码的审计，为保持行为一致，本版本**有意未改动**：

- 启用扫描报告时，`ScanDiagnostics` 仍会在每次扫描时序列化整份视觉快照；`ModeDiagnostics.ReadMode`
  会遍历每个模式节点的所有子组件，其中 `NodePaths`、`ComponentTypes`、`MaterialDetails` 只用于报告。
- `DeviceDatabase.ReloadIfChanged` 每次打开轮盘都会访问文件系统，且 `DefinitionValidator.TryBuild` 会在
  `Populate` 里对同一批定义再跑一次。
- `StrobeController.Find` 是对条目列表的线性查找，却被多个访问器分别调用。
- `IconManager` 会把加载失败的图标缓存为 `null`，之后每次查询都重试读盘，且没有内置兜底图标。
- `LaserObservation.Fingerprint` 与 `DeviceDatabase` 中 `schemaVersion == 1` 的迁移分支是死代码。
- 运行时视觉证据解析链（`ModeDiagnostics`、`ScanDiagnostics`、`VanillaModeResolver`、`X400ModeResolver`、
  `NativeLightRules`、`SpectrumMarkers`）依赖私有字段与混淆标识符，是下次游戏更新最容易失效的部分。
  若改为按模板 ID 显式列出模式表，可删掉约 800 行并消除全部反射。

## 配置项（首次运行生成）

| 分组 / 键 | 默认 | 说明 |
|---|---|---|
| General / Enabled | true | 总开关；仅在 EFT 战术设备操作设为「切换」时生效 |
| General / DebugLogging | false | 详细的输入、映射、图标与操作日志 |
| General / WriteScanReport | false | 每次打开轮盘写出 `diagnostics/last-scan.json` |
| Input / HoldThresholdSeconds | 0.2 | 长按多久后打开轮盘 |
| Input / CloseOnTRelease | true | 松开 T 即关闭；关闭此项则松 T 不关（T 不作为选择键） |
| Input / CenterDeadZone | 0.2 | 轮盘中心死区 |
| Input / MouseSensitivity | 0.1 | 鼠标到选择向量的增益 |
| UI / Scale、FunctionIconScale、DeviceIconScale | 1 | 轮盘与图标尺寸 |
| Flashlight / EnableStrobe、StrobeFrequencyHz | **false**、4 | 开启时才向轮盘添加白光爆闪扇区；关闭时不存在该扇区。频率仅在开启时生效 |
| Rangefinder / ShowReadout、ReadoutRefreshHz | true、4 | 轮盘内的实时测距读数 |
| Compatibility / ShowUnknownModes | true | 未映射设备以数字模式显示 |

## 鸣谢

- 原版设计与实现：**xunhuaizhuo**（Tactical Device Wheel 0.4.0，SPT 4.0.13）。
- SPT 4.1.5 移植、基于反编译的重建、审计与修复：本仓库。

## 许可证

MIT，见 [LICENSE](LICENSE)。上游署名见上；原版 0.4.0 发布时已声明可自由使用、修改、转载与整合，
且不要求署名。
