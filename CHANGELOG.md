# Changelog

All notable changes to PowerAim are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0.2]

### Added

- **Per-profile "Use Ollama" toggle in AutoPlay.** Each AutoPlay profile now has a
  toggle that decides whether the Ollama strategic layer runs alongside the
  heuristic. With it off, AutoPlay drives the game purely from the heuristic +
  OCR cues — no LLM polling, no screenshot capture, no Ollama install required.
  Flipping it mid-session is honoured live: switching off settles the intent to
  default and idles the loop on a 5 s poll; switching back on restarts the
  strategic layer from the next tick. The profile description in the list
  surfaces the active mode (`(N actions, heuristic only)` vs. `(N actions,
  moondream)`).
- **Inference-GPU picker in the title bar.** A new chip in the header lets you
  choose which DXGI adapter runs ONNX inference. On multi-GPU systems (e.g.
  Intel iGPU + RTX 4090) you can keep detection off the GPU that's running the
  game, which cuts the input lag from competing for cycles. The pill shows the
  shortened adapter name (e.g. "RTX 4090"); the popup lists every detected GPU
  with VRAM, has Refresh + "Open diagnostic log" buttons, and writes a
  per-enumeration log to `%TEMP%\PowerAim_GpuEnum.log` so weird hardware setups
  are debuggable without a debugger. The selected device id is persisted as
  `AISettings.InferenceGpuDeviceId` and reloads the active model immediately on
  change.
- **Ollama UX in the AutoPlay profile editor.**
  - Status indicator can now **start Ollama from the UI** (probes
    `LocalAppData\Programs\Ollama`, `Program Files`, then `PATH`) or open
    `ollama.com/download` if it isn't installed, depending on whether the
    executable was found.
  - Indicator **auto-detects externally-started Ollama** via a 4 s
    `DispatcherTimer` — no more mashing Refresh after starting `ollama serve`
    in a terminal.
  - The Ollama-model TextBox is now an **editable ComboBox** populated with the
    locally installed models (from `/api/tags`) plus a curated list of vision
    models, so you can pick or type. A new **Pull** button next to it streams
    `ollama pull <model>` progress (manifest → downloading → verifying →
    success) into a live status line below the row.

### Changed

- **OnnxExecutionProvider plumbing** — `AppendExecutionProvider` /
  `SetExecutionProvider` / `CanWork` now accept a `deviceId` int that's passed
  straight to `AppendExecutionProvider_CUDA(deviceId)` /
  `AppendExecutionProvider_DML(deviceId)`. The factory and `PredictionLogic`
  read `AISettings.InferenceGpuDeviceId` and route inference accordingly. Old
  callers default to device 0 (the previous behaviour).
- **OllamaClient HttpClient is now process-wide static** so that a status
  indicator getting disposed (navigation, language-change rebuild) can't kill
  the shared HTTP pipe other callers are still using. Per-instance `Dispose()`
  is a no-op now; the static client lives for the process lifetime.

### Fixed

- **Editable ComboBox no longer renders blank after a selection.** The global
  ComboBox template in `Theme.xaml` was missing the WPF-mandatory
  `PART_EditableTextBox` named part, so `IsEditable="True"` controls only ever
  rendered `SelectionBoxItem` — which is empty for free-typed text or while a
  TwoWay `Text` binding was mid-sync. The template now overlays a
  `PART_EditableTextBox` on the ToggleButton's content column (transparent
  background, chevron stays clickable), with a trigger that swaps visibility on
  `IsEditable` and a style on the SelectionBoxItem presenter that hides it
  when the TextBox is active. The AutoPlay model ComboBox finally shows what
  you picked or typed.
- **`Cannot access a disposed object` on Ollama start.** When the AutoPlay
  status indicator was reloaded across a language change or navigation, the
  previous instance's `Dispose()` had already torn down the shared HttpClient
  — any in-flight `IsAvailable` / `GetModels` call on a sibling control then
  blew up. Resolved by the static-HttpClient change above plus making the
  control's `OnUnloaded` not dispose the client at all.
- **Status indicator gets stuck on "Disconnected"** when the user started
  `ollama serve` outside the app. Fixed by the periodic poll above.

### Internal

- New `AILogic/GpuAdapterEnumerator` — Vortice DXGI walker that lists every
  hardware GPU (filters the Microsoft Basic Render / WARP software adapter via
  raw `DXGI_ADAPTER_FLAG_SOFTWARE` bit instead of an enum cast that can throw
  on some hosts), with VRAM + Vendor/Device IDs. Caches successful runs,
  re-tries on failure, mirrors a structured per-adapter log to a temp file.
- `OllamaClient` gained `FindOllamaExecutable()`, `TryStartServer()`,
  `PullModelAsync(model, IProgress<PullProgress>, ct)` (NDJSON streaming
  parser) and the `IsInstalled` / `DownloadUrl` / `RecommendedVisionModels`
  static surface used by the new UX.
- `OllamaStatusIndicator` gained Start / Install buttons and a 4-second
  `DispatcherTimer` poll, both stopped on `Unloaded` so they don't leak across
  UI rebuilds.

## [2.0.0.1]

### Added

