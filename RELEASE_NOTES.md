# PowerAim 2.1.0 — Release notes

> Everything new on this release versus 2.0.0.2. The headline work is a complete
> rebuild of the aim configuration around **multiple, simultaneously-active profiles**,
> a new **Dynamic Tools** system, a unified **Controller Overview**, and a
> **Gamepad Tester** you can point at any controller. This is written in
> [Keep a Changelog](https://keepachangelog.com/) style so it can fold straight into
> `CHANGELOG.md` at release time.

## Added

### 🎯 Aim — profile-based, simpler, calibrated

- **Aim is now fully profile-based — no single "global" aim config.** Every aim
  setting (key, feel, FOV, region, smoothing, prediction, disengage, OCR
  auto-activation) lives on an **aim profile**. The old global aim settings were
  migrated into a seeded **Default** profile on first load, so nothing is lost.
- **Multiple active profiles at once.** More than one aim profile can be enabled
  and engaged at the same time — the aim action iterates every profile whose
  **aim key is held** and whose **OCR auto-activation condition** is met. This
  replaces the single-active radio model.
- **A required aim key per profile**, with a **duplicate-key warning** when two
  profiles share the same key, and the key control moved to the top of the editor
  where it belongs. A per-row keybind on the profile list toggles that profile on/off.
- **Rebuilt aiming pipeline.** The default path is a lean **nearest-target +
  proportional move** instead of an always-on multi-object tracker — far less
  jitter and overshoot, and predictable behaviour in a closed capture→move loop.
- **Per-profile smoothing modes:** **None**, **EMA**, or a **1€ filter** (adaptive:
  calm when the target is slow, responsive on flicks), with the 1€ min-cutoff and
  beta exposed for fine-tuning.
- **Opt-in target tracking** per profile — the SORT-style tracker, frame **coasting**
  through dropped detections and **switch hysteresis** (won't flip-flop between two
  enemies) are now an optional layer you turn on, not a forced default.
- **Per-profile prediction** — the legacy prediction methods (Kalman / Shalloe /
  WiseTheFox) and EMA are still available per profile for those who want the old
  behaviour; new profiles default to the simpler calibrated path.
- **Sensitivity calibrator.** The calibration wizard measures your real
  screen-pixels-per-mouse-count and stores it, so the aim move is **game-independent**
  — the same profile feels the same across games regardless of in-game sensitivity.
- **Random aim point** now lives in the profile's **Aim region** section (it picks a
  point inside the region) and is configured alongside the region editor.
- **Redesigned aim-profile editor** — icon-badged sections (Profile, Aim key, Feel,
  Aim region, Disengage, Auto-activation) matching the trigger editor, and it now
  opens as a full in-window **page** (Back / Cancel / Save) instead of a dialog.
- **Revised aim presets** so the built-in feels (smooth / snappy / precise /
  humanized / legacy) match the simplified pipeline.

### 🧰 Dynamic Tools — build your own

- **The Tools page is now a unified list of expandable cards.** Each tool has a
  **start keybind**, a **start button (▶)**, an enable toggle, and an expandable
  panel. The built-in **Magnifier** and **HWID Spoofer** are always present (with
  their own controls); custom tools add Edit / Delete.
- **Build your own tools.** A custom tool is an **ordered sequence of typed actions**
  that runs **once** per start:
  - **Move mouse** (relative delta or absolute screen position),
  - **Click** (left / right / middle, down / up / down-and-up),
  - **Send key(s)** — the same multi-key control as triggers, with **record-sequence**
    and **Sequential vs. Simultaneous** execution,
  - **Run program** (path + arguments, optionally as admin, optionally wait for exit),
  - **Delay** (ms).
- **User-defined options as variables.** Define options (String / Number / Bool /
  Path / Enum) on a tool and reference them inside any action's text fields as
  `{name}` — e.g. an option `target=100`, then *Move mouse X = {target}*. Option
  values are editable right on the tool's panel before you run it.
- **In-window tool editor page** (Back / Cancel / Save) with an options editor and a
  reorderable action sequence; editing works on a clone so **Cancel truly discards**.
- **Fire-once with restart.** A running tool that's started again cancels and restarts;
  any key/button a sequence latched is force-released on cancel (no stuck inputs).

### 🎮 Controller Overview

- **One place for every controller.** A new controller manager lists **all** detected
  physical pads **and** the virtual pad in a single view, each with a connected dot,
  its **XInput slot**, and tag chips for the current **sync source** and **hidden** state.
- **Set the sync source** (which physical pad mirrors into the virtual one) and
  **hide a controller from games** — via **HidHide** when installed (PowerAim stays
  on the allow-list, so it still sees the pad), otherwise an internal soft-hide.
- **One-click "make games use the virtual pad."** Because Windows assigns XInput
  slots by arrival order and an app can't reassign them, this hides the physical pad
  from XInput (HidHide cloak, or a system disable when elevated) and reconnects the
  virtual one so it drops to the low slot games read first — with a one-click
  **Restore** to undo it.
