# Changelog

All notable changes to PowerAim are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### Added

#### 🤖 AutoPlay (new)
- **AI-driven AutoPlay mode** that actually plays the game for you. It combines a
  fast per-frame heuristic layer (movement, strafing, aim, burst-fire, exploration,
  view-centering) with an optional **Ollama vision "strategic layer"** that looks at
  the screen every few seconds and decides high-level intent (explore / engage /
  retreat / hold / tactical).
- **Profile system.** Create multiple AutoPlay profiles, each with its own game
  description, Ollama model, decision interval, and a set of actions. Actions map to
  **keyboard and/or controller** inputs and have types — *Continuous*, *Instant*,
  *Toggle*, *Modifier* — so the engine knows how to hold/tap each one.
- **Live game context fed to the model:** current enemy count, OCR-read ammo/health,
  and a rolling history of recent decisions (with a "vary your choices" instruction so
  the bot doesn't get stuck or oscillate).
- **Per-game auto profile switching** — AutoPlay (and aim) profiles can bind to a
  process name and switch automatically when you alt-tab between games.
- **Learning mode:** record your own playstyle (state → action samples) and bias the
  bot toward your habits, with an adjustable strength.
- **Ready-to-use "FPS Default" profile** with move/strafe/sprint/jump/shoot/aim/reload/
  crouch plus tactical *interact*, *melee*, *grenade*, and *switch weapon* actions.
- **In-app profile editor page** (Back / Cancel / Save) instead of a popup dialog.

#### 🔎 HUD OCR (new)
- **On-screen text reading.** Define named OCR regions over your HUD (e.g. *ammo*,
  *health*, *armor*) and PowerAim reads them live.
- **OCR-driven AutoPlay cues:** low ammo → reload, low health → brief retreat.
- **Conditional triggers via OCR** — gate an action trigger on a HUD reading.
- **Aim auto-disengage on HUD state** — pause aim assist while an OCR reading matches
  a rule (e.g. "scoped", "knife"), per game.

#### 🎯 Recoil & Anti-Recoil (new)
- **Recoil-pattern recorder** — spray a wall and PowerAim records the recoil curve,
  with a live preview of the drift you're capturing.
- **Pattern library + playback** — name patterns per weapon, preview them, arm one for
  automatic recoil compensation, with an adjustable strength.
- **5-second "get ready" countdown** (with on-screen counter) before recording or
  sensitivity calibration begins, so you can focus the game and start firing. Cancellable.
- **Sensitivity calibration wizard** that measures your in-game sensitivity and suggests
  a damping factor.

#### 🖥️ UI (new)
- **Completely redesigned interface** on .NET 10 with a modern Fluent look: new title
  bar, sidebar navigation, card-grouped sections, and light/dark/system theming.
- **Accent color picker** — pick your own accent (with a separate accent shown while
  Global Active is on); it drives the whole theme.
- **Global search** (Ctrl+F) to jump to any setting.
- **Crosshair overlay** and a **debug overlay** with live stats and an optional
  **input visualizer** (keyboard + mouse + controller diagrams of what's being sent).
- **Toggle notifications** — a brief on-screen notice when you flip a toggle via its
  hotkey, so you get feedback without alt-tabbing.
- **In-app help** (WebView2) and per-control help texts.
- **Full localization** in 9 languages (English, German, Spanish, French, Italian,
  Russian, Turkish, Ukrainian, Chinese).

#### 🎮 Controller & input (new)
- **Controller ↔ keyboard mapping engine** with profiles, a visual controller/keyboard
  diagram, a default FPS preset, and a master on/off hotkey.
- **Use controller for aim** — drive a virtual Xbox right-stick instead of mouse motion
  (via ViGEm), with auto-gating until a sender is configured.
- **Detected-gamepad list** with hide/show, ViGEmBus-missing warnings, and a
  "Restart as admin" shortcut for device hiding.

#### ⚙️ Engine & capture (new)
- **DXGI/Vortice screen-capture backend**, **dynamic model input size**, **multi-class /
  target-class support**, a refactored prediction pipeline (velocity + Kalman),
  sticky-aim selection, detection-region masks, a **performance benchmark** with FPS cap,
  and a **replay buffer**.

#### Other
- **New setting: "Keybinds only while Global Active."** When on, every global keybind
  only fires while Global Active is enabled. The Global Active hotkey itself is always
  exempt. Off by default.

### Changed
- **More responsive AutoPlay defaults:** new profiles default to a `0.5s` decision
  interval (was `1.0s`); the strategic loop's lower bound was reduced so faster intervals
  take effect.
- **Aim correction now uses the model's real input size** instead of a hard-coded value,
  so targeting stays accurate on models that aren't 640px.
- **FOV Size / Dynamic FOV Size slider maximums now track the current Image Size** (e.g.
  740 → 740, 1200 → 1200), updated live, instead of a fixed ceiling.

### Removed
- **ESP Opacity and FOV Opacity sliders.** Overlay colors now carry their own alpha
  channel, so transparency is set directly from the color pickers for detection boxes,
  tracers, confidence text, and the FOV circle.

### Fixed
- **Header keybind "min time" popup now opens.** The window's click-to-drag handler was
  swallowing the click before it reached the control; key changers are now exempt from
  window dragging (and clicking a header keybind no longer drags the window).
