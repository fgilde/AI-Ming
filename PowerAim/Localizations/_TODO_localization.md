# Hardcoded strings to localize

Survey of `PowerAim/` for user-visible English literals that are not yet routed through
`Locale.X` / the `localeConverter` (XAML `ConverterParameter`).

Conventions:
- "REUSE" indicates the literal already has an identical English text in `Localizations/Locale.json` — use the existing key instead of a new one.
- Helper text strings that act as a label + hint are usually a candidate for two keys (label + tooltip).
- Format-with-placeholder strings (`"{0}"`) are kept literal — only the surrounding English needs a key.

## By file

### Visuality/CalibrationWizardDialog.xaml
- Line 8 `Title="Sensitivity Calibration"` → `SensitivityCalibration`
- Line 38 `Text="Sensitivity Calibration Wizard"` → `SensitivityCalibrationWizard`
- Line 74 `Text="Calibrate to your game"` → `CalibrateToYourGame`
- Line 85 long help `Text="PowerAim measures how many screen pixels…"` → `CalibrationIntroBody`
- Line 92 `Run … Text="Before you start:"` → `CalibrationBeforeYouStart`
- Lines 94–100 four bullet lines (combine into one localised paragraph) → `CalibrationBulletList`
- Line 107 `Text="Tip: also disable raw input acceleration…"` → `CalibrationTip`
- Line 116 `Text="Calibrating…"` → `CalibrationRunning`
- Line 122 `Text="Don't move the mouse"` → `DontMoveMouse`
- Line 140 `Text="Result"` → `Result`
- Line 157 `Text="Measured pixels / input"` → `MeasuredPixelsPerInput`
- Line 169 `Text="Samples used"` → `SamplesUsed`
- Line 181 `Text="Current MouseSensitivity"` → `CurrentMouseSensitivity`
- Line 193 `Text="Suggested MouseSensitivity"` → `SuggestedMouseSensitivity`
- Line 221 `Text="Calibration failed"` → `CalibrationFailed`
- Line 246 `Content="Cancel"` → REUSE `Cancel`
- Line 251 `Content="Start calibration"` → `StartCalibration`

### Visuality/CalibrationWizardDialog.xaml.cs
- Line 43 `LeftButton.Content = "Cancel"` → REUSE `Cancel`
- Line 44 `RightButton.Content = "Start calibration"` → `StartCalibration`
- Line 49 `RightButton.Content = "Running…"` → `Running`
- Line 55 `LeftButton.Content = "Close"` → REUSE `Close`
- Line 56 `RightButton.Content = "Apply suggested"` → `ApplySuggested`
- Line 62 `RightButton.Content = "Try again"` → REUSE `TryAgain`
- Line 107 `ShowError("No capture source available…")` → `NoCaptureSourceLoaded`
- Line 131 `ShowError("Cancelled.")` → `Cancelled`
- Line 135 `ShowError(result.ErrorMessage ?? "Unknown error.")` → `UnknownError`
- Lines 171–175 interpretation text switch (5 strings) → `CalibrationInterpretLow`, `CalibrationInterpret1to1`, `CalibrationInterpretSlightHigh`, `CalibrationInterpretHigh`, `CalibrationInterpretVeryHigh`

### Visuality/OcrRegionsDialog.xaml
- Line 8 `Title="HUD OCR Regions"` → `HudOcrRegions`
- Line 38 `Text="HUD OCR Regions"` → `HudOcrRegions`
- Line 75 long help `Text="Coordinates are in absolute screen pixels…"` → `OcrCoordinatesHelp`
- Line 102 `Content="Add region"` → `AddRegion`
- Line 108 `Content="Delete"` → REUSE `Delete`
- Line 124 `Text="No region selected"` → `NoRegionSelected`
- Line 138 `Text="Name"` → REUSE `Name`
- Line 144 `Text="Kind"` → `Kind`
- Line 148 `ComboBoxItem Content="Text"` → `OcrKindText`
- Line 149 `ComboBoxItem Content="Number"` → `OcrKindNumber`
- Line 150 `ComboBoxItem Content="Health"` → `OcrKindHealth`
- Line 161 `Text="Screen rectangle"` → `ScreenRectangle`
- Line 166 `Content="Pick on screen…"` → `PickOnScreen`
- Line 212 `Text="Binarize threshold"` → `BinarizeThreshold`
- Line 227 `Content="Invert (white text on dark HUD)"` → `OcrInvertCheckbox`
- Line 234 `Text="Live preview"` → `LivePreview`
- Line 263 `Content="OCR engine running"` → `OcrEngineRunning`
- Line 273 `Content="Download Tesseract data"` → `DownloadTesseractData`
- Line 278 `Content="Test engine"` → `TestEngine`
- Line 282 long ToolTip `"Synchronously instantiate the Tesseract engine…"` → `TestEngineTooltip`
- Line 284 `Content="Close"` → REUSE `Close`

### Visuality/OcrRegionsDialog.xaml.cs
- Line 156 `DetailHeader.Text = "No region selected"` → `NoRegionSelected`
- Line 168 `DetailHeader.Text = $"Editing: {_selected.Name}"` → `EditingItemFormat` ("Editing: {0}")
- Line 254 `PreviewText.Text = "(empty)"` → `OcrEmpty`
- Line 255 `ConfidenceText.Text = $"Confidence: {result.Confidence:0.00} · {result.Timestamp:HH:mm:ss}"` → `OcrConfidenceFormat`
- Line 260 `ConfidenceText.Text = "No reading yet — enable the engine and wait one polling cycle."` → `OcrNoReadingYet`
- Line 270 `Name = $"Region {settings.Regions.Count + 1}"` → `OcrRegionDefaultNameFormat` ("Region {0}")
- Line 323 `StatusLine.Text = "Downloading eng.traineddata…"` → `OcrDownloadingTraineddata`
- Line 325 `StatusLine.Text = $"Downloading eng.traineddata… {(int)(v * 100)}%"` → `OcrDownloadingTraineddataProgressFormat`
- Line 327 `$"eng.traineddata installed in {OcrService.ResolveTessdataPath()}"` → `OcrTraineddataInstalledFormat`
- Line 328 `"Download failed — drop eng.traineddata into the tessdata folder manually."` → `OcrDownloadFailed`
- Line 351 `"eng.traineddata is missing — use 'Download Tesseract data'."` → `OcrTraineddataMissing`
- Line 352 `"Engine paused."` → `OcrEnginePaused`
- Line 353 `"Engine running."` → `OcrEngineRunningStatus`

### Visuality/RecoilPatternsDialog.xaml
- Line 8 `Title="Recoil Patterns"` → `RecoilPatterns`
- Line 38 `Text="Recoil Patterns"` → `RecoilPatterns`
- Line 75 long help `Text="Record a 2-second pattern…"` → `RecoilPatternsHelp`
- Line 104 `Content="Record (2s)"` → `RecordPattern2s`
- Line 110 `Content="Delete selected"` → `DeleteSelected`
- Line 139 `Text="Name"` → REUSE `Name`
- Line 146 `Text="Weapon"` → `Weapon`
- Line 156 `Text="No pattern selected."` → `NoPatternSelected`
- Line 180 `Content="Use pattern playback while firing"` → `UsePatternPlayback`
- Line 192 `Text="Pattern strength"` → `PatternStrength`
- Line 222 `Content="Close"` → REUSE `Close`

### Visuality/RecoilPatternsDialog.xaml.cs
- Line 147 `DurationText.Text = "No pattern selected."` → `NoPatternSelected`
- Line 154 `DurationText.Text = $"{_selected.Samples.Count} samples · {_selected.DurationMs} ms total"` → `PatternSamplesDurationFormat`
- Line 252 `Text = "Accent: Y drift · Faint: X drift"` → `RecoilPreviewLegend`
- Line 272 `StatusText.Text = "No capture source — load a model first."` → `NoCaptureSourceLoadModelFirst`
- Line 285 `RecordButton.Content = "Recording… (click to cancel)"` → `RecordingClickToCancel`
- Line 286 `StatusText.Text = "Fire the weapon — recording for 2 seconds."` → `RecordingFireWeapon`
- Line 289 `$"Fire the weapon — {(int)(v * 100)}% recorded."` → `RecordingProgressFormat`
- Line 294 `Name = "(recording)"` → `Recording`
- Line 305 `var name = $"Pattern {…}"` → `PatternDefaultNameFormat` ("Pattern {0}")
- Line 317 `StatusText.Text = "No motion detected — aim at a textured surface and try again."` → `RecordingNoMotion`
- Line 324 `StatusText.Text = $"Recorded {pattern.Samples.Count} samples over {pattern.DurationMs} ms."` → `RecordingDoneFormat`
- Line 329 `StatusText.Text = $"Recording failed: {ex.Message}"` → `RecordingFailedFormat`
- Line 340 `RecordButton.Content = "Record (2s)"` → `RecordPattern2s`

### Visuality/DetectionMasksDialog.xaml
- Line 8 `Title="Detection Region Masks"` → `DetectionRegionMasks`
- Line 40 `Text="Detection Region Masks"` → `DetectionRegionMasks`
- Line 78 long help `Text="Draw rectangles inside the detection area…"` → `DetectionMasksHelp`
- Line 107 `Content="Add mask"` → `AddMask`
- Line 112 `Content="Clear all"` → `ClearAll`
- Line 156 `Text="(drag inside to draw / move selected mask)"` → `MasksDragHint`
- Line 170 `Text="No mask selected"` → `NoMaskSelected`
- Line 187 `Text="X"` → leave (single letter axis label, not localisable) — SKIP
- Line 190 `Text="Y"` → SKIP
- Line 201 `Text="Width"` → REUSE `Width`
- Line 204 `Text="Height"` → REUSE `Height`
- Line 233 `Text="Predictions whose centre lands inside an enabled mask…"` → `MasksFooterHelp`
- Line 234 `Content="Cancel"` → REUSE `Cancel`
- Line 239 `Content="Save"` → REUSE `Save`

### Visuality/DetectionMasksDialog.xaml.cs
- Line 177 `SelectedHeader.Text = "No mask selected"` → `NoMaskSelected`
- Line 183 `SelectedHeader.Text = $"Editing: {_selected.Name}"` → `EditingItemFormat`
- Line 256, 330 `Name = $"Mask {_working.Count + 1}"` → `MaskDefaultNameFormat` ("Mask {0}")

### Visuality/MappingPage.xaml
- Line 16 `Text="Controller ↔ Keyboard Mapping"` → `MappingTitle`
- Line 21 long body `Text="Make games respond to whichever input device…"` → `MappingIntro`
- Line 42 `Text="Mapping profiles"` → `MappingProfiles`
- Line 47 `Text="One active profile at a time — the engine picks…"` → `MappingProfilesHelp`

### Visuality/MappingPage.xaml.cs
- Line 64 `"Mapping active"` → `MappingActive`
- Line 157 `var active = engine.ActiveProfile?.Name ?? "(none)"` → `NoneParen` ("(none)")
- Line 162 `"  —  Mapping active is OFF. Flip the master toggle above to activate the profile."` → `MappingActiveOffHint`
- Line 164 `"  —  No profile is enabled. Toggle one in the list below."` → `NoMappingProfileEnabledHint`
- Line 165 `EngineStatusText.Text = $"Engine: {engine.Status} · Active profile: {active}{hint}"` → `MappingEngineStatusFormat`

