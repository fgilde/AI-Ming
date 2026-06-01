---
title: Config File
parent: Configuration
nav_order: 4
---

# The Config File

PowerAim persists every setting to a JSON config file. This page documents its location and shape so you can:

- Back up your config
- Edit it manually
- Share it with other users
- Build a tool that generates configs programmatically

## Location

```
<install dir>\bin\configs\Default.cfg              # default config (path to it stored in:)
%AppData%\AI-M\LastConfigPath.cfg                  # ASCII text — full path to the active config
```

The format is the standard `System.Text.Json` JSON representation of `AppConfig`. Pretty-printed, UTF-8, no BOM.

## Top-level structure

```jsonc
{
  "LastLoadedModel": "Universal.onnx",
  "LastLoadedConfig": "C:\\...\\Default.cfg",
  "ThemeName": "Purple",
  "ActiveThemeName": "Green",
  "ThemeMode": "System",

  "BindingSettings": { ... },          // global keybinds
  "SliderSettings":  { ... },          // every slider value
  "ToggleState":     { ... },          // every toggle on/off
  "MinimizeState":   { ... },          // collapse states of cards
  "DropdownState":   { ... },          // every dropdown selection
  "ColorState":      { ... },          // colors (FOV, ESP boxes, etc.)
  "AntiRecoilSettings": { ... },       // anti-recoil incl. patterns
  "FileLocationState": { ... },        // gun-config file paths
  "OllamaSettings":  { ... },          // AutoPlay LLM connection
  "AISettings":      { ... },          // multi-class filter + sticky aim + detection masks
  "ActiveProcessSettings": { ... },    // auto-pause + auto-switch
  "CrosshairSettings": { ... },        // custom crosshair shape/size/color
  "OcrSettings":     { ... },          // HUD OCR regions
  "ReplaySettings":  { ... },          // replay buffer
  "AutoPlayLearningSettings": { ... },
  "LayoutConfiguration": { ... },      // sidebar / card layout customizations

  "Triggers":      [ /* ActionTrigger objects */ ],
  "AutoPlayProfiles": [ /* AutoPlayProfile objects */ ],
  "ControllerMappingProfiles": [ /* ControllerMappingProfile objects */ ],

  "MappingDirection": "Both",
  "Language": "en-US",
  "CaptureSource":  { ... }
}
```

## Key sub-objects

### SliderSettings

Holds every numeric tuning parameter — image size, FOV size, mouse sensitivity, jitter, EMA, Y/X offsets, sticky-aim thresholds, magnifier dimensions, etc. See [Settings Overview]({{ '/configuration/settings-overview' | relative_url }}) for the list.

### ToggleState

Holds every boolean. Notably:

- `GlobalActive` — master kill switch
- `AimAssist`, `AutoTrigger`, `AntiRecoil`, `FOV`, etc.
- `MappingActive` — Controller Mapping master toggle
- `HideUIFromCapture` — Win32 capture exclusion

`UseControllerForAim` no longer exists as a runtime toggle — its successor is `DropdownState.MouseMovementMethod == Gamepad`. The legacy field persists for backward-compat: on first load, a `true` value is migrated into the Gamepad movement method and the bool is cleared. See [Movement Methods]({{ '/features/mouse-input-methods' | relative_url }}).

### BindingSettings

```jsonc
"BindingSettings": {
  "AimKeyBindings": [...],
  "DynamicFOVKeybind": {...},
  "MagnifierKeybind": {...},
  "MagnifierZoomInKeybind": {...},
  "MagnifierZoomOutKeybind": {...},
  "ModelSwitchKeybind": {...},
  "AntiRecoilKeybind": {...},
  "DisableAntiRecoilKeybind": {...},
  "Gun1Key": {...},
  "Gun2Key": {...}
}
```

Each `StoredInputBinding` is `{ "Kind": "Keyboard|Mouse|GamepadButton|GamepadSlider", "Code": <int> }` where the code is the platform-specific virtual key / mouse button / gamepad button.

### Triggers

An array of `ActionTrigger`:

```jsonc
{
  "Id": "abcdef123",
  "Name": "Primary Fire",
  "Enabled": true,
  "ChargeMode": false,
  "NeedsDetection": true,
  "TriggerKeys": [{ "Kind": "Mouse", "Code": 1 }],
  "TriggerKeysOperator": "Or",
  "AntiTriggerKeys": [],
  "AntiTriggerKeysOperator": "Or",
  "Actions": [{ "Kind": "Mouse", "Code": 1 }],
  "ExecutionMode": "Simultaneous",
  "Delay": 0,
  "BreakTime": 0,
  "BeginIntersectionCheck": "None",
  "BeginIntersectionArea": {...},
  "ExecutionIntersectionCheck": "HeadIntersectingCenter",
  "ExecutionIntersectionArea": {...},
  "MatchProcess": ""
}
```

### ControllerMappingProfiles

An array of `ControllerMappingProfile`:

