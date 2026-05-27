# Changelog

All notable changes to PowerAim are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0.0]

### Added
- **AutoPlay profile editor is now a full in-app page** (like the trigger editor) instead of a
  popup dialog. Editing or creating a profile opens inline with Back / Cancel / Save.
- **AutoPlay strategic layer now gets live game context** with every decision: the number of
  enemies currently detected, your OCR-read ammo/health (when matching OCR regions are
  configured), and a short history of its own recent decisions — with an instruction to vary
  them, so the bot stops oscillating or getting stuck in one mode.
- **Four new default AutoPlay actions** in the "FPS Default" profile — *interact*, *melee*,
  *grenade*, and *switch weapon* — each mapped to sensible keyboard + controller buttons and
  wired into the tactical/strategic action pool.
- **New setting: "Keybinds only while Global Active."** When enabled, every global keybind
  (toggle hotkeys, trigger/mapping enable hotkeys, model & config switch hotkeys) only fires
  while Global Active is on. The Global Active hotkey itself is always exempt so you can still
  switch it back on. Off by default — existing behavior is unchanged unless you opt in.
- **5-second "get ready" countdown** before Sensitivity Calibration and Recoil Pattern recording
  start, with a live on-screen counter so you have time to focus the game, line up your aim, and
  start firing. Both are cancellable during the countdown.
- New settings, tooltips, and countdown messages are fully localized across all 9 supported
  languages.

### Changed
- **More responsive AutoPlay defaults:** new profiles default to a `0.5s` decision interval
  (was `1.0s`), the "FPS Default" profile is aligned to `0.5s`, and the strategic loop's lower
  bound was reduced so faster intervals actually take effect.
- **Aim correction now uses the model's real input size** instead of a hard-coded value, so
  targeting stays accurate on models that aren't 640px.
- **FOV Size and Dynamic FOV Size slider maximums now track the current Image Size** (e.g.
  740 → 740, 1200 → 1200), updated live, instead of a fixed ceiling.

### Removed
- **ESP Opacity and FOV Opacity sliders.** Now that overlay colors carry their own alpha channel,
  transparency is controlled directly from the color pickers for detection boxes, tracers,
  confidence text, and the FOV circle. The color's alpha is now respected across both overlay
  render paths.

### Fixed
- **Header keybind "min time" popup now opens.** The Global Active keybind in the title bar
  couldn't open its min-time configuration popup — the window's click-to-drag handler was
  swallowing the click before it reached the control. Key changers are now exempt from window
  dragging, so the popup works in the header (it already worked inside the lists). As a bonus,
  clicking a keybind in the header no longer accidentally drags the window.
