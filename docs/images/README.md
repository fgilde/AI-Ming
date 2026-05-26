# Documentation Screenshots — Checklist

This folder holds the screenshots referenced by the docs. Every doc page has placeholder `![](images/foo.png)` references with an HTML comment describing what to capture. This file lists everything in one place so you can knock out screenshots in a single session.

Screenshots are recommended to be:

- **PNG** (no JPEG artefacts on UI text)
- **1600 × 900** target resolution (looks crisp in light + dark mode)
- **Dark mode** for consistency (PowerAim's default theme)
- Show **real data** when possible — a clean PowerAim window with one model loaded and one or two triggers is more useful than an empty one

When you've taken a screenshot, drop it in this folder with the filename listed below. The docs will pick it up automatically.

---

## Landing & Getting Started

| Filename | Description |
|:---------|:------------|
| `main-window.png` | Top-level shot of PowerAim with a model loaded, sidebar visible, AimConfig + ESPConfig + AimAssist cards on screen |
| `settings-switch-build.png` | Settings page → UISettings card with the "Switch to DirectML / CUDA" button highlighted |
| `gamepad-settings-vigem-ok.png` | Gamepad settings page with ViGEm selected as send mode and the green "ready" status credit visible |
| `models-tab.png` | Models tab showing the local model list (left) and the downloadable models strip (right), one model selected |
| `capture-source-selector.png` | Title bar's capture-source picker open, showing monitors / windows with thumbnails |
| `aim-assist-card.png` | Aim Assist card with the Aim Key Bindings chip in focus |
| `detected-player-overlay.png` | Game with detected-player boxes drawn over enemies (ESP overlay) |

## Features

| Filename | Description |
|:---------|:------------|
| `aim-tools-page.png` | Aim Tools page open, Aim Assist card top-left and AimConfig card right column |
| `triggers-list.png` | Aim Tools page scrolled to the Triggers card, 2–3 triggers in the list with Edit button visible |
| `trigger-editor.png` | Trigger editor dialog open — Name field, key/action chips, intersection-check dropdowns, live head-area preview |
| `anti-recoil-card.png` | Aim Tools → AntiRecoil card showing master toggle, BETA toggle, pattern toggle, legacy sliders |
| `crosshair-overlay.png` | In-game with PowerAim's custom Plus crosshair (purple, 16 px, 4 px gap) drawn over the game's reticle |
| `debug-overlay.png` | Game with PowerAim's Debug Overlay in a corner showing FPS, inference time, detection count, locked target |
| `fov-ring.png` | Game with PowerAim's FOV ring drawn centered, showing radius and a faint accent-color tint |
| `session-stats-card.png` | Settings page → Stats card with FPS, inference time, detections, shots fired, frames processed, etc. |
| `replay-card.png` | Settings page → Replay Buffer card with toggle, buffer-length slider, JPEG-quality slider, Save/Clear buttons, "Frames buffered: 73" status |
| `ocr-regions-dialog.png` | OCR Regions dialog — master/detail layout: list of regions on the left, live preview + sliders on the right |
| `calibration-wizard.png` | Calibration Wizard's "Welcome" step open over the main window |
| `detection-masks-dialog.png` | Detection Masks dialog — masks list on the left, preview canvas with red mask overlays, sliders below for precision edits |
| `recoil-patterns-dialog.png` | Recoil Patterns dialog — pattern list (left), drift-curve preview (right), name field + record button at top |
| `autoplay-page.png` | AutoPlay sidebar entry selected: Ollama status indicator, master toggle, Ollama settings sliders, profile list |
| `mapping-page.png` | Mapping sidebar entry: Active toggle, direction picker (Both / KB→Pad / Pad→KB), profile list, Engine status line |
| `mapping-editor.png` | Mapping editor with several mappings, slot-style chips for source/target, activator dropdown, stick-tuning sliders top-right |
| `gamepad-tester.png` | Gamepad Tester window: Xbox controller silhouette with two columns — physical (left) + virtual (right), tap-indicators visible |
| `hidden-controllers-page.png` | Hidden Controllers subpage: 2–3 HID devices listed, each with a name, status, and Disable/Enable buttons |

## Models

| Filename | Description |
|:---------|:------------|
| `benchmark-results.png` | Performance Benchmark results dialog: per-size table (Resolution, FPS, ms, GPU%), recommended size highlighted, Apply/Cancel buttons |

## Settings

| Filename | Description |
|:---------|:------------|
| `settings-page.png` | Full Settings page screenshot, scrolled to show as many cards as possible |

---

## Tips for taking the screenshots

1. **Set theme to Purple (default) for consistency** — Settings → Theme → Purple.
2. **Maximize the window** to 1600×900 so screenshots have consistent aspect ratio.
3. **Hide your username** if it appears anywhere (file paths in OCR / replay folder textbox). Use a sample username like `Player`.
4. **Use a real model name** — bundled `Universal.onnx` is fine; community models with a "by [Handle]" name look more authentic.
5. **For the in-game screenshots** (`detected-player-overlay`, `crosshair-overlay`, `debug-overlay`, `fov-ring`), use a free-to-distribute environment — CS:GO custom workshop maps, the Aim Lab demo, etc. Avoid screenshots that could be confused with active cheating in a competitive game.
6. **Crop tightly.** The docs render at ~800 px wide; tight crops are crisper than wide ones.