### UILibrary/MappingEdit.xaml
- Line 54 `Text="Profile"` → `Profile`
- Line 55 `Text="Name and active state"` → REUSE `TriggerSubtitle`
- Line 59 `Text="Enabled"` → REUSE `Enabled`
- Line 64 `Tag="Profile name"` → `ProfileName`
- Line 81 `Text="Match process (optional)"` → `MatchProcessOptional`
- Line 82 `Text="Only activate while a process matching the pattern is focused"` → `MatchProcessActivateHint`
- Line 86 `Tag="cs2*, valorant, *fortnite*"` → leave (example pattern, technical) — SKIP
- Line 91 `Text="Supports wildcards (*, ?) and pipes (cs2|valorant). Leave empty for always-on."` → `MatchProcessWildcardHelp`
- Line 110 `Text="Mappings"` → `Mappings`
- Line 112 `Text="Click a Source / Target slot, then press the key…"` → `MappingsSubtitle`
- Line 115 `Content="+ Add"` → `AddRow`
- Line 118 ToolTip `"Add an empty mapping row. Click each side to record a key / button."` → `AddRowTooltip`
- Line 121 `Content="+ Stick → Mouse"` → `AddStickToMouse`
- Line 124 ToolTip `"Adds the special right-stick → mouse-motion mapping."` → `AddStickToMouseTooltip`
- Line 127 `Content="+ Mouse → Stick"` → `AddMouseToStick`
- Line 129 ToolTip `"Adds the special mouse-motion → right-stick mapping."` → `AddMouseToStickTooltip`
- Line 145 `Text="SOURCE"` → `SourceHeader`
- Line 158 `Text="TARGET"` → `TargetHeader`
- Line 165 `Text="ACTIVATOR"` → `ActivatorHeader`
- Line 177 `Text="No mappings yet. Click '+ Add' above, then click each slot to record a key or controller button."` → `NoMappingsYet`
- Line 193 `Text="Stick & Motion Tuning"` → `StickMotionTuning`
- Line 194 `Text="Dead-zone, response curve and sensitivities for stick ↔ mouse"` → `StickTuningSubtitle`

### UILibrary/MappingEdit.xaml.cs
- Line 86 `AddSlider("Mouse → Stick sensitivity", "× scale", …)` → `MouseToStickSensitivity` + unit `MultiplierUnit`
- Line 88 `AddSlider("Stick → Mouse sensitivity", "px per tick", …)` → `StickToMouseSensitivity` + unit `PxPerTickUnit`
- Line 90 `AddSlider("Dead-zone", "× full deflection", …)` → `DeadZone` + unit `FullDeflectionUnit`
- Line 92 `AddSlider("Anti-dead-zone", "× full deflection", …)` → `AntiDeadZone`
- Line 94 `AddSlider("Mouse response curve", "exponent", …)` → `MouseResponseCurve` + unit `ExponentUnit`
- Line 99 `Content = "Invert Y axis (stick ↔ mouse)"` → `InvertYAxis`
- Line 193 ToolTip `"Press style: Press = while held, LongPress = after holding, …"` → `MappingActivatorTooltip`
- Line 211 `ToolTip = "Remove this mapping"` → `RemoveMapping`
- Line 274 `ToolTip = "Switch back to recording a normal key / button / trigger."` → `RecordNormalTooltip`
- Line 332 `ToolTip = "Pick a stick direction or mouse-motion target (things the recorder can't capture)."` → `SpecialEndpointTooltip`
- Line 362–372 menu items "🎮 Left Stick — Up/Down/Left/Right", "🎮 Right Stick — …", "🖱 Mouse motion (relative)" → `MenuLeftStickUp`, `MenuLeftStickDown`, `MenuLeftStickLeft`, `MenuLeftStickRight`, `MenuRightStickUp`, `MenuRightStickDown`, `MenuRightStickLeft`, `MenuRightStickRight`, `MenuMouseMotionRelative`

### UILibrary/MappingProfileList.xaml
- Line 39 `Text="{Binding MatchProcess, StringFormat=Match: {0}}"` → `MatchPrefix` ("Match: {0}")
- Line 53 `ToolTip="Edit"` → REUSE `Edit`
- Line 60 `ToolTip="Duplicate"` → `Duplicate`
- Line 67 `ToolTip="Delete"` → REUSE `Delete`
- Line 86 `ToolTip="Create a new profile — empty or pre-populated from a preset."` → `NewProfileTooltip`
- Line 88 `Text="+ New profile"` → `NewProfile`

### UILibrary/MappingProfileList.xaml.cs
- Line 61 `Name = p.Name + " (copy)"` → `CopySuffix` (" (copy)") — append-style
- Line 92 `$"Delete mapping profile '{p.Name}'?"` → `ConfirmDeleteMappingProfileFormat`
- Line 93 `"Delete profile"` → `DeleteProfile`
- Line 123 `Add("Empty profile", …)` → `EmptyProfile`
- Line 123 default name `$"Profile {Profiles.Count + 1}"` → `ProfileDefaultNameFormat`
- Line 125 `Header = "PRESETS"` → `PresetsHeader`
- Line 127 `"FPS — Both directions (KB+M ↔ Controller)"` → `PresetFpsBoth`
- Line 128 `"FPS — KB+M → Controller"` → `PresetFpsKbToPad`
- Line 129 `"FPS — Controller → KB+M"` → `PresetFpsPadToKb`
- Line 130 `"Driving / Racing — KB+M → Controller"` → `PresetDrivingKbToPad`
- Line 131 `"Controller as Mouse (navigate Windows)"` → `PresetControllerAsMouse`

### InputLogic/Mapping/MappingPresets.cs
- Line 22 `Name = "FPS (KB+M → Pad)"` → `PresetNameFpsKbToPad`
- Line 79 `Name = "FPS preset (both directions)"` → `PresetNameFpsBoth`
- Line 97 `Name = "Driving (KB+M → Pad)"` → `PresetNameDrivingKbToPad`
- Line 130 `Name = "Controller as Mouse"` → `PresetNameControllerAsMouse`
- Line 173 `Name = "FPS (Pad → KB+M)"` → `PresetNameFpsPadToKb`

### UILibrary/AutoPlayActionList.xaml
- Line 49 `ToolTip="Edit Action"` → `EditAction`
- Line 59 `ToolTip="Delete Action"` → `DeleteAction`
- Line 76 `ToolTip="Add Action"` → `AddAction`

### UILibrary/AutoPlayActionList.xaml.cs
- Line 39 `Title = "Edit Action"` → `EditAction`
- Line 50 `Title = "Add Action"` → `AddAction`
- Line 61 `$"Delete action '{action.Name}'?"` → `ConfirmDeleteActionFormat`
- Line 61 `"Delete Action"` → `DeleteAction`

### UILibrary/AutoPlayProfileEdit.xaml
- Line 25 `Content="Profile Name"` → `ProfileName`
- Line 32 `Tag="Enter profile name..."` → `EnterProfileName`
- Line 46 `Content="Ollama Model"` → `OllamaModel`
- Line 52 `Tag="moondream, llava:7b, qwen2.5-vl:7b..."` → SKIP (example list)
- Line 62 `Text="Recommended: moondream (fast), llava:7b (balanced), qwen2.5-vl:7b (quality)"` → `OllamaModelRecommended`
- Line 71 `Content="Match process (optional)"` → `MatchProcessOptional`
- Line 77 `Tag="cs2*, valorant, *fortnite*"` → SKIP
- Line 88 long `Text="When set, this profile only fires…"` → `AutoPlayMatchProcessHelp`
- Line 99 `Content="Game Context (for AI)"` → `GameContext`
- Line 110 `Tag="Describe the game for the AI..."` → `DescribeGameForAi`
- Line 121 `Text="Example: This is a 2D platformer. The player needs to jump over obstacles and collect coins."` → `GameContextExample`
- Line 131 `Content="Actions"` → `Actions`
- Line 138 `Text="Define actions the AI can choose from. Each action has a name (e.g., 'move_left') and keys to press."` → `ActionsHelp`

### UILibrary/AutoPlayProfileEdit.xaml.cs
- Line 39 `AddSlider("Decision Interval", "seconds", …)` → `DecisionInterval` + REUSE `Seconds`
- Line 42 `slider.ToolTip = "Time between AI decisions. Lower = more responsive but higher load."` → `DecisionIntervalTooltip`

### UILibrary/AutoPlayProfileList.xaml
- Line 51 `ToolTip="Edit"` → REUSE `Edit`
- Line 62 `ToolTip="Delete"` → REUSE `Delete`
- Line 79 `ToolTip="Add Profile"` → `AddProfile`

### UILibrary/AutoPlayProfileList.xaml.cs
- Line 44 `Title = "Edit AutoPlay Profile"` → `EditAutoPlayProfile`
- Line 55 `Title = "Add AutoPlay Profile"` → `AddAutoPlayProfile`
- Line 67 `$"Delete profile '{profile.Name}'?"` → `ConfirmDeleteAutoPlayProfileFormat`
- Line 67 `"Delete Profile"` → `DeleteProfile`

### Visuality/AutoPlayActionEditDialog.xaml
- Line 11 `Title="Edit Action"` → `EditAction`
- Line 83 `Text="Action Name"` → `ActionName`
- Line 92 `Tag="e.g. move_left, jump, attack..."` → `ActionNamePlaceholder`
- Line 103 `Text="The name the AI will use to trigger this action. Use underscores, no spaces."` → `ActionNameHelp`
- Line 107 `Text="Description (for AI)"` → `DescriptionForAi`
- Line 119 `Tag="Describe what this action does..."` → `DescribeActionPlaceholder`
- Line 130 `Text="Help the AI understand when to use this action."` → `DescriptionForAiHelp`
- Line 134 `Text="Keys to Press"` → `KeysToPress`
- Line 142 `Text="The keys/buttons that will be pressed when this action is executed."` → `KeysToPressHelp`
- Line 153 `Content="Cancel"` → REUSE `Cancel`
- Line 160 `Content="Save"` → REUSE `Save`

### Visuality/AutoPlayActionEditDialog.xaml.cs
- Line 39 `AddDropdown("Action Type", …)` → `ActionType`
- Line 47 long ToolTip `"How this action behaves:\n…"` → `ActionTypeTooltip`
- Line 53 `AddSlider("Tap Duration", "seconds", …)` → `TapDuration` + REUSE `Seconds`
- Line 56 `slider.ToolTip = "How long to hold the key for instant/toggle actions."` → `TapDurationTooltip`

### Visuality/AutoPlayProfileEditDialog.xaml
- Line 10 `Title="Edit AutoPlay Profile"` → `EditAutoPlayProfile`
- Line 102 `Content="Cancel"` → REUSE `Cancel`
- Line 110 `Content="Save"` → REUSE `Save`

### Visuality/MessageDialog.xaml.cs
(Buttons already pull from Locale; the convenience helpers fall back to `Locale.Title` so titles passed in by callers are the catalogue items — see specific call sites elsewhere.)

### Visuality/NoticeBar.xaml
- Line 31 `Content="This feature has not been implemented yet."` (design-time only — at runtime constructor passes a string) — SKIP (placeholder)

