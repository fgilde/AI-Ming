---
title: Setup Best Practices
parent: Getting Started
nav_order: 4
---

# Setup Best Practices

A practical guide for getting PowerAim into a *good* state — not just a *working* state. Read after [Your First Aim]({{ '/getting-started/first-aim' | relative_url }}); each section assumes you can already see PowerAim move the mouse.

## Recommended setup order

Setting things up in this order avoids re-tuning later, because every step builds on the one before it.

1. **Pick a capture source.** Title bar → capture selector. Pick the monitor the game runs on, or capture the game's window directly. The crosshair, debug overlay and detection pipeline all follow this choice live, so getting it right first saves alt-tabbing.
2. **Load a model.** Models & Configs → pick a model for your game (game-specific beats Universal). Watch the model name appear in the bottom status bar.
3. **Pick a GPU for inference.** Title bar → GPU pill. On multi-GPU systems, choose the card *not* running the game. See [Performance](#performance) below.
4. **Calibrate sensitivity.** Aim Tools → [Calibration Wizard]({{ '/features/calibration-wizard' | relative_url }}). Run it once *per game* and *per Movement Method* — the values aren't portable.
5. **Set up triggers.** Add at least a Primary Fire trigger with `NeedsDetection = true` so PowerAim never fires into empty air.
6. **Record recoil patterns** for the weapons you use most ([Recoil Patterns]({{ '/features/recoil-patterns' | relative_url }})).
7. **Create Anti-Recoil profiles** that reference those patterns ([Anti-Recoil]({{ '/features/anti-recoil' | relative_url }})). Bind each one to a free hotkey *and* wire up the OCR weapon switch so they self-activate.
8. **(Optional) Set up an AutoPlay profile** for the game ([AutoPlay]({{ '/features/autoplay' | relative_url }})).

If you skip ahead to step 7 before doing step 4, your "AK-47" pattern will need re-recording because the in-game sensitivity it was captured at no longer matches.

## The per-game profile pattern

Every profile type (Trigger / Mapping / AutoPlay / Anti-Recoil) has a `MatchProcess` field. Use it.

**Workflow:** keep one of each per game you play. Enable them all; the process filter decides which one is live at any moment. When you alt-tab from CS2 to Valorant, the Valorant profiles take over without a single click.

For Anti-Recoil specifically, combine `MatchProcess` with the OCR weapon switch:

1. Define an OCR region over your in-game weapon name display.
2. Per-weapon Anti-Recoil profile: set `MatchProcess` to the game(s) it applies to, set `OcrRegionName` to that region, set `WeaponMatch` to a substring (`AK`, `Vandal`, `Operator`), tick `AutoSwitchOnOcr`.
3. Active profile flips automatically when you swap weapons mid-match.

For background on `MatchProcess` syntax, see [Per-game profiles]({{ '/configuration/per-game-profiles' | relative_url }}).

## Performance

### Picking an inference GPU

The title-bar GPU pill writes `AISettings.InferenceGpuDeviceId`. Rule of thumb:

- **Single-GPU desktop with iGPU + dGPU:** put inference on the integrated GPU if the model is small (Universal-sized YOLOv8s/n). Frees the dGPU for the game and removes the inference vs. render contention that adds input lag.
- **Dual dedicated GPUs:** put inference on the secondary card.
- **Single dGPU only:** leave it at `0`. Drop image size before changing devices.

The diagnostic log at `%TEMP%\PowerAim_GpuEnum.log` shows every adapter PowerAim sees with VRAM + Vendor/Device IDs — useful when an expected GPU is missing from the picker.

### Picking a model size

Bigger models = better detections, more inference time. Watch the Debug Overlay's **Inference time** field:

| Inference time | Verdict |
|:---------------|:--------|
| < 8 ms | Plenty of headroom — try a bigger model for better recall. |
| 8–16 ms | Sweet spot for 60+ FPS games. |
| > 16 ms | You're below 60 FPS of *AI processing*. Drop model size or image size. |

The [Models guide]({{ '/models/using-models' | relative_url }}) covers benchmark + sizes in detail.

### FPS cap

If you don't need the AI loop running faster than the game does, cap it. Settings → AI Settings → **FPS Cap** removes the busy-loop overhead and frees GPU cycles for the game. A cap a few frames above your monitor's refresh rate is usually fine.

## Anti-detection

PowerAim is built for accessibility and training; it does not try to defeat anti-cheats. That said, perfectly deterministic input is suspicious-looking even to humans, so a small amount of jitter improves the *feel* of the assist as well as masking patterns.

AutoPlay-specific knobs (per profile):

- **`MouseJitterPx = 1–4`** — subtle, "human-ish" wobble on AutoPlay aim. Bump it higher for noticeably noisy aim. `0` keeps determinism.
- **`KeyDelayJitterMs = 5–15`** — keys don't all land on perfectly even tick boundaries.
- **`MouseSensScale`** — tune per-game: high-sens game → `0.7`, low-sens game → `1.4`. Lets one profile cover several games without re-recording actions.

For the main aim path, EMA smoothening + a non-linear movement path (Cubic Bezier / Exponential) already give the assist a natural-feeling acceleration curve. Don't crank EMA past `0.4` or aim becomes laggy.

## Ollama vs. heuristic-only AutoPlay

Each AutoPlay profile has a `UseOllama` toggle. Pick by use-case:

- **Heuristic only (`UseOllama = false`)**
  - No LLM round-trip, no screenshot capture for the LLM.
  - Movement, strafing, burst-fire, view-centering, exploration, OCR-driven reload/retreat all still run.
  - Latency-critical; runs fine on hardware that can't host Ollama.
  - Best for: pure FPS reflex play, low-end machines, the moment you alt-tab and don't want LLM traffic.

- **Ollama enabled (default)**
  - Adds a "strategic intent" layer (every 0.5–2 s) that biases the heuristic — *explore / engage / retreat / hold / tactical*.
  - Helpful when you want context-aware behaviour: retreat on low health, reposition between fights, vary between modes.
  - Best for: games where positioning matters more than reaction time.

Toggle is honoured live. You can leave Ollama disabled for a sprint and re-enable it for a slower phase without restarting AutoPlay.

## Multi-monitor and windowed games

- **Pick the monitor PowerAim captures from the title bar selector.** It's a per-config setting, not a per-game one — change it when you change which monitor the game lives on.
- **Crosshair + Debug Overlay follow the capture source live.** No need to restart PowerAim when you move the game to a different monitor.
- **Windowed games:** capture the window directly (process selector in the title bar) rather than the whole monitor. The capture area, crosshair, FOV ring and overlays all snap to the window's client rect.
- **Borderless fullscreen** behaves like a normal monitor capture from PowerAim's perspective.
- **DXGI capture exclusion** ("Hide UI from Capture" on the Settings page) hides PowerAim's overlays from OBS / Discord / screen recorders. Turn it off if you *want* OBS to see the crosshair.

## Common pitfalls

- **Tuning sensitivity in MouseEvent, then switching to SendInput** — re-run the [Calibration Wizard]({{ '/features/calibration-wizard' | relative_url }}). Different backends emit slightly different deltas.
- **Master Anti-Recoil toggle on, but no profile selected** — nothing fires. Open the profile list, pick one (its row toggle goes on).
- **Per-profile hotkey "doesn't work"** — confirm the binding chip on the *row* shows the key, not just a global hotkey on the master toggle. They're different bindings.
- **OCR weapon auto-switch never triggers** — the region needs to *actually contain text*. Open the on-screen OCR overlay, watch what the recognised value looks like, and use a substring you actually see (not what the HUD says — what OCR reads).
- **Inference time spikes whenever you alt-tab** — expected; DXGI capture pauses during foreground transitions.
- **AutoPlay locks W on forever** — a Continuous action got stuck because the model never picked anything else. Disable AutoPlay → keys release. Lower `DecisionInterval`, raise Temperature, or refine the GameContext prompt.
- **Crosshair off-centre after picking a non-primary monitor** — that's fixed now (overlays follow `ICapture.CaptureArea` live). If you still see it, confirm the title bar capture selector matches the game's monitor.

## Pre-flight checklist before a game session

A 30-second sanity check before launching:

1. Capture source matches the game's monitor / window.
2. Correct model loaded (status bar at the bottom).
3. Inference GPU is the one you want (title bar pill).
4. The right MatchProcess profiles are enabled for this game.
5. Movement Method matches the game's input policy (Gamepad for gamepad-only titles).
6. Global Active hotkey is bound to a free key.
7. (For Anti-Recoil) at least one profile is selected, or OCR + hotkey are wired up to switch automatically.