```jsonc
{
  "Id": "fedcba321",
  "Name": "FPS — Both",
  "Enabled": true,
  "MatchProcess": "",
  "Mappings": [
    {
      "SourceKind": "KeyboardKey",
      "SourceCode": 87,                 // Keys.W
      "TargetKind": "GamepadStickDirection",
      "TargetCode": 0,                  // LeftStickUp
      "Enabled": true,
      "Activator": "Press",
      "LongPressMs": 350,
      "ModifierKind": "None",
      "ModifierCode": 0
    },
    ...
  ],
  "StickToMouseSensitivity": 12.0,
  "MouseToStickSensitivity": 1.0,
  "StickDeadzone": 0.15,
  "StickAntiDeadzone": 0.0,
  "StickResponseCurve": 1.0,
  "StickMouseExponent": 1.4,
  "InvertMouseY": false
}
```

### AutoPlayProfiles

```jsonc
{
  "Id": "...",
  "Name": "FPS Default",
  "Enabled": false,
  "UseOllama": true,
  "OllamaModel": "moondream",
  "DecisionInterval": 0.5,
  "GameContext": "First-person shooter game...",
  "MouseSensScale": 1.0,
  "MouseJitterPx": 0,
  "KeyDelayJitterMs": 0,
  "MatchProcess": "",
  "Actions": [
    {
      "Name": "move_forward",
      "Description": "Move forward — use when path is clear",
      "Keys": [{ "Kind": "Keyboard", "Code": 87 }, { "Kind": "GamepadButton", "Code": 0 }],
      "ActionType": "Continuous"
    },
    ...
  ]
}
```

`UseOllama = false` skips the strategic LLM layer entirely (no HTTP polling, no screenshot capture for the LLM). `MouseSensScale`, `MouseJitterPx` and `KeyDelayJitterMs` are per-profile anti-detection / per-game tuning knobs — see [AutoPlay]({{ '/features/autoplay' | relative_url }}).

### AntiRecoilSettings

```jsonc
"AntiRecoilSettings": {
  "SchemaVersion": 1,
  "ActiveProfileId": "abc123…",
  "Profiles": [
    {
      "Id": "abc123…",
      "Name": "AK-47",
      "Mode": "PatternPlayback",     // Legacy | ImageBased | PatternPlayback
      "MatchProcess": "",
      "OcrRegionName": "weapon",
      "WeaponMatch": "AK",
      "AutoSwitchOnOcr": true,
      "HoldTime": 10, "FireRate": 200, "YRecoil": 10, "XRecoil": 0,
      "AutoStrength": 0.85,
      "PatternName": "AK-47",
      "PatternStrength": 1.0
    }
  ],
  "Patterns": [ /* RecoilPattern entries — the shared library */ ],

  // Legacy fields preserved for migration only — write-once, then ignored.
  "HoldTime": 10, "FireRate": 200, "YRecoil": 10, "XRecoil": 0,
  "AutoStrength": 0.85,
  "UseImageBasedAntiRecoil": false,
  "ActivePatternName": "", "PatternStrength": 1.0,
  "UsePatternRecoil": false
}
```

`SchemaVersion = 0` triggers a one-shot migration on load: a single profile is seeded from the legacy fields (`UseImageBasedAntiRecoil` / `UsePatternRecoil` / `ActivePatternName` / `HoldTime` / `XRecoil` / `YRecoil` / `AutoStrength` / `PatternStrength`) so old configs keep their behaviour. The version is then bumped to `1` and the migration is never run again. See [Anti-Recoil]({{ '/features/anti-recoil' | relative_url }}).

### AISettings

Multi-class filter, sticky aim, detection masks — plus:

- `InferenceGpuDeviceId` (int, default `0`) — DXGI adapter index for ONNX inference. The title-bar GPU picker writes this. On multi-GPU systems, set it to a secondary card to keep inference off the GPU running the game. Setting it reloads the active model.

### CrosshairSettings

Standard shape / size / colour fields, plus:

- `DetectionFlashEnabled` (bool, default `false`) — tint the crosshair when the current frame has any detections.
- `DetectionFlashColorHex` (ARGB hex, default `#FFFF3030`).
- `DetectionFlashMs` (int, 50–1000, default `200`).

## Quick-config menu

The cogwheel button in PowerAim's title bar offers:

- **Save as Quick Config** — overwrites the currently loaded config
- **Save Config as...** — file-save dialog, choose a new filename
- **Open Quick Config** — re-load the currently loaded config (discards unsaved changes)
- **Open Other Config** — file-open dialog, pick another `.cfg`

After loading, the entire UI rebuilds with the new values.

## Manual editing

PowerAim **must be closed** before editing the file — otherwise the in-memory config will overwrite your changes on exit.

Steps:

1. Close PowerAim
2. Open `<install dir>\bin\configs\Default.cfg` in your text editor
3. Edit any field. Make sure JSON stays valid (commas, quotes, braces).
4. Save
5. Launch PowerAim — it loads from your edited file

If PowerAim refuses to load a config, it falls back to defaults and writes a console message. Check the in-app log (sidebar → Logs).

## Shared configs

The **Models & Configs** page → "Configs" segmented toggle pulls community-shared `.cfg` files from the same two repos as models. Click to download; PowerAim drops them in `bin\configs\`. The cogwheel menu picks them up automatically.