### MainWindow.xaml
- Line 94 `ToolTip="Config"` → `ConfigTooltip`
- Line 159 `ToolTip="Restart PowerAim as administrator (needed to hide gamepad devices)"` → `RestartAsAdminTooltip`
- Line 174 `ToolTip="Find a setting (Ctrl+F)"` → `FindSettingTooltip`
- Line 204 `Tag="Search settings…"` → `SearchSettings`
- Line 221 `Text="Type to filter. Enter = jump to first result."` → `SearchHint`
- Line 246 `Content="MAIN"` → `MainSection`
- Line 322 `ToolTip="Toggle navigation"` → `ToggleNavigation`
- Line 358 `ToolTip="Gamepad"` → REUSE `Gamepad`
- Line 367 `Text="Gamepad"` → REUSE `Gamepad`
- Line 372 `ToolTip="Logs"` → REUSE `Logs`
- Line 381 `Text="Logs"` → REUSE `Logs`
- Line 386 `ToolTip="Settings"` → `Settings`
- Line 395 `Text="Settings"` → `Settings`
- Line 400 `ToolTip="About"` → `About`
- Line 410 `Text="About"` → `About`
- Line 418 `ToolTip="Aim Tools"` → `AimTools`
- Line 427 `Text="Aim Tools"` → `AimTools`
- Line 432 `ToolTip="Tools"` → `Tools`
- Line 441 `Text="Tools"` → `Tools`
- Line 446 `ToolTip="AutoPlay (AI Game Control)"` → `AutoPlayMenuTooltip`
- Line 455 `Text="AutoPlay"` → `AutoPlay`
- Line 460 `ToolTip="Controller / Keyboard Mapping"` → `MappingMenuTooltip`
- Line 469 `Text="Mapping"` → `Mapping`
- Line 474 `ToolTip="Models & Configs"` → `ModelsAndConfigs`
- Line 483 `Text="Models & Configs"` → `ModelsAndConfigs`
- Line 566 `Content="Models"` → `Models`
- Line 570 `Content="Configs"` → `Configs`
- Line 595 `Tag="Search models & downloads…"` → `SearchModelsDownloads`
- Line 649 `Text="Installed"` → `Installed`
- Line 653 `ToolTip="Open models folder"` → `OpenModelsFolder`
- Line 770 `Text="Installed"` → `Installed`
- Line 774 `ToolTip="Open configs folder"` → `OpenConfigsFolder`
- Line 991 `Content="Intel I5-13400F • NVIDIA GTX 1050"` → SKIP (replaced at runtime by GetProcessorName etc., literal is placeholder)
- Line 1077 `Text="Back"` → REUSE `Back`
- Line 1081 `Text="Hidden controllers"` → `HiddenControllers`
- Line 1118 `Text="Back"` → REUSE `Back`
- Line 1126 `Text="Edit Trigger"` → REUSE `EditTrigger`
- Line 1150 `Content="Cancel"` → REUSE `Cancel`
- Line 1157 `Content="Save"` → REUSE `Save`
- Line 1194 `Text="Back"` → REUSE `Back`
- Line 1201 `Text="Mapping profile:"` → `MappingProfileLabel`
- Line 1224 `Content="Discard"` → `Discard`
- Line 1230 `Content="Save"` → REUSE `Save`
- Line 1282 `Header="Bold"` → SKIP (placeholder, replaced at runtime)

### MainWindow.xaml.cs
- Line 371 `new NoticeBar("Couldn't resolve the PowerAim executable path.", 4000)` → `ResolveExePathFailed`
- Line 387 `new NoticeBar("UAC declined — staying in non-elevated mode.", 3000)` → `UacDeclined`
- Line 391 `new NoticeBar($"Restart-as-admin failed: {ex.Message}", 5000)` → `RestartAsAdminFailedFormat`
- Line 468 `new("Open Search", …)` → SKIP (command name, not displayed)
- Line 523 `"No matches."` → `NoMatches`
- Line 526 `$"{matches.Count} match{(matches.Count == 1 ? "" : "es")} · Enter = open first"` → `SearchMatchesFormat`
- Line 657 `var section = … .ToUpper()` and `"MAIN"` → `MainSection`
- Lines 1257, 1283 `"Calibrate sensitivity…"`, `"Detection masks…"` → `CalibrateSensitivity`, `DetectionMasksMenuItem`
- Line 1276 `AddToggleWithKeyBind("Use controller for aim", …)` → `UseControllerForAim`
- Line 1281 ToolTip `"Drive aim through the virtual Xbox controller's right-stick…"` → `UseControllerForAimTooltip`
- Line 1282 ToolTip `"Disabled — no working gamepad sender. Configure ViGEm in Settings → Gamepad first."` → `UseControllerForAimDisabledTooltip`
- Line 1338 `AddToggle("Use recoil pattern playback")` → `UsePatternPlayback`
- Line 1339 ToolTip `"When armed, plays back a previously recorded recoil pattern while you fire. Overrides the legacy / BETA paths."` → `UsePatternPlaybackTooltip`
- Line 1352 `AddButton("Recoil patterns…")` → `RecoilPatternsMenuItem`
- Line 1399 `$"Active pattern: {s.ActivePatternName}  ·  strength {s.PatternStrength:0.00}"` → `ActivePatternStatusFormat`
- Line 1401 `"Pattern playback armed but no pattern selected. Open 'Recoil patterns…' and click one in the list."` → `PatternArmedButNoneSelected`
- Line 1403 `"Mode: image-based (BETA, phase correlation)."` → `RecoilModeImageBased`
- Line 1405 `"Mode: legacy (fixed X/Y per fire rate). Record Fire Rate measures the cadence for this mode only."` → `RecoilModeLegacy`
- Line 1489 `new NoticeBar("WARNING: This method is the fastest, but not hidden for captures!!", 8000)` → `DrawingMethodWarning`
- Line 1495 `new NoticeBar("For more security we disabled the active state to prevent auto bans by capture analysis. Use this mode carefully! ", 10000)` → `DisabledActiveStateForSafety`
- Line 1623 `AddCredit("Internal Mode", "Internal mode virtualizes the controller…")` → `InternalModeTitle`, `InternalModeInfo`
- Line 1630 `AddCredit(Locale.Status, "Internal mode initialized", …)` → `InternalModeInitialized`
- Line 1703 `AddTitle("HWID Spoofer", false)` → `HwidSpoofer`
- Line 1705 `AddButton("Open HWID Spoofer")` → `OpenHwidSpoofer`
- Line 1706 `AddCredit("", "This external tool helps to change your Hardware Id, that can help if your PC is banned")` → `HwidSpooferHelp`
- Line 1724 `AddTitle("Active Processes", true)` → `ActiveProcesses`
- Line 1725 `AddToggle("Auto-pause when game loses focus")` → `AutoPauseOnFocusLoss`
- Line 1726 ToolTip `"Pauses AI actions when the foreground window is a known non-game …"` → `AutoPauseOnFocusLossTooltip`
- Line 1728 `AddToggle("Auto-switch profile by game")` → `AutoSwitchProfile`
- Line 1729 ToolTip `"When a Trigger / AutoPlay profile has a Match-Process pattern…"` → `AutoSwitchProfileTooltip`
- Line 1731 `AddCredit("", "Match patterns support wildcards (cs2*) and pipes (cs2|valorant). Configure them on each Trigger / AutoPlay profile.")` → `MatchPatternHelp`
- Line 1743 ToolTip `"Comma- or pipe-separated process patterns considered \"games\"..."` → `GameProcessPatternsTooltip`
- Line 1757 `AddTitle("Overlays", true)` → `Overlays`
- Line 1758 `AddToggle("Show debug overlay")` → `ShowDebugOverlay`
- Line 1759 ToolTip `"Tiny topmost panel showing FPS, inference time, detection count, current intent and active profile."` → `ShowDebugOverlayTooltip`
- Line 1762 `AddToggle("Show custom crosshair")` → `ShowCustomCrosshair`
- Line 1763 ToolTip `"Render a configurable crosshair at the centre of the primary screen…"` → `ShowCustomCrosshairTooltip`
- Line 1766 `AddDropdown("Crosshair shape", …)` → `CrosshairShape`
- Line 1769 `AddSlider("Crosshair size", "px", …)` → `CrosshairSize` + REUSE `Pixels`
- Line 1771 `AddSlider("Crosshair thickness", "px", …)` → `CrosshairThickness`
- Line 1773 `AddSlider("Crosshair gap", "px", …)` → `CrosshairGap`
- Line 1775 `AddSlider("Crosshair outline", "px", …)` → `CrosshairOutline`
- Line 1780 `AddTitle("Session Stats", true)` → `SessionStats`
- Lines 1802–1808 stats labels `"FPS"`, `"Inference time"`, `"Detections (last)"`, `"Shots fired"`, `"Frames processed"`, `"Tactical actions"`, `"Session"` → `StatFpsLabel`, `StatInferenceTime`, `StatDetectionsLast`, `StatShotsFired`, `StatFramesProcessed`, `StatTacticalActions`, `StatSession`
- Line 1812 `AddButton("Reset stats")` → `ResetStats`
- Line 1813 `AddToggle("Adaptive Kalman lead-time")` → `AdaptiveKalmanLead`
- Line 1814 ToolTip `"Scales the predictor's lead-time based on target velocity…"` → `AdaptiveKalmanLeadTooltip`
- Line 1819 `AddTitle("HUD OCR", true)` → `HudOcr`
- Line 1820 `AddToggle("Enable HUD OCR engine")` → `EnableHudOcr`
- Line 1821 ToolTip `"Periodically samples screen regions (ammo, health, score) with Tesseract…"` → `EnableHudOcrTooltip`
- Line 1823 `AddSlider("OCR interval", "ms", …)` → `OcrInterval` + `MillisecondsShort` (or REUSE `Milliseconds` if happy with "Milliseconds")
- Line 1825 `AddButton("Configure OCR regions…")` → `ConfigureOcrRegions`
- Line 1832 `AddTitle("Replay Buffer", true)` → `ReplayBuffer`
- Line 1833 `AddToggle("Record rolling buffer")` → `RecordRollingBuffer`
- Line 1834 ToolTip `"Keeps the last N seconds of frames + detections in RAM…"` → `RecordRollingBufferTooltip`
- Line 1836 `AddSlider("Buffer length", "s", …)` → `BufferLength` + `SecondsShort` ("s")
- Line 1838 `AddSlider("JPEG quality", "", …)` → `JpegQuality`
- Line 1848 `Content = "0 frames buffered"` → `FramesBufferedZero`
- Line 1853 `$"{…} frames buffered"` → `FramesBufferedFormat`
- Line 1855 `AddButton("Save replay buffer")` → `SaveReplayBuffer`
- Line 1857 `replayStatus.Content = "Exporting…"` → `Exporting`
- Line 1860 `"Nothing to export — buffer is empty."` → `NothingToExportEmpty`
- Line 1861 `$"Saved to {folder}"` → `SavedToFormat`
- Line 1863 `AddButton("Clear buffer")` → `ClearBuffer`
- Line 1868 `AddTitle("AutoPlay Learning", true)` → `AutoPlayLearning`
- Line 1869 `AddToggle("Record my playstyle")` → `RecordPlaystyle`
- Line 1870 ToolTip `"While on, samples (state, action) tuples from your input…"` → `RecordPlaystyleTooltip`
- Line 1872 `AddToggle("Apply learned bias in AutoPlay")` → `ApplyLearnedBias`
- Line 1873 ToolTip `"When AutoPlay picks a tactical action, the learned model can nudge the choice…"` → `ApplyLearnedBiasTooltip`
- Line 1875 `AddSlider("Bias strength", "", …)` → `BiasStrength`
- Line 1877 `AddSlider("Sample interval", "ms", …)` → `SampleInterval`
- Line 1893 `AddButton("Save model")` → `SaveModel`
- Line 1898 `$"Saved · {…} samples"` → `SavedSamplesFormat`
- Line 1900 `$"Save failed: {ex.Message}"` → `SaveFailedFormat`
- Line 1902 `AddButton("Load model")` → `LoadModel`
- Line 1905 `"Model loaded."` → `ModelLoaded`
- Line 1905 `"No model file found at the configured path."` → `NoModelFileAtPath`
- Line 1908 `AddButton("Clear model")` → `ClearModel`
- Line 1925 `$"{…} samples · {…} states"` → `LearningStatusFormat`
- Line 1952 `AddTitle("AutoPlay (AI Game Control)", true)` → `AutoPlayMenuTitle`
- Line 1956 `AddToggleWithKeyBind("AutoPlay", "AutoPlay", …)` → `AutoPlay`
- Line 1958 ToolTip `"Enable AI-controlled gameplay using Ollama vision models"` → `AutoPlayToggleTooltip`
- Line 1962 `AddTitle("Ollama Settings")` → `OllamaSettings`
- Line 1963 `AddSlider("Request Timeout", "seconds", …)` → `RequestTimeout` + REUSE `Seconds`
- Line 1964 `AddSlider("Temperature", "", …)` → `Temperature`
- Line 1965 `AddSlider("Image Max Size", "px", …)` → `ImageMaxSize` + REUSE `Pixels`
- Line 1966 `AddSlider("Image Quality", "%", …)` → `ImageQuality` + REUSE `PercentSign`
- Line 1971 `AddTitle("AutoPlay Profiles", true)` → `AutoPlayProfiles`
- Line 1977 `AddTitle("Quick Start")` → `QuickStart`
- Lines 1978–1982 5 credits `("1. Install Ollama", "Visit ollama.com and install")` etc. → `QuickStartStep1Title`/`QuickStartStep1Body`, …Step5
- Line 1999 `"Switch to DirectML Version" / "Switch to CUDA version"` → `SwitchToDirectML`, `SwitchToCuda`
- Line 2050 `AddDropdown<AppThemeMode>("Theme Mode", …)` → `ThemeMode`
- Line 2058 `AddToggle("Locale.ShowHelpTexts")` → **BUG**: literal `"Locale.ShowHelpTexts"` should be `Locale.ShowHelpTexts`; key `ShowHelpTexts`
- Line 2060 `AddToggle("Hide UI from screen capture")` → `HideUIFromCapture`
- Line 2067 `"Warning: if you disable this, the Aimmy window and all its overlays may become visible…"` → `DisableCaptureProtectionWarning`
- Line 2068 `"Disable capture protection?"` → `DisableCaptureProtectionTitle`
- Lines 2141–2161 credits (Developers / Contributors / Model Creators) — currently `return;` is hit immediately, so SKIP unless re-enabled.
- Line 2235 `"Loading Config: "` → REUSE `LoadingConfigMessage`

