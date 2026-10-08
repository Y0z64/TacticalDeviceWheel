# Tactical Device Wheel (TDW) — SPT 4.1.x port

**English** | [中文说明](README_CN.md)

Client-side BepInEx plugin that adds a radial menu for the tactical devices mounted on the weapon in
hand. Hold **T**, move the mouse to a function, left-click to toggle, right-click to close.

The menu is built from the device instances actually present on the current weapon (it reads the
weapon's `TacticalComboVisualController` instances and their visual mode nodes at runtime), and maps
native modes to functions such as white light, visible laser, IR laser, IR illuminator, rangefinder and
white-light strobe. Modes that combine several functions are mapped to individual functions instead of
being shown as separate entries.

| | |
|---|---|
| Target | **SPT 4.1.x / EFT 0.16.9.40743** |
| Runtime | BepInEx 5.4.23.x, .NET Standard 2.1 |
| Side | Client only (no server mod) |

## Origin and scope of this repository

This is a port of **Tactical Device Wheel 0.4.0** (targeting SPT 4.0.13 / EFT 0.16.9.40087, author
xunhuaizhuo, originally distributed through the ODDBA community). The original is published
with permission to use, modify, redistribute and integrate, without requiring attribution.

The 0.4.0 API surface was remapped to the SPT 4.1 assemblies and rebuilt from source, and the result
was additionally audited: a number of defects were found and fixed. Every change is listed below.

This repository contains **source only**. It does not contain the original packages or the original
documentation set that shipped with 0.4.0.

## Install

1. Copy the `TacticalDeviceWheel` folder to `<SPT>/BepInEx/plugins/`.
   `TacticalDeviceWheel.dll`, `devices.json` and `resources/` must remain in the same folder — the
   plugin resolves them relative to its own location.
2. Start the game. Configuration is written to `BepInEx/config/com.xunhuaizhuo.tdw.cfg`
   (editable in game with ConfigurationManager / F12).

The plugin refuses to initialise unless the version gate matches: `spt-core` must report a **4.1.x**
version and `Assembly-CSharp` must have module version id
`cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7`. That id is the strict half of the check — it pins EFT
0.16.9.40743, the game build every SPT 4.1.x ships and therefore the one the patches were written
against. The SPT patch level is not checked, so a single build of this plugin works across 4.1.0
through 4.1.6 and later 4.1.x releases.

On any other game build it logs a warning and stays inactive, because the plugin reads
private game members whose names and layout change between game builds.

## Build

```
dotnet build -c Release -p:GameDir="D:\SPT"
```

`GameDir` is the SPT install directory (the one containing `EscapeFromTarkov.exe`); it defaults to the
author's path. All references are taken from the installed game/SPT assemblies — no NuGet packages are
needed. The build output (`dist/`) contains the DLL together with `devices.json` and `resources/`, i.e.
a drop-in folder for `BepInEx/plugins/`.

The sources use C# 13 features (`field` keyword, `params ReadOnlySpan<T>`), so a compiler with C# 13
support is required: .NET SDK 9+, or the Roslyn that ships with current Visual Studio versions. With an
older SDK the build fails with `CS0501` / `CS8652`. When using the SDK's own build with a newer Roslyn:

```
dotnet build -c Release -p:GameDir="D:\SPT" ^
  -p:CscToolPath="<VS>\MSBuild\Current\Bin\Roslyn" -p:CscToolExe=csc.exe -p:UseSharedCompilation=false
```

## Repository layout

```
TacticalDeviceWheel.csproj     build definition (parameterised by GameDir)
devices.json                   device/mode database, schema 2 (25 definitions)
resources/icons/               PNG + SVG icons loaded at runtime
src/
  TacticalDeviceWheel/         plugin entry point, configuration, release info
  TacticalDeviceWheel.Core/    pure logic: radial math, gestures, native mode maps, validation
  TacticalDeviceWheel.Compatibility/  the only layer that touches EFT/SPT APIs (reflection, settings)
  TacticalDeviceWheel.Devices/ device scan, icons, toggling, strobe, rangefinder readout
  TacticalDeviceWheel.Input/   two Harmony prefixes + the input state machine
  TacticalDeviceWheel.UI/      radial menu rendering (wedge meshes, canvas, icon cache)
```

## Changes in 0.4.3

Documentation only. No behaviour change, and no code change beyond the version number.

- **Corrected the "fix 5" entry under 0.4.1.** It claimed `DefinitionValidator` had a condition that was
  always true and left `laserSpectrum` unvalidated, and that 0.4.1 fixed it. That was wrong. Comparing
  this build against the 0.4.7 release shows both forms route "unrecognised spectrum" and "failed
  validation" to the same rejection, so the check always worked and `laserSpectrum` was always
  validated. The 0.4.1 rewrite changed how the condition reads, not what it does; the entry under
  0.4.1 now says so instead of claiming a defect that never existed.

## Changes in 0.4.2

- **The version gate now accepts the whole SPT 4.1 series instead of one patch release.** Every 4.1.x
  ships the same EFT 0.16.9.40743 game build, which is what the patches actually bind to, and that is
  still pinned strictly by the `Assembly-CSharp` module version id. Previously `spt-core` had to report
  exactly `4.1.5.0`, so the plugin refused to load on 4.1.0 through 4.1.4 and on 4.1.6.
- **`devices.json` grew from 25 to 37 device definitions.** The 12 added entries cover the TGC-NG custom
  tactical devices plus the Insight WMX200; without them `DeviceDatabase.Populate` returned false for
  those template ids and the devices fell back to numbered modes. The database check likewise accepts any
  4.1.x `sptVersion` now.
- The plugin log line reports the SPT version it actually found, and the server announcement says
  `targets SPT: 4.1.x`.

## Changes in 0.4.1

### API port: SPT 4.0.13 → SPT 4.1

| What had to be mapped | SPT 4.1 |
|---|---|
| light mod component | `EFT.InventoryLogic.LightComponent` |
| item icon | `ItemIcon` (global namespace) |
| localized-string helper | `EFT.LocalizationExtensions.Localized(string, string)` |
| monospace text helper | `EFT.StringExtensions.SetMonospaceText(...)` |
| light-state struct | `EFT.LightsState` |
| `FirearmController` (top level) | `EFT.Player.FirearmController` (now nested) |
| the weapon's tactical combo list | `FirearmController.Firearms._tacticalComboVisualControllers` (both public fields, reflection removed) |
| the mode-node list | `TacticalComboVisualController._ligthbeamsTransforms` |
| the "can this command be translated" predicate | `PlayerOwner.TranslatePlayerInput(ECommand)` (see fix 1 — the call site was removed) |
| game settings access | `EFT.Settings.SettingsManager` → `Game.Controller.Group.TacticalInputMode` / `Control.Controller.Group.UserKeyBindings` |
| translate-result enum | `EFT.InputSystem.InputNode.ETranslateResult` (nested) |
| version gate | `spt-core` 4.1.x + MVID `cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7` |

`[BepInDependency]` is declared explicitly (`com.SPT.core`); the published 0.4.0 package did not carry
that metadata.

### Fixes

1. **Radial menu never opened** (`Input/TacticalController.cs:Valid`). In 4.0.13 the predicate ended with
   a side-effect-free "can this command be translated" check. The method carrying the same name in 4.1,
   `PlayerOwner.TranslatePlayerInput(ECommand)`, is **static and executes the command**; used as a
   predicate it made `Valid()` false on every frame, so the menu never
   triggered. The call was removed and replaced by the explicit state checks (local player, alive,
   inventory closed, firearm hands controller, focused, cursor locked). This also removes the per-frame
   side effect of re-issuing the native tactical command.
2. **Permanent fault latch** (`Plugin.Fault`). Any unhandled exception — including a transient one from a
   single UI frame — set `failed = true` and called `UnpatchSelf()`, disabling the mod until the process
   restarted. Faults are now counted: the first three and every tenth are logged, and the mod only
   disables itself (and unhooks Harmony) after 50 faults.
3. **Null-reference hazards** on the menu-open path: `TacticalCapability` (2 sites) and the selected-entry
   read in `RadialMenuUI` dereferenced `Device.Component` without a check; `StrobeController` evaluated
   `player.IsInventoryOpened` before its aliveness guard.
4. **Stuck mouse capture** (`InputController`). `mouseOwner` was only cleared while the menu was closed
   and not draining, and `Cancel()` never cleared it. If the owner object was destroyed (weapon change,
   death) the plugin kept consuming the fire/ADS commands for the rest of the session. `Cancel()` now
   clears it and the assignment is guarded.
5. **`DefinitionValidator` conditions rewritten** (`Core/DefinitionValidator.cs`). The `laserSpectrum`
   check was written as four negated comparisons with the two branches swapped, and the
   `minimumModes` value came from a negated ternary. Both are now stated positively.

   **This is a readability change only.** The two forms are equivalent and validation behaves exactly
   as it did before. An earlier revision of this document described it as fixing a condition that was
   "always true" and left `laserSpectrum` unvalidated; comparing this build against the 0.4.7 release
   shows that was wrong and the check always worked. See "Changes in 0.4.3" below.
6. **Plugin was skipped by BepInEx** (`Plugin.cs`). `BepInPlugin.Version` is a `System.Version`, so a
   semver-suffixed string makes BepInEx 5.4.23.5 drop the plugin (`Skipping type [...] because its
   version is invalid`). The attribute now carries a numeric version (`0.4.1`); the readable version is
   kept in the plugin name and assembly metadata.

### Performance / behaviour

- New configuration entry `General / WriteScanReport`, **default off**. When enabled, every wheel open
  writes `diagnostics/last-scan.json` (full visual evidence per device). Previously this ran on every
  open and performed two synchronous file operations — the largest single stall on the open path.
- Per-open informational logs (`SCAN device=`, `Capability mapping`, `Capability icon cached`,
  `Loading capability icon`, `Icon loaded successfully`, `Scan report saved`) now go through
  `DebugLogging` instead of always being printed.
- `Flashlight / EnableStrobe` now defaults to **off**, and the white-light strobe sector is only added to
  the wheel while that option is enabled. With the default, no strobe entry exists and cannot be selected by
  accident; if it is switched off while a strobe is running, the running strobe is stopped and its previous
  state restored.
- One-shot diagnostics behind `DebugLogging`: input gate results, the first occurrence of each
  `ECommand`, the resolved tactical input mode, and a dump of the tactical key binding path.

## Known issues not addressed in this release

Carried over from the audit, deliberately left unchanged to keep behaviour identical:

- `ScanDiagnostics` still serialises the whole visual snapshot on every scan when enabled, and
  `ModeDiagnostics.ReadMode` walks every child component of each mode node, including fields
  (`NodePaths`, `ComponentTypes`, `MaterialDetails`) that only feed the report.
- `DeviceDatabase.ReloadIfChanged` touches the file system on every open and `DefinitionValidator.TryBuild`
  runs again inside `Populate` for the same definitions.
- `StrobeController.Find` is a linear search over the entry list and is called from several accessors.
- `IconManager` caches `null` for a failed icon lookup and re-attempts the file read on each subsequent
  query; there is no built-in fallback sprite.
- `LaserObservation.Fingerprint` and the `schemaVersion == 1` migration branch in `DeviceDatabase` are
  dead code.
- The runtime-visual-evidence resolution (`ModeDiagnostics`, `ScanDiagnostics`, `VanillaModeResolver`,
  `X400ModeResolver`, `NativeLightRules`, `SpectrumMarkers`) depends on private game fields, so it is
  the part most likely to break on the next game build. Replacing it with explicit
  per-template mode tables would remove ~800 lines and all reflection.

## Configuration (generated at first run)

| Section / key | Default | Meaning |
|---|---|---|
| General / Enabled | true | Master switch; only active while EFT's tactical device input is set to *toggle* |
| General / DebugLogging | false | Verbose input, mapping, icon and operation logs |
| General / WriteScanReport | false | Write `diagnostics/last-scan.json` on every wheel open |
| Input / HoldThresholdSeconds | 0.2 | Hold time before the wheel opens |
| Input / CloseOnTRelease | true | Close the wheel when T is released; off keeps it open (T never selects) |
| Input / CenterDeadZone | 0.2 | Radial dead zone |
| Input / MouseSensitivity | 0.1 | Mouse-to-selection gain |
| UI / Scale, FunctionIconScale, DeviceIconScale | 1 | Wheel and icon sizing |
| Flashlight / EnableStrobe, StrobeFrequencyHz | **false**, 4 | While enabled, adds the white-light strobe sector to the wheel; while disabled the sector is not created. Rate applies when enabled |
| Rangefinder / ShowReadout, ReadoutRefreshHz | true, 4 | Live rangefinder readout in the wheel |
| Compatibility / ShowUnknownModes | true | Show unmapped devices as numbered modes |

## Credits

- Original mod and design: **xunhuaizhuo** (Tactical Device Wheel 0.4.0, SPT 4.0.13).
- SPT 4.1.x port, audit and fixes: this repository.

## License

MIT — see [LICENSE](LICENSE). Upstream credit is given above; the original 0.4.0 release was published with
permission to use, modify, redistribute and integrate without requiring attribution.
