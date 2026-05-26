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
- `UseControllerForAim` — drive virtual right stick instead of mouse
- `HideUIFromCapture` — Win32 capture exclusion

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
  "OllamaModel": "moondream",
  "DecisionInterval": 0.3,
  "GameContext": "First-person shooter game...",
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