### UILibrary/OllamaStatusIndicator.xaml
- Line 31 `Text="Ollama: Checking..."` → `OllamaChecking`
- Line 40 ToolTip `"Refresh Status"` → `RefreshStatus`
- Line 49 `Text="URL: http://localhost:11434"` → SKIP (placeholder, replaced at runtime)
- Line 56 `Text="Models: -"` → `ModelsDashFallback`
- Line 71 `Text="Install Ollama: ollama.com | Start: 'ollama serve'"` → `InstallOllamaHint`

### UILibrary/OllamaStatusIndicator.xaml.cs
- Line 39 `"http://localhost:11434"` → SKIP (URL fallback)
- Line 40 `$"URL: {url}"` → `OllamaUrlFormat`
- Line 53 `"Ollama: Connected"` → `OllamaConnected`
- Line 65 `$"Vision Models: {string.Join(", ", visionModels.Take(5))}"` → `OllamaVisionModelsFormat`
- Line 67 `$"Models: {string.Join(", ", models.Take(5))} (no vision models)"` → `OllamaNoVisionModelsFormat`
- Line 71 `"Models: None found"` → `OllamaNoModelsFound`
- Line 77 `"Ollama: Disconnected"` → `OllamaDisconnected`
- Line 78 `"Models: -"` → `ModelsDashFallback`
- Line 82 `$"Error: {error}"` → `ErrorFormat`
- Line 99 `"Ollama: Checking..."` → `OllamaChecking`

### UILibrary/HiddenControllersPanel.xaml
- Line 11 long help → `HiddenControllersHelp`
- Line 26 long warning `"⚠ PowerAim is not running elevated…"` → `NotElevatedWarning`
- Line 31 `Content="Refresh"` → `Refresh`

### UILibrary/HiddenControllersPanel.xaml.cs
- Line 46 `Text = "No HID gaming devices detected. Plug a controller in and click Refresh."` → `NoHidDevicesDetected`
- Line 61 `$"Device enumeration failed: {ex.Message}"` → `DeviceEnumerationFailedFormat`
- Line 88 `$"{d.FriendlyName}    (HIDDEN)"` → `HiddenSuffix` (or `ControllerHiddenFormat` "{0}    (HIDDEN)")
- Line 124 `Content = d.Enabled ? "Hide from games" : "Show again"` → `HideFromGames`, `ShowAgain`
- Line 131-132 ToolTip `"Disable this device system-wide via CM_Disable_DevNode…"` → `HideDeviceTooltip`
- Line 133 ToolTip `"Re-enable the device."` → `ReEnableDeviceTooltip`
- Line 134 ToolTip `"Requires PowerAim running as administrator…"` → `RequiresAdminTooltip`
- Line 145 `$"'{d.FriendlyName}' {(d.Enabled ? "hidden" : "shown")}…"` → `DeviceToggledFormat` (with "hidden"/"shown" via separate keys `Hidden`/`Shown`)
- Line 146 `$"Could not {…} '{d.FriendlyName}': {DeviceHide.LastError}"` → `DeviceToggleFailedFormat`
- Line 148 `$"Operation failed: {ex.Message}"` → `OperationFailedFormat`

### UILibrary/GamepadDiagnosticsPanel.xaml
- Line 11 `Text="XInput slots (the 4 controllers Windows tracks)"` → `XInputSlotsHeader`
- Line 26 `Text="Detected gamepads (Windows HID devices)"` → `DetectedGamepadsHeader`
- Line 36 `Content="Manage hidden controllers →"` → `ManageHiddenControllers`
- Line 39 ToolTip `"Opens a focused page where you can hide / unhide individual controllers without HidHide."` → `ManageHiddenControllersTooltip`
- Line 44 `Text="PowerAim sender"` → `PowerAimSender`
- Line 56 `Text="Diagnose"` → `Diagnose`
- Line 72 `Content="Fire test RT pulse"` → `FireTestRtPulse`
- Line 76 `Content="Reconnect virtual pad"` → `ReconnectVirtualPad`
- Line 79 ToolTip `"Forces a USB disconnect + reconnect of PowerAim's virtual controller…"` → `ReconnectVirtualPadTooltip`
- Line 81 `Content="Reset HidHide cloak"` → `ResetHidHideCloak`
- Line 84 ToolTip `"Turns HidHide's master cloak off and unhides every device…"` → `ResetHidHideCloakTooltip`
- Line 86 `Content="Refresh now"` → `RefreshNow`
- Line 91 `Content="Launch HidHide UI"` → REUSE `LaunchHidHideUI`
- Line 94 ToolTip `"Opens HidHideClient.exe if installed…"` → `LaunchHidHideUITooltip`
- Line 96 `Content="Open Gamepad Tester"` → `OpenGamepadTester`
- Line 99 ToolTip `"Switches to the live gamepad tester page…"` → `OpenGamepadTesterTooltip`
- Line 101 `Content="Windows joy.cpl"` → `WindowsJoyCpl`
- Line 104 ToolTip `"Opens Windows' built-in game controller properties dialog (joy.cpl)."` → `WindowsJoyCplTooltip`

### UILibrary/GamepadDiagnosticsPanel.xaml.cs
- Line 51 `string detail = "(empty)"` → `EmptyParen`
- Line 58 `$"RT={…} LT={…} Btns=0x{…}"` → SKIP (technical raw values)
- Line 62 `$"error: {ex.Message}"` → `ErrorLowerFormat`
- Line 74-75 `"No HID gaming devices detected."` → `NoHidDevicesDetectedShort`
- Line 75 `$"{devices.Count} detected, {hidden} hidden. Click → for the full list with Hide / Show actions."` → `DevicesSummaryFormat`
- Line 80 `$"Device enumeration failed: {ex.Message}"` → `DeviceEnumerationFailedFormat`
- Line 92 `"(unknown)"` → `UnknownParen`
- Line 93–103 MakeKv labels: `"Send-mode"`, `"Sender instance"`, `"(null)"`, `"Sender.CanWork"`, `"AutoHideController (HidHide)"`, `"UseControllerForAim"`, `"PowerAim elevated"`, `"HidHide installed"` → `DiagSendMode`, `DiagSenderInstance`, `NullParen`, `DiagSenderCanWork`, `DiagAutoHideController`, `DiagUseControllerForAim`, `DiagPowerAimElevated`, `DiagHidHideInstalled`
- Line 112–133 Suggestion strings — these are long multi-paragraph diagnostic texts. Suggested keys:
  - `SuggestVigemMissing`
  - `SuggestNoSender`
  - `SuggestVigemCantWork`
  - `SuggestSenderCantWork`
  - `SuggestViGEmSlotConflict`
  - `SuggestXInputHook`
  - `SuggestViGEmNoPad`
  - `SuggestGenericSenderAlive`
- Line 159 `Text = $"Slot {index}"` → `SlotFormat`
- Line 225-228 reconnect strings → `VirtualPadReconnected`, `VirtualPadReconnectFailed`, `VirtualPadReconnectNotViGEm`
- Line 244-246 reset HidHide outcomes → `HidHideResetOk`, `HidHideResetFailed`
- Line 256 `"HidHide isn't installed — install it from https://github.com/ViGEm/HidHide first."` → `HidHideNotInstalled`
- Line 263 `"HidHideClient.exe not found next to the CLI. Reinstall HidHide."` → `HidHideClientNotFound`
- Line 265 `$"Could not launch HidHide UI: {ex.Message}"` → `LaunchHidHideUiFailedFormat`
- Line 271 `$"Navigation failed: {ex.Message}"` → `NavigationFailedFormat`
- Line 286 `$"Could not open joy.cpl: {ex.Message}"` → `OpenJoyCplFailedFormat`
- Line 300 `"Sender unavailable — can't fire a pulse. See the diagnose box above."` → `SenderUnavailable`
- Line 306 `"Test pulse fired (RT=255 for 250ms). Watch the slot rows above — whichever slot's RT spikes is where the virtual pad is. If your physical pad's slot also shows RT, you might be touching the trigger."` → `TestPulseFiredHelp`
- Line 312 `$"Test pulse failed: {ex.Message}"` → `TestPulseFailedFormat`

### UILibrary/GamepadTesterPanel.xaml
- Line 67 `Text="Gamepad Controller Test"` → `GamepadControllerTest`
- Line 86 `Text="Disconnected"` → `Disconnected`
- Line 97 ToolTip `"Open the tester in a separate, non-modal window…"` → `PopOutTooltip`
- Line 103 `Text="Pop out"` → `PopOut`
- Line 126 `Text="D-Pad"` → `DPad`
- Line 145 `Text="Center"` → `Center`
- Line 148 `Text="Back"` → REUSE `Back`
- Line 151 `Text="Start"` → `Start`
- Line 172 `Text="Face Buttons"` → `FaceButtons`
- Line 204 `Text="Shoulders & Triggers"` → `ShouldersAndTriggers`
- Line 217 `Text="Left Trigger"` → `LeftTrigger`
- Line 235 `Text="Right Trigger"` → `RightTrigger`
- Line 247 `Text="Sequence Testing"` → `SequenceTesting`
- Line 260 `Text="Delay before start"` → `DelayBeforeStart`
- Line 264 `Text="Test Sequence"` → `TestSequence`
- Lines 266–271 ComboBox items "A Button Press (500ms)", "A+B Combo (1s)", "D-Pad Circle (2s)", "Left Stick Circle (2s)", "Trigger Press (1s)", "Complex Combo (3s)" → `SeqAButtonPress`, `SeqABCombo`, `SeqDpadCircle`, `SeqLStickCircle`, `SeqTriggerPress`, `SeqComplexCombo`
- Line 275 long `Text="Simulates D-Pad presses in a circular pattern: Up → Right → Down → Left"` → `SeqDpadCircleDescription`
- Line 286 `Content="Stop"` → `Stop`
- Line 293 `Content="Start Test Sequence"` → `StartTestSequence`
- Line 309 `Text="Refresh connection"` → `RefreshConnection`