- The virtual pad is **named by the active send mode** (ViGEm / vJoy / XInput hook /
  internal), not hard-coded to "ViGEm".
- The control is **reusable as a picker** (see the Gamepad Tester).

### 🕹️ Gamepad Tester — pick what you're watching

- **Choose which controller the tester visualizes.** A controller picker at the top
  lets you point the live button / trigger / stick panel at any detected pad.
- **See what happens where.** Select the **virtual** pad to watch exactly what
  PowerAim and a **test sequence** inject (read from the injected virtual state, or
  the virtual pad's XInput slot); select a **physical** pad to watch your own input —
  a test sequence won't show there because it's sent to the virtual pad. This makes
  the sync / virtual-output path visible at a glance.

### ⌨️ Combo (multi-key) keybinds

- **Keybinds can now be chords** — keyboard, mouse and gamepad freely mixed, e.g.
  `Ctrl+Shift+X`, `Ctrl+Left-Click`, `X+B` (two pad buttons), `G+Mouse-Left`.
- **Record-on-release:** start recording, hold the combination, and it commits on the
  first release capturing everything still held. Matching fires only while **all**
  parts are held; sending a chord emits it correctly (e.g. `Ctrl+A` → Ctrl↓ A↓ A↑ Ctrl↑).
- Works everywhere a keybind does — trigger keys, aim keys, tool start keys — with no
  change needed to existing single-key binds (old configs stay byte-identical).

### ⚡ Quick Config & config labels

- **Quick Config window** — a compact, fast view of the most-used settings with its
  own keybind, for tweaking on the fly without opening the full UI.
- **Config label overlay** — an on-screen label of the active config.

### 🌐 Website & docs

- A **static landing page + features page** (`docs/index.html`, `docs/features.html`)
  and a refreshed documentation set under `docs/`.

## Changed

- **Aim config schema** bumped with an idempotent migration that seeds a Default
  profile from the old global aim settings and moves the global aim key / EMA /
  prediction onto the previously-active profile. Old configs load unchanged.
- **The Magnifier's start hotkey** is now the unified per-tool keybind on the Tools
  list (the old standalone `MagnifierKeybind` is migrated onto it on first load); the
  zoom-in / zoom-out keys remain in the Magnifier's panel.
- **Send-keys execution** in tools mirrors the trigger model — Simultaneous (all at
  once, each honouring its recorded delay) or Sequential.

## Fixed

- **Startup crash** when migrating the Magnifier hotkey — the `BindingSettings`
  indexer returns null for a never-set key, and the migration dereferenced it; now
  null-safe.
- **Freeze when switching a tool option from String to Number** — the option type
  dropdown rebuilt its own list mid-selection, re-firing its auto-select and queuing
  unbounded rebuilds. Guarded with the same build-flag pattern the aim editor uses;
  the same fix prevents an existing action from being reset when you open it.
- **Stuck mouse button / key** if a tool sequence was cancelled between a standalone
  Down and its Up — the runner now force-releases anything it latched in a `finally`.
- **Silent data loss on editor Cancel** — editing a tool now works on a clone, so
  Cancel can't keep in-place edits to the options / actions collections.
- **`NullReferenceException` in `HidHideHelper.GetHidHidePath()`** when the controller
  catalog's refresh tick runs during a config (re)load — made null-safe against a
  not-yet-loaded `AppConfig.Current` / `FileLocationState`.
- **Corrupt / forward-incompatible config no longer silently resets to defaults** —
  `AppConfig.Load` backs the original file up to `*.corrupt.bak` before falling back,
  and logs the real exception, so a bad load can be recovered.
- Various null-safety hardening on the new tool model (Options / Actions / EnumValues /
  UserTools collections never null even from a hand-edited config) and bounds
  normalization on Number options (an inverted Min/Max no longer throws).

## Internal

- New config models — `ToolDefinition` / `CustomTool`, `ToolOption`, polymorphic
  `ToolAction` (System.Text.Json `$type`, mirroring `OcrConditionNode`), persisted in
  `AppConfig.UserTools`; `ControllerSettings` (preferred sync device + hidden ids).
- New runtime — `ToolRunner` (fire-once sequence executor with `{token}` substitution),
  `ToolLauncher` (per-tool cancellation), `ControllerCatalog` / `ControllerInfo`
  (slot-driven controller enumeration + hide / sync actions).
- New UI — `ToolsList`, `ToolEdit` (page) + `ToolActionEditDialog`, `ControllerManageControl`
  (Manage + Select modes), plus the in-window `ToolEditPage` wiring in MainWindow.
- `StoredInputBinding` gained an optional `Components` list (combo), with combo-aware
  recording / matching / chord-send in `InputBindingManager` / `InputSender`.
- Namespace / config extraction refactors and a config cache improvement.