- **OCR regions overlay** with on-screen editing. A new toggle (with hotkey support)
  in the HUD OCR settings paints a labelled rectangle on screen for every enabled
  OCR region, showing the recognized value live. An "Edit" checkbox on the overlay
  turns the rectangles into drag-to-move + resize-from-corner handles that write
  straight back to the region's coordinates — no more eyeballing X/Y/W/H numbers
  in the dialog. A "+" button (visible in edit mode) creates a fresh region at the
  screen centre. The OCR regions configuration dialog now also has a **"Visual
  edit"** button that hops directly into the overlay with edit mode on.
- **Debug overlay OCR column.** Next to the existing FPS / inference / detection
  stats, the debug overlay now lists every enabled OCR region with its live
  recognized value. Hidden when OCR is off or no regions are configured.
- **About page expansion.** New "Project" card with the source code, issues,
  documentation and author website links, plus a "Releases" card that pulls every
  release from GitHub and shows version · publish date · total download count ·
  one-click "open on GitHub". Loads lazily on first About visit and caches for the
  session.
- **Aim-Disengage dialog UX.** Column headers (OCR region · Condition · Value ·
  Game), inline placeholder hints in the value + game-pattern boxes, and a help
  banner with a concrete worked example ("region 'state' contains 'scoped' → aim
  assist pauses while the scope is active").

### Changed

- **LG Hub Downloader dialog redesigned** with the rest of the app's Fluent style
  (proper titlebar, surface-card mirror list with icons, sub-labels, chevron
  affordance, Fluent footer). The DragMove handler is on the titlebar instead of
  the whole window, matching the other dialogs.
- **About → Releases shows a real total** — the sum of every asset's downloads per
  release (DirectML zip + CUDA zip + Installer.exe), not a single asset.

### Fixed

- **Scroll bar grabbing now works.** The window's 6px `WindowChrome` resize border
  used to claim the last few pixels of any scroll bar — hovering over the thumb
  flipped to the resize cursor and clicks initiated a window resize instead of
  scrolling. The scroll bar style now opts the entire bar out of chrome hit-
  testing, so scroll clicks land on the thumb and only the very edge outside the
  bar still resizes.
- **Razer Synapse detection ignored Synapse 4.** The "Razer Synapse" mouse-movement
  method only probed the legacy `Razer Synapse` process name, so Synapse 4 (which
  ships `RazerAppEngine` plus a `Razer Synapse Service` background process)
  always reported "not installed". The check now probes every documented Razer
  Synapse process name — `RazerAppEngine`, `Razer Synapse Service`,
  `Razer Synapse Service Process`, `Razer Synapse`, `RazerCentralService` and
  `Razer Central` — and counts any of them as "installed and running". The two
  leftover `"Aimmy"` titles in the Razer dialogs were renamed to `"PowerAim"`.
- **No-model screen no longer flashes at startup.** The "no model loaded" card now
  shows a loading spinner while the initial model load is in flight, and only
  flips to the empty-state message + "Load default model" button once the load
  actually resolves with no model.
- **Settings cards no longer duplicate on language switch.** The Active-Processes,
  Overlays, Stats, HUD-OCR, Replay and AutoPlay-Learning cards used to append
  their content on each language change instead of rebuilding. Each of them now
  clears first. The stats `DispatcherTimer` and the `ReplayBuffer` /
  `AutoPlayLearningModel` `PropertyChanged` subscriptions are torn down before
  reassignment too, so language switches no longer stack orphan timers /
  handlers on detached labels.
- **"Hidden sections" pill reappears after a language switch.** Toggling section
  visibility (the per-card × button) used to silently lose the pill after the UI
  was rebuilt by a language change. The page-layout managers are now preserved
  across rebuilds (the FluentCard borders + their already-attached chrome
  survive), so the pill stays bound to the same manager the × buttons drive.
- **Header keybind "min time" popup opens.** The Global Active hotkey's min-time
  configuration popup couldn't open because the window's click-to-drag handler
  swallowed the click before it reached the control. Key changers are now exempt
  from window dragging — and clicking a header keybind no longer accidentally
  starts dragging the window.
- **AutoPlay default profile + bundled default model.** "FPS Default" now ships
  with all four tactical actions (interact, melee, grenade, switch weapon) plus
  the four extra triggers from the shipped config (Primary Fire, Rapid Fire,
  Auto Throw, Ping). The bundled `default.onnx` model lives under
  `Resources/` and the no-model card's "Load default model" button copies it
  into `bin/models/` on demand.
- **AutoPlay strategic layer gets real game context.** The Ollama vision prompt
  now includes the current enemy count, OCR-read ammo/health (when matching
  regions are configured) and a short rolling history of recent decisions, with
  an instruction to vary them — so the bot stops oscillating or getting stuck in
  one mode.

### Internal

- New constants in `ApplicationConstants` for the project URLs (`RepoUrl`,
  `ReleasesUrl`, `IssuesUrl`, `AuthorUrl`, `Copyright`) so the About page and any
  future "open on GitHub" buttons share one source of truth.
- New `OcrRegionsOverlay.OpenInEditMode()` convenience entry point used by the
  OCR-regions dialog's "Visual edit" button.
- The page-layout-manager dictionary is no longer cleared on every `CreateUI`
  rebuild — managers reuse their existing instrumentation, which avoids the
  empty-new-manager / orphaned-chrome state that broke the hidden-sections pill
  after a language switch.



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