### UILibrary/GamepadTesterPanel.xaml.cs
- Lines 85, 103, 112 `"Connected"`, `"Disconnected"` → `Connected`, `Disconnected`
- Line 97 `$"Connected (Controller {controllerNumber})"` → `ConnectedControllerFormat`
- Lines 179–185 sequence descriptions → `SeqDescAButton`, `SeqDescABCombo`, `SeqDescDpadCircle` (REUSE the XAML default), `SeqDescLStickCircle`, `SeqDescTriggerPress`, `SeqDescComplexCombo`
- Line 194 `"Error: No gamepad sender available. Please select a gamepad mode."` → `NoGamepadSenderError`
- Line 213 `$"Starting in {i}…"` → `StartingInFormat`
- Line 217 `"Running sequence…"` → `RunningSequence`
- Line 220 `"Sequence completed."` → `SequenceCompleted`
- Line 224 `"Sequence stopped."` → `SequenceStopped`
- Line 228 `$"Error: {ex.Message}"` → `ErrorFormat`

### Visuality/GamepadTesterWindow.xaml
- Line 5 `Title="Gamepad Tester"` → `GamepadTester`

### Visuality/GamepadDiagnosticsDialog.xaml
- Line 5 `Title="Gamepad Diagnostics"` → `GamepadDiagnostics`
- Line 34 `Text="Gamepad Diagnostics"` → `GamepadDiagnostics`
- Line 47 `Text="What PowerAim sees in your system right now."` → `GamepadDiagSubtitle`
- Line 60 `Text="XInput slots (the 4 controllers Windows tracks)"` → `XInputSlotsHeader`
- Line 76 `Text="PowerAim sender status"` → `PowerAimSenderStatus`
- Line 92 `Text="Suggested action"` → `SuggestedAction`
- Line 114 `Content="Fire test RT pulse"` → `FireTestRtPulse`
- Line 118 `Content="Refresh"` → `Refresh`

### Visuality/ScreenRegionPicker.xaml
- Line 28 `Text="Drag a rectangle around the HUD element you want OCR to read.  ESC = cancel."` → `ScreenRegionPickerHint`

### Visuality/SetAntiRecoil.xaml
- Line 11 `Title="Set Static FOV Positioning"` → `SetStaticFovPositioning`

### Visuality/CrosshairOverlay.xaml / Visuality/DebugOverlay.xaml
- Inspected: no user-visible literal strings (Canvas / Shapes only) — SKIP

### Visuality/HiddenBoxesPill.cs
- Line 131 `Text = "Hidden sections"` → `HiddenSections`
- Line 179 `_label.Text = hidden.Count == 1 ? "1 hidden section" : $"{hidden.Count} hidden sections"` → `OneHiddenSection`, `HiddenSectionsCountFormat`

### UILibrary/AKeyChanger.xaml
- Line 17 `MenuItem Header="Delete Binding"` → `DeleteBinding`
- Line 29 `Content="Change Aim Keybind"` → SKIP (default placeholder — KeyChangerTitle gets replaced at runtime; if never overridden, treat as design-time)
- Line 53 `Content="Right"` → SKIP (design-time placeholder)
- Line 79 ToolTip `"Configure min time"` → `ConfigureMinTime`

### UILibrary/AKeyChanger.xaml.cs
- Line 62 `new PropertyMetadata("None")` → REUSE `None` (default fallback shown in UI when invalid)

### UILibrary/AFileLocator.xaml
- Line 21 `Content="Prediction Method"` → SKIP (design-time placeholder)
- Line 32 `Tag="No File Located"` → `NoFileLocated`

### UILibrary/AProcessPicker.xaml
- Line 35 `Content="Select Process..."` → REUSE `SelectProcess` (text is "Select Process" — current Locale value matches if we drop ellipsis)

### UILibrary/ADropdown.xaml
- Line 21 `Content="Prediction Method"` → SKIP (design-time)

### UILibrary/ATitle.xaml
- Line 52 `Content="Title"` → SKIP (design-time placeholder)

### UILibrary/ADownloadGateway.xaml
- Line 22 `Content="Refresh"` → SKIP (design-time placeholder)

### UILibrary/MultiKeyChanger.xaml
(All visible text already routed through `Locale` via converter — nothing to add.)

### UILibrary/CaptureSourceSelect.xaml
- Line 35 `Select Monitor to Capture` → `SelectMonitorToCapture`
- Line 37 `Current: ` → `CurrentColon`
- Line 60 `Text="Monitor"` → `Monitor`
- Line 91 `Select Application Window to Capture` → `SelectApplicationWindow`
- Line 93 `Current:` → `CurrentColon`
- Line 116 `Text="Window"` → `Window`

### UILibrary/CaptureSourceSelect.xaml.cs
- Line 139 `Console.WriteLine("Error updating preview")` → SKIP (dev log)
- Line 211 `Text = "Select capture window"` → `SelectCaptureWindow`
- Line 223 `Content = "Browse all…"` → `BrowseAll`
- Line 373 `Text = $"PID {process.Id}"` → `PidFormat` ("PID {0}")
- Line 475 `Text = "Select capture monitor"` → `SelectCaptureMonitor`
- Line 577 `Text = "Primary"` → `Primary`
- Line 631 `Text = $"Position {monitor.Bounds.X}, {monitor.Bounds.Y}"` → `PositionFormat`

### Visuality/TargetClassDialog.xaml
- Line 8 `Title="Target Classes"` → REUSE `TargetClasses`
- Line 40 `Text="Target Classes"` → REUSE `TargetClasses`
- Line 78 `Text="Filter mode"` → REUSE `TargetClassFilterMode`
- Line 83 `Content="All Classes"` → REUSE `AllClasses`
- Line 88 `Content="Only selected"` → REUSE `SpecificClasses`

### Visuality/MessageDialog.xaml.cs / MainWindow.xaml.cs — confirmation dialogs
- MainWindow.xaml.cs line 2067 (capture protection warning) — listed above
- MainWindow.xaml.cs / MappingProfileList — covered above
- All other `MessageDialog.Show(…)` titles already pull from `Locale.*` constants.

### UILibrary/TriggerList.xaml
(All visible text routed through `localeConverter` — nothing new.)

### UILibrary/TriggerEdit.xaml
- Line 63 `Text="Trigger"` → `TriggerSectionTitle` (note: existing `Title` value is "Title" — not suitable here)
- Line 64 `Text="Name and active state"` → REUSE `TriggerSubtitle`
- Line 90 `Text="Match process (optional)"` → `MatchProcessOptional`
- Line 91 `Text="Only fire while a process matching the pattern is focused"` → `MatchProcessFireHint`
- Line 95 `Tag="cs2*, valorant, *fortnite*"` → SKIP
- Line 101 `Text="Supports wildcards (*, ?) and pipes (cs2|valorant). Leave empty for always-on. Requires 'Auto-switch profile by game' in Settings → Active Processes."` → `TriggerMatchProcessWildcardHelp`
- Line 120 `Text="Keys or buttons to send when triggered"` → REUSE `ActionsSubtitle`
- Line 149 `Text="Detection"` → REUSE `Detection`
- Line 150 `Text="Whether and how detection gates the trigger"` → REUSE `DetectionSubtitle`
- Line 186 `Text="When charging begins"` → REUSE `WhenChargingBegins`
- Line 206 `Text="When trigger fires"` → REUSE `WhenTriggerFires`
- Line 235 `Text="Keys that must be pressed for the trigger to run"` → REUSE `TriggerKeysSubtitle`
- Line 271 `Text="Keys that block the trigger when pressed"` → REUSE `AntiTriggerKeysSubtitle`
- Line 304 `Text="Timing"` → REUSE `Timing`
- Line 305 `Text="Delays and pauses between fires"` → REUSE `TimingSubtitle`

### Visuality/ConfigSaver.xaml
(All visible text already localised — nothing to add.)

### Visuality/KnownIssuesDialog.xaml
(All visible text already localised — nothing to add.)

### Visuality/UpdateDialog.xaml
(All visible text already localised — nothing to add.)

### Visuality/EditHeadArea.xaml
(All visible text already localised — nothing to add.)

### Visuality/MagnifierDialog.xaml
(No user-visible literal — SKIP.)

### Visuality/FOV.xaml / Visuality/DetectedPlayerWindow.xaml
- DetectedPlayerWindow line 33 `Content="Confidence = 20%"` → SKIP (design-time only; replaced at runtime by Confidence label)

### Visuality/ProcessPickerDialog.xaml
(All visible text already localised.)

### AILogic/PredictionLogic.cs
- Line 89 `$"CUDA load failed ({cudaEx.Message}), retrying with DirectML."` → `CudaLoadFailedFormat`
- Line 95 `$"Error starting the model: {dmlEx.Message}"` → `ErrorStartingModelFormat`
- Line 202 `$"Output shape does not match the expected shape of {…}. … Please use a YOLOv8 model converted to ONNXv8."` → `OutputShapeMismatchFormat`
- Line 346 `$"Collect Data isn't working, try again later. {e.Message}"` → `CollectDataFailedFormat`

### AILogic/AIManager.cs
- Line 48 `new NoticeBar($"Error: {e.Message}", 5000)` → `ErrorFormat`

### Config/AutoPlayProfile.cs
- Line 192 `Name ?? "New Profile"` (used inside the user-visible Description) → `NewProfileFallback`

---

## Summary
- Total candidate strings catalogued: ~310 (incl. ~70 marked REUSE of existing keys).
- XAML files affected: 22 (significant text additions).
- C# files affected: 18 (significant text additions).
- All `Visuality/*Dialog.xaml.cs` files were inspected; most dialogs only need their *English code-side* messages added (titles + error strings).

## Likely-bug notes (out of scope, mentioned for visibility)
- `MainWindow.xaml.cs:2058` — `UISettings.AddToggle("Locale.ShowHelpTexts")` is passing the literal string `"Locale.ShowHelpTexts"` as the toggle label instead of resolving `Locale.ShowHelpTexts`. The bound config property `ShowHelpTexts` exists, but the label shown to the user is the literal source-code identifier. Worth fixing as part of the localisation pass — and the key `ShowHelpTexts` will need to be added to `Locale.json`.

## New keys (English source)

Some keys are *suggestions* — feel free to adjust naming. Keys marked as REUSE above are NOT included here.

```json
{
  "SensitivityCalibration": "Sensitivity Calibration",
  "SensitivityCalibrationWizard": "Sensitivity Calibration Wizard",
  "CalibrateToYourGame": "Calibrate to your game",
  "CalibrationIntroBody": "PowerAim measures how many screen pixels your game moves per unit of raw mouse input. It then suggests a damping factor (MouseSensitivity) so the AI's aim correction doesn't overshoot.",
  "CalibrationBeforeYouStart": "Before you start:",
  "CalibrationBulletList": "• Switch to the game window.\n• Aim at a static, textured surface (e.g. a wall, the floor).\n• Don't move your mouse — the wizard will move it for you.\n• The wizard takes about 2 seconds.",
  "CalibrationTip": "Tip: also disable raw input acceleration / smoothing in your game for the most accurate result.",
  "CalibrationRunning": "Calibrating…",
  "DontMoveMouse": "Don't move the mouse",
  "Result": "Result",
  "MeasuredPixelsPerInput": "Measured pixels / input",
  "SamplesUsed": "Samples used",
  "CurrentMouseSensitivity": "Current MouseSensitivity",
  "SuggestedMouseSensitivity": "Suggested MouseSensitivity",
  "CalibrationFailed": "Calibration failed",
  "StartCalibration": "Start calibration",
  "Running": "Running…",
  "ApplySuggested": "Apply suggested",
  "NoCaptureSourceLoaded": "No capture source available. Make sure a model is loaded and PowerAim is initialised.",
  "Cancelled": "Cancelled.",
  "UnknownError": "Unknown error.",
  "CalibrationInterpretLow": "Your in-game sensitivity is quite low (the screen barely moves per mouse unit). Consider raising it so PowerAim has more headroom to correct.",
  "CalibrationInterpret1to1": "1:1 sensitivity. No damping needed — keep MouseSensitivity at 0.",
  "CalibrationInterpretSlightHigh": "Slightly high in-game sensitivity. A small amount of damping helps the AI settle on targets.",
  "CalibrationInterpretHigh": "High in-game sensitivity. The damping factor will keep the AI from overshooting on close-range corrections.",
  "CalibrationInterpretVeryHigh": "Very high in-game sensitivity. Heavy damping is required; you may also want to lower your in-game sens for finer control.",

  "HudOcrRegions": "HUD OCR Regions",
  "OcrCoordinatesHelp": "Coordinates are in absolute screen pixels (not relative to the FOV box). The recognizer runs on its own background timer so it doesn't impact your AI inference framerate.",
  "AddRegion": "Add region",
  "NoRegionSelected": "No region selected",
  "EditingItemFormat": "Editing: {0}",
  "Kind": "Kind",
  "OcrKindText": "Text",
  "OcrKindNumber": "Number",
  "OcrKindHealth": "Health",
  "ScreenRectangle": "Screen rectangle",
  "PickOnScreen": "Pick on screen…",
  "BinarizeThreshold": "Binarize threshold",
  "OcrInvertCheckbox": "Invert (white text on dark HUD)",
  "LivePreview": "Live preview",
  "OcrEngineRunning": "OCR engine running",
  "DownloadTesseractData": "Download Tesseract data",
  "TestEngine": "Test engine",
  "TestEngineTooltip": "Synchronously instantiate the Tesseract engine and report whether the native DLL loaded — useful for diagnosing first-run setup issues.",
  "OcrEmpty": "(empty)",
  "OcrConfidenceFormat": "Confidence: {0:0.00} · {1:HH:mm:ss}",
  "OcrNoReadingYet": "No reading yet — enable the engine and wait one polling cycle.",
  "OcrRegionDefaultNameFormat": "Region {0}",
  "OcrDownloadingTraineddata": "Downloading eng.traineddata…",
  "OcrDownloadingTraineddataProgressFormat": "Downloading eng.traineddata… {0}%",
  "OcrTraineddataInstalledFormat": "eng.traineddata installed in {0}",
  "OcrDownloadFailed": "Download failed — drop eng.traineddata into the tessdata folder manually.",
  "OcrTraineddataMissing": "eng.traineddata is missing — use 'Download Tesseract data'.",
  "OcrEnginePaused": "Engine paused.",
  "OcrEngineRunningStatus": "Engine running.",

  "RecoilPatterns": "Recoil Patterns",
  "RecoilPatternsHelp": "Record a 2-second pattern by firing the weapon at a static wall — PowerAim measures the screen drift via phase correlation and replays the inverse stroke while you shoot. Adjust PatternStrength to scale the effect for different in-game sensitivities.",
  "RecordPattern2s": "Record (2s)",
  "DeleteSelected": "Delete selected",
  "Weapon": "Weapon",
  "NoPatternSelected": "No pattern selected.",
  "PatternSamplesDurationFormat": "{0} samples · {1} ms total",
  "UsePatternPlayback": "Use pattern playback while firing",
  "UsePatternPlaybackTooltip": "When armed, plays back a previously recorded recoil pattern while you fire. Overrides the legacy / BETA paths.",
  "PatternStrength": "Pattern strength",
  "RecoilPreviewLegend": "Accent: Y drift · Faint: X drift",
  "NoCaptureSourceLoadModelFirst": "No capture source — load a model first.",
  "RecordingClickToCancel": "Recording… (click to cancel)",
  "RecordingFireWeapon": "Fire the weapon — recording for 2 seconds.",
  "RecordingProgressFormat": "Fire the weapon — {0}% recorded.",
  "Recording": "(recording)",
  "PatternDefaultNameFormat": "Pattern {0}",
  "RecordingNoMotion": "No motion detected — aim at a textured surface and try again.",
  "RecordingDoneFormat": "Recorded {0} samples over {1} ms.",
  "RecordingFailedFormat": "Recording failed: {0}",
  "ActivePatternStatusFormat": "Active pattern: {0}  ·  strength {1:0.00}",
  "PatternArmedButNoneSelected": "Pattern playback armed but no pattern selected. Open 'Recoil patterns…' and click one in the list.",
  "RecoilModeImageBased": "Mode: image-based (BETA, phase correlation).",
  "RecoilModeLegacy": "Mode: legacy (fixed X/Y per fire rate). Record Fire Rate measures the cadence for this mode only.",
  "RecoilPatternsMenuItem": "Recoil patterns…",

  "DetectionRegionMasks": "Detection Region Masks",
  "DetectionMasksHelp": "Draw rectangles inside the detection area to suppress false positives (HUD, ammo, minimap). The Y-axis is flipped so 0,0 is top-left — same orientation as your screen. Toggle individual masks to disable without deleting.",
  "AddMask": "Add mask",
  "ClearAll": "Clear all",
  "MasksDragHint": "(drag inside to draw / move selected mask)",
  "NoMaskSelected": "No mask selected",
  "MasksFooterHelp": "Predictions whose centre lands inside an enabled mask are dropped before reaching the targeting/trigger pipeline.",
  "MaskDefaultNameFormat": "Mask {0}",
  "DetectionMasksMenuItem": "Detection masks…",

  "MappingTitle": "Controller ↔ Keyboard Mapping",
  "MappingIntro": "Make games respond to whichever input device they don't natively support — kb+mouse drives a virtual gamepad, or a controller drives kb+mouse. Pick a profile and a direction; flip the master switch when you're ready to play.",
  "MappingProfiles": "Mapping profiles",
  "MappingProfilesHelp": "One active profile at a time — the engine picks the first enabled profile whose 'match process' (if any) is focused. Click the pencil to edit, the diamond to duplicate, the bin to delete.",
  "NoneParen": "(none)",
  "MappingActiveOffHint": "  —  Mapping active is OFF. Flip the master toggle above to activate the profile.",
  "NoMappingProfileEnabledHint": "  —  No profile is enabled. Toggle one in the list below.",
  "MappingEngineStatusFormat": "Engine: {0} · Active profile: {1}{2}",

  "Profile": "Profile",
  "ProfileName": "Profile name",
  "MatchProcessOptional": "Match process (optional)",
  "MatchProcessActivateHint": "Only activate while a process matching the pattern is focused",
  "MatchProcessFireHint": "Only fire while a process matching the pattern is focused",
  "MatchProcessWildcardHelp": "Supports wildcards (*, ?) and pipes (cs2|valorant). Leave empty for always-on.",
  "TriggerMatchProcessWildcardHelp": "Supports wildcards (*, ?) and pipes (cs2|valorant). Leave empty for always-on. Requires 'Auto-switch profile by game' in Settings → Active Processes.",
  "Mappings": "Mappings",
  "MappingsSubtitle": "Click a Source / Target slot, then press the key or controller button you want to bind.",
  "AddRow": "+ Add",
  "AddRowTooltip": "Add an empty mapping row. Click each side to record a key / button.",
  "AddStickToMouse": "+ Stick → Mouse",
  "AddStickToMouseTooltip": "Adds the special right-stick → mouse-motion mapping.",
  "AddMouseToStick": "+ Mouse → Stick",
  "AddMouseToStickTooltip": "Adds the special mouse-motion → right-stick mapping.",
  "SourceHeader": "SOURCE",
  "TargetHeader": "TARGET",
  "ActivatorHeader": "ACTIVATOR",
  "NoMappingsYet": "No mappings yet. Click '+ Add' above, then click each slot to record a key or controller button.",
  "StickMotionTuning": "Stick & Motion Tuning",
  "StickTuningSubtitle": "Dead-zone, response curve and sensitivities for stick ↔ mouse",
  "MouseToStickSensitivity": "Mouse → Stick sensitivity",
  "MultiplierUnit": "× scale",
  "StickToMouseSensitivity": "Stick → Mouse sensitivity",
  "PxPerTickUnit": "px per tick",
  "DeadZone": "Dead-zone",
  "FullDeflectionUnit": "× full deflection",
  "AntiDeadZone": "Anti-dead-zone",
  "MouseResponseCurve": "Mouse response curve",
  "ExponentUnit": "exponent",
  "InvertYAxis": "Invert Y axis (stick ↔ mouse)",
  "MappingActivatorTooltip": "Press style: Press = while held, LongPress = after holding, DoubleTap = quick two presses, Toggle = latching, Pulse = brief fire-and-release.",
  "RemoveMapping": "Remove this mapping",
  "RecordNormalTooltip": "Switch back to recording a normal key / button / trigger.",
  "SpecialEndpointTooltip": "Pick a stick direction or mouse-motion target (things the recorder can't capture).",
  "MenuLeftStickUp": "🎮 Left Stick — Up",
  "MenuLeftStickDown": "🎮 Left Stick — Down",
  "MenuLeftStickLeft": "🎮 Left Stick — Left",
  "MenuLeftStickRight": "🎮 Left Stick — Right",
  "MenuRightStickUp": "🎮 Right Stick — Up",
  "MenuRightStickDown": "🎮 Right Stick — Down",
  "MenuRightStickLeft": "🎮 Right Stick — Left",
  "MenuRightStickRight": "🎮 Right Stick — Right",
  "MenuMouseMotionRelative": "🖱 Mouse motion (relative)",

  "MatchPrefix": "Match: {0}",
  "Duplicate": "Duplicate",
  "NewProfile": "+ New profile",
  "NewProfileTooltip": "Create a new profile — empty or pre-populated from a preset.",
  "CopySuffix": " (copy)",
  "ConfirmDeleteMappingProfileFormat": "Delete mapping profile '{0}'?",
  "DeleteProfile": "Delete profile",
  "EmptyProfile": "Empty profile",
  "ProfileDefaultNameFormat": "Profile {0}",
  "PresetsHeader": "PRESETS",
  "PresetFpsBoth": "FPS — Both directions (KB+M ↔ Controller)",
  "PresetFpsKbToPad": "FPS — KB+M → Controller",
  "PresetFpsPadToKb": "FPS — Controller → KB+M",
  "PresetDrivingKbToPad": "Driving / Racing — KB+M → Controller",
  "PresetControllerAsMouse": "Controller as Mouse (navigate Windows)",
  "PresetNameFpsKbToPad": "FPS (KB+M → Pad)",
  "PresetNameFpsBoth": "FPS preset (both directions)",
  "PresetNameDrivingKbToPad": "Driving (KB+M → Pad)",
  "PresetNameControllerAsMouse": "Controller as Mouse",
  "PresetNameFpsPadToKb": "FPS (Pad → KB+M)",

  "EditAction": "Edit Action",
  "AddAction": "Add Action",
  "DeleteAction": "Delete Action",
  "ConfirmDeleteActionFormat": "Delete action '{0}'?",
  "ActionName": "Action Name",
  "ActionNamePlaceholder": "e.g. move_left, jump, attack...",
  "ActionNameHelp": "The name the AI will use to trigger this action. Use underscores, no spaces.",
  "DescriptionForAi": "Description (for AI)",
  "DescribeActionPlaceholder": "Describe what this action does...",
  "DescriptionForAiHelp": "Help the AI understand when to use this action.",
  "KeysToPress": "Keys to Press",
  "KeysToPressHelp": "The keys/buttons that will be pressed when this action is executed.",
  "ActionType": "Action Type",
  "ActionTypeTooltip": "How this action behaves:\n• Continuous: Held until another action is chosen\n• Instant: Quick tap (jump, reload)\n• Modifier: Can combine with other actions (sprint, aim)\n• Toggle: Press once to toggle on/off (crouch)",
  "TapDuration": "Tap Duration",
  "TapDurationTooltip": "How long to hold the key for instant/toggle actions.",

  "EnterProfileName": "Enter profile name...",
  "OllamaModel": "Ollama Model",
  "OllamaModelRecommended": "Recommended: moondream (fast), llava:7b (balanced), qwen2.5-vl:7b (quality)",
  "AutoPlayMatchProcessHelp": "When set, this profile only fires while a process matching the pattern is focused. Supports wildcards (*, ?) and pipes (cs2|valorant). Leave empty for always-on.",
  "GameContext": "Game Context (for AI)",
  "DescribeGameForAi": "Describe the game for the AI...",
  "GameContextExample": "Example: This is a 2D platformer. The player needs to jump over obstacles and collect coins.",
  "Actions": "Actions",
  "ActionsHelp": "Define actions the AI can choose from. Each action has a name (e.g., 'move_left') and keys to press.",
  "DecisionInterval": "Decision Interval",
  "DecisionIntervalTooltip": "Time between AI decisions. Lower = more responsive but higher load.",

  "EditAutoPlayProfile": "Edit AutoPlay Profile",
  "AddAutoPlayProfile": "Add AutoPlay Profile",
  "AddProfile": "Add Profile",
  "ConfirmDeleteAutoPlayProfileFormat": "Delete profile '{0}'?",

  "AutoPlay": "AutoPlay",
  "AutoPlayMenuTitle": "AutoPlay (AI Game Control)",
  "AutoPlayToggleTooltip": "Enable AI-controlled gameplay using Ollama vision models",
  "OllamaSettings": "Ollama Settings",
  "RequestTimeout": "Request Timeout",
  "Temperature": "Temperature",
  "ImageMaxSize": "Image Max Size",
  "ImageQuality": "Image Quality",
  "AutoPlayProfiles": "AutoPlay Profiles",
  "QuickStart": "Quick Start",
  "QuickStartStep1Title": "1. Install Ollama",
  "QuickStartStep1Body": "Visit ollama.com and install",
  "QuickStartStep2Title": "2. Pull a Vision Model",
  "QuickStartStep2Body": "Run: ollama pull moondream",
  "QuickStartStep3Title": "3. Start Ollama",
  "QuickStartStep3Body": "Run: ollama serve",
  "QuickStartStep4Title": "4. Create Profile",
  "QuickStartStep4Body": "Add actions like 'move_left', 'jump'",
  "QuickStartStep5Title": "5. Enable AutoPlay",
  "QuickStartStep5Body": "Toggle AutoPlay on",
  "AutoPlayMenuTooltip": "AutoPlay (AI Game Control)",
  "MappingMenuTooltip": "Controller / Keyboard Mapping",
  "MappingProfileLabel": "Mapping profile:",
  "Discard": "Discard",
  "NewProfileFallback": "New Profile",

  "ConfigTooltip": "Config",
  "RestartAsAdminTooltip": "Restart PowerAim as administrator (needed to hide gamepad devices)",
  "FindSettingTooltip": "Find a setting (Ctrl+F)",
  "SearchSettings": "Search settings…",
  "SearchHint": "Type to filter. Enter = jump to first result.",
  "MainSection": "MAIN",
  "ToggleNavigation": "Toggle navigation",
  "AimTools": "Aim Tools",
  "Tools": "Tools",
  "Settings": "Settings",
  "About": "About",
  "Mapping": "Mapping",
  "ModelsAndConfigs": "Models & Configs",
  "Models": "Models",
  "Configs": "Configs",
  "SearchModelsDownloads": "Search models & downloads…",
  "Installed": "Installed",
  "OpenModelsFolder": "Open models folder",
  "OpenConfigsFolder": "Open configs folder",
  "HiddenControllers": "Hidden controllers",

  "ResolveExePathFailed": "Couldn't resolve the PowerAim executable path.",
  "UacDeclined": "UAC declined — staying in non-elevated mode.",
  "RestartAsAdminFailedFormat": "Restart-as-admin failed: {0}",
  "NoMatches": "No matches.",
  "SearchMatchesFormat": "{0} matches · Enter = open first",

  "CalibrateSensitivity": "Calibrate sensitivity…",
  "UseControllerForAim": "Use controller for aim",
  "UseControllerForAimTooltip": "Drive aim through the virtual Xbox controller's right-stick instead of synthesising mouse motion. Useful for games that lock out KB+M input or apply controller-specific aim-assist. Click the keybind icon on the right to set a global hotkey.",
  "UseControllerForAimDisabledTooltip": "Disabled — no working gamepad sender. Configure ViGEm in Settings → Gamepad first.",

  "DrawingMethodWarning": "WARNING: This method is the fastest, but not hidden for captures!!",
  "DisabledActiveStateForSafety": "For more security we disabled the active state to prevent auto bans by capture analysis. Use this mode carefully!",
  "InternalModeTitle": "Internal Mode",
  "InternalModeInfo": "Internal mode virtualizes the controller without requiring external drivers. The virtual controller can be manipulated via the existing interfaces and classes.",
  "InternalModeInitialized": "Internal mode initialized",

  "HwidSpoofer": "HWID Spoofer",
  "OpenHwidSpoofer": "Open HWID Spoofer",
  "HwidSpooferHelp": "This external tool helps to change your Hardware Id, that can help if your PC is banned",

  "ActiveProcesses": "Active Processes",
  "AutoPauseOnFocusLoss": "Auto-pause when game loses focus",
  "AutoPauseOnFocusLossTooltip": "Pauses AI actions when the foreground window is a known non-game (browser, terminal, PowerAim itself, …).",
  "AutoSwitchProfile": "Auto-switch profile by game",
  "AutoSwitchProfileTooltip": "When a Trigger / AutoPlay profile has a Match-Process pattern, it's only active while the matching game is in the foreground.",
  "MatchPatternHelp": "Match patterns support wildcards (cs2*) and pipes (cs2|valorant). Configure them on each Trigger / AutoPlay profile.",
  "GameProcessPatternsTooltip": "Comma- or pipe-separated process patterns considered \"games\". Empty = use the built-in non-game blacklist.",

  "Overlays": "Overlays",
  "ShowDebugOverlay": "Show debug overlay",
  "ShowDebugOverlayTooltip": "Tiny topmost panel showing FPS, inference time, detection count, current intent and active profile.",
  "ShowCustomCrosshair": "Show custom crosshair",
  "ShowCustomCrosshairTooltip": "Render a configurable crosshair at the centre of the primary screen. Useful for games without a built-in crosshair.",
  "CrosshairShape": "Crosshair shape",
  "CrosshairSize": "Crosshair size",
  "CrosshairThickness": "Crosshair thickness",
  "CrosshairGap": "Crosshair gap",
  "CrosshairOutline": "Crosshair outline",

  "SessionStats": "Session Stats",
  "StatFpsLabel": "FPS",
  "StatInferenceTime": "Inference time",
  "StatDetectionsLast": "Detections (last)",
  "StatShotsFired": "Shots fired",
  "StatFramesProcessed": "Frames processed",
  "StatTacticalActions": "Tactical actions",
  "StatSession": "Session",
  "ResetStats": "Reset stats",
  "AdaptiveKalmanLead": "Adaptive Kalman lead-time",
  "AdaptiveKalmanLeadTooltip": "Scales the predictor's lead-time based on target velocity — less lead on stationary targets, more on fast strafing ones.",

  "HudOcr": "HUD OCR",
  "EnableHudOcr": "Enable HUD OCR engine",
  "EnableHudOcrTooltip": "Periodically samples screen regions (ammo, health, score) with Tesseract. Results are exposed to AutoPlay via OcrService.Instance.Latest.",
  "OcrInterval": "OCR interval",
  "MillisecondsShort": "ms",
  "ConfigureOcrRegions": "Configure OCR regions…",

  "ReplayBuffer": "Replay Buffer",
  "RecordRollingBuffer": "Record rolling buffer",
  "RecordRollingBufferTooltip": "Keeps the last N seconds of frames + detections in RAM. Click 'Save replay' to flush to disk as a PNG sequence + annotations.json.",
  "BufferLength": "Buffer length",
  "SecondsShort": "s",
  "JpegQuality": "JPEG quality",
  "FramesBufferedZero": "0 frames buffered",
  "FramesBufferedFormat": "{0} frames buffered",
  "SaveReplayBuffer": "Save replay buffer",
  "Exporting": "Exporting…",
  "NothingToExportEmpty": "Nothing to export — buffer is empty.",
  "SavedToFormat": "Saved to {0}",
  "ClearBuffer": "Clear buffer",

  "AutoPlayLearning": "AutoPlay Learning",
  "RecordPlaystyle": "Record my playstyle",
  "RecordPlaystyleTooltip": "While on, samples (state, action) tuples from your input and current detections. Stored in an in-memory model that can be saved to disk.",
  "ApplyLearnedBias": "Apply learned bias in AutoPlay",
  "ApplyLearnedBiasTooltip": "When AutoPlay picks a tactical action, the learned model can nudge the choice toward what you typically did in similar situations.",
  "BiasStrength": "Bias strength",
  "SampleInterval": "Sample interval",
  "SaveModel": "Save model",
  "SavedSamplesFormat": "Saved · {0} samples",
  "SaveFailedFormat": "Save failed: {0}",
  "LoadModel": "Load model",
  "ModelLoaded": "Model loaded.",
  "NoModelFileAtPath": "No model file found at the configured path.",
  "ClearModel": "Clear model",
  "LearningStatusFormat": "{0} samples · {1} states",

  "SwitchToDirectML": "Switch to DirectML Version",
  "SwitchToCuda": "Switch to CUDA version",
  "ThemeMode": "Theme Mode",
  "ShowHelpTexts": "Show help texts",
  "HideUIFromCapture": "Hide UI from screen capture",
  "DisableCaptureProtectionWarning": "Warning: if you disable this, the Aimmy window and all its overlays may become visible in screen recordings, streams (OBS, Discord, etc.) and other capture tools.\n\nAre you sure you want to disable capture protection?",
  "DisableCaptureProtectionTitle": "Disable capture protection?",

  "OllamaChecking": "Ollama: Checking...",
  "RefreshStatus": "Refresh Status",
  "ModelsDashFallback": "Models: -",
  "InstallOllamaHint": "Install Ollama: ollama.com | Start: 'ollama serve'",
  "OllamaUrlFormat": "URL: {0}",
  "OllamaConnected": "Ollama: Connected",
  "OllamaVisionModelsFormat": "Vision Models: {0}",
  "OllamaNoVisionModelsFormat": "Models: {0} (no vision models)",
  "OllamaNoModelsFound": "Models: None found",
  "OllamaDisconnected": "Ollama: Disconnected",
  "ErrorFormat": "Error: {0}",
  "ErrorLowerFormat": "error: {0}",

  "HiddenControllersHelp": "Manage which controllers Windows exposes to games. Disable a device system-wide via stock Windows (no HidHide needed) — requires running PowerAim as administrator.",
  "NotElevatedWarning": "⚠ PowerAim is not running elevated — Hide / Show buttons are disabled. Use the 'Restart as admin' button in the top bar.",
  "Refresh": "Refresh",
  "NoHidDevicesDetected": "No HID gaming devices detected. Plug a controller in and click Refresh.",
  "NoHidDevicesDetectedShort": "No HID gaming devices detected.",
  "DeviceEnumerationFailedFormat": "Device enumeration failed: {0}",
  "ControllerHiddenFormat": "{0}    (HIDDEN)",
  "HideFromGames": "Hide from games",
  "ShowAgain": "Show again",
  "HideDeviceTooltip": "Disable this device system-wide via CM_Disable_DevNode. Games stop seeing it; PowerAim's virtual pad can claim slot 0.",
  "ReEnableDeviceTooltip": "Re-enable the device.",
  "RequiresAdminTooltip": "Requires PowerAim running as administrator. Use the 'Restart as admin' button in the top bar.",
  "Hidden": "hidden",
  "Shown": "shown",
  "DeviceToggledFormat": "'{0}' {1}. Windows refreshes XInput on next pump.",
  "DeviceToggleFailedFormat": "Could not {0} '{1}': {2}",
  "OperationFailedFormat": "Operation failed: {0}",

  "XInputSlotsHeader": "XInput slots (the 4 controllers Windows tracks)",
  "DetectedGamepadsHeader": "Detected gamepads (Windows HID devices)",
  "ManageHiddenControllers": "Manage hidden controllers →",
  "ManageHiddenControllersTooltip": "Opens a focused page where you can hide / unhide individual controllers without HidHide.",
  "PowerAimSender": "PowerAim sender",
  "PowerAimSenderStatus": "PowerAim sender status",
  "Diagnose": "Diagnose",
  "SuggestedAction": "Suggested action",
  "FireTestRtPulse": "Fire test RT pulse",
  "ReconnectVirtualPad": "Reconnect virtual pad",
  "ReconnectVirtualPadTooltip": "Forces a USB disconnect + reconnect of PowerAim's virtual controller. Often kicks games into picking it up when they were stuck on a different slot.",
  "ResetHidHideCloak": "Reset HidHide cloak",
  "ResetHidHideCloakTooltip": "Turns HidHide's master cloak off and unhides every device — fixes situations where past PowerAim sessions accidentally cloaked your real or virtual pads.",
  "RefreshNow": "Refresh now",
  "LaunchHidHideUITooltip": "Opens HidHideClient.exe if installed. Use HidHide to cloak your physical pad from games while leaving PowerAim's virtual pad visible.",
  "OpenGamepadTester": "Open Gamepad Tester",
  "OpenGamepadTesterTooltip": "Switches to the live gamepad tester page — useful for verifying physical input + slot mapping.",
  "WindowsJoyCpl": "Windows joy.cpl",
  "WindowsJoyCplTooltip": "Opens Windows' built-in game controller properties dialog (joy.cpl).",
  "GamepadDiagnostics": "Gamepad Diagnostics",
  "GamepadDiagSubtitle": "What PowerAim sees in your system right now.",
  "GamepadTester": "Gamepad Tester",
  "GamepadControllerTest": "Gamepad Controller Test",
  "Disconnected": "Disconnected",
  "Connected": "Connected",
  "ConnectedControllerFormat": "Connected (Controller {0})",
  "PopOut": "Pop out",
  "PopOutTooltip": "Open the tester in a separate, non-modal window so you can keep it visible while doing other things in PowerAim.",
  "DPad": "D-Pad",
  "Center": "Center",
  "Start": "Start",
  "FaceButtons": "Face Buttons",
  "ShouldersAndTriggers": "Shoulders & Triggers",
  "LeftTrigger": "Left Trigger",
  "RightTrigger": "Right Trigger",
  "SequenceTesting": "Sequence Testing",
  "DelayBeforeStart": "Delay before start",
  "TestSequence": "Test Sequence",
  "SeqAButtonPress": "A Button Press (500ms)",
  "SeqABCombo": "A+B Combo (1s)",
  "SeqDpadCircle": "D-Pad Circle (2s)",
  "SeqLStickCircle": "Left Stick Circle (2s)",
  "SeqTriggerPress": "Trigger Press (1s)",
  "SeqComplexCombo": "Complex Combo (3s)",
  "SeqDpadCircleDescription": "Simulates D-Pad presses in a circular pattern: Up → Right → Down → Left",
  "SeqDescAButton": "Presses the A button for 500ms",
  "SeqDescABCombo": "Presses A and B buttons together for 1 second",
  "SeqDescLStickCircle": "Moves left thumbstick in a circular motion",
  "SeqDescTriggerPress": "Presses both triggers to maximum and releases",
  "SeqDescComplexCombo": "Complex sequence: A → B → X → Y with D-Pad movements",
  "Stop": "Stop",
  "StartTestSequence": "Start Test Sequence",
  "RefreshConnection": "Refresh connection",
  "NoGamepadSenderError": "Error: No gamepad sender available. Please select a gamepad mode.",
  "StartingInFormat": "Starting in {0}…",
  "RunningSequence": "Running sequence…",
  "SequenceCompleted": "Sequence completed.",
  "SequenceStopped": "Sequence stopped.",

  "EmptyParen": "(empty)",
  "UnknownParen": "(unknown)",
  "NullParen": "(null)",
  "DiagSendMode": "Send-mode",
  "DiagSenderInstance": "Sender instance",
  "DiagSenderCanWork": "Sender.CanWork",
  "DiagAutoHideController": "AutoHideController (HidHide)",
  "DiagUseControllerForAim": "UseControllerForAim",
  "DiagPowerAimElevated": "PowerAim elevated",
  "DiagHidHideInstalled": "HidHide installed",
  "DevicesSummaryFormat": "{0} detected, {1} hidden. Click → for the full list with Hide / Show actions.",
  "SlotFormat": "Slot {0}",
  "SuggestVigemMissing": "⚠ ViGEmBus driver appears to be missing. PowerAim couldn't create the virtual controller — every gamepad-output feature (synth triggers, mapping engine, controller-aim) is dead until you fix this.\n\n1. Download ViGEmBus: https://github.com/nefarius/ViGEmBus/releases (pick the latest .exe installer).\n2. Run the installer (UAC prompt).\n3. Restart PowerAim.\n\nThe diagnostic will then show Sender.CanWork=true and a virtual pad will appear in one of the XInput slots above.",
  "SuggestNoSender": "GamepadManager has no sender. Open Gamepad-Settings → Send-mode and pick one (ViGEm is the easy default; needs the ViGEmBus driver).",
  "SuggestVigemCantWork": "⚠ ViGEm sender created but CanWork=false — the ViGEmBus driver is missing or broken. Install/repair from https://github.com/nefarius/ViGEmBus/releases and reboot.",
  "SuggestSenderCantWork": "⚠ Sender exists ({0}) but CanWork=false. For XInputHook this is expected if the bundled XInputHook.dll is x86-only — fails for 64-bit games (CoD, Apex). Switch to ViGEm + slot swap.",
  "SuggestViGEmSlotConflict": "Your physical pad sits at XInput slot {0}, which is the slot games read from. The virtual pad lands at a higher slot — the game isn't seeing it.\n\nPick ONE of these (in order of friction):\n• Easiest: unplug your physical pad, restart PowerAim, plug it back in. The virtual claims slot 0 first.\n• Install HidHide (https://github.com/ViGEm/HidHide) and enable 'AutoHideController' below.\n• Or run PowerAim as administrator and use the Disable buttons we'll add per device — disables the pad system-wide.",
  "SuggestXInputHook": "XInputHook injects EasyHook into the game. The bundled XInputHook.dll is 32-bit only — works for x86 games (rare today), fails for x64 games (Call of Duty, Apex Legends, etc.) with 'STATUS_INVALID_PARAMETER_5 64-Bit library does not exist'. Use ViGEm + slot swap instead.",
  "SuggestViGEmNoPad": "ViGEm sender alive, no physical pad detected. Test the virtual pad with the button below — your game should read from slot 0.",
  "SuggestGenericSenderAlive": "Sender is alive. Fire the test pulse below — whichever slot lights up is where the virtual pad sits.",
  "VirtualPadReconnected": "Virtual pad disconnected + reconnected. Look at the slot list above — if the slot moved, your game now needs to re-read XInput (Alt-Tab COD or restart it to be sure).",
  "VirtualPadReconnectFailed": "Reconnect failed — see the log for details. Most commonly this means ViGEmBus has hit an error and needs a reboot.",
  "VirtualPadReconnectNotViGEm": "Active sender isn't ViGEm — reconnect is only meaningful there. Switch send-mode to ViGEm first.",
  "HidHideResetOk": "HidHide reset: cloak off, device-hide-list cleared, app-whitelist cleared. If your virtual pad was being mistakenly hidden, it should now be visible to games.",
  "HidHideResetFailed": "HidHide reset attempted but the CLI returned an error. HidHide might not be installed — that's also fine, in which case nothing was hidden in the first place.",
  "HidHideNotInstalled": "HidHide isn't installed — install it from https://github.com/ViGEm/HidHide first.",
  "HidHideClientNotFound": "HidHideClient.exe not found next to the CLI. Reinstall HidHide.",
  "LaunchHidHideUiFailedFormat": "Could not launch HidHide UI: {0}",
  "NavigationFailedFormat": "Navigation failed: {0}",
  "OpenJoyCplFailedFormat": "Could not open joy.cpl: {0}",
  "SenderUnavailable": "Sender unavailable — can't fire a pulse. See the diagnose box above.",
  "TestPulseFiredHelp": "Test pulse fired (RT=255 for 250ms). Watch the slot rows above — whichever slot's RT spikes is where the virtual pad is. If your physical pad's slot also shows RT, you might be touching the trigger.",
  "TestPulseFailedFormat": "Test pulse failed: {0}",

  "ScreenRegionPickerHint": "Drag a rectangle around the HUD element you want OCR to read.  ESC = cancel.",
  "SetStaticFovPositioning": "Set Static FOV Positioning",

  "HiddenSections": "Hidden sections",
  "OneHiddenSection": "1 hidden section",
  "HiddenSectionsCountFormat": "{0} hidden sections",

  "DeleteBinding": "Delete Binding",
  "ConfigureMinTime": "Configure min time",
  "NoFileLocated": "No File Located",

  "SelectMonitorToCapture": "Select Monitor to Capture",
  "SelectApplicationWindow": "Select Application Window to Capture",
  "CurrentColon": "Current: ",
  "Monitor": "Monitor",
  "Window": "Window",
  "SelectCaptureWindow": "Select capture window",
  "BrowseAll": "Browse all…",
  "PidFormat": "PID {0}",
  "SelectCaptureMonitor": "Select capture monitor",
  "Primary": "Primary",
  "PositionFormat": "Position {0}, {1}",

  "CudaLoadFailedFormat": "CUDA load failed ({0}), retrying with DirectML.",
  "ErrorStartingModelFormat": "Error starting the model: {0}",
  "OutputShapeMismatchFormat": "Output shape does not match the expected shape of {0}.\n\nPlease use a YOLOv8 model converted to ONNXv8.",
  "CollectDataFailedFormat": "Collect Data isn't working, try again later. {0}",

  "TriggerSectionTitle": "Trigger"
}
```
