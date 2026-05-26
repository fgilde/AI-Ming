# Documentation Screenshots — Checklist

This folder holds the screenshots referenced by the docs. This file tracks what's already wired up and what's still missing.

Recommended capture settings:

- **PNG** (no JPEG artefacts on UI text)
- **Dark mode**, **Purple theme** (PowerAim's default) for consistency
- Show **real data** when possible — a window with one model loaded and a couple of triggers/profiles beats an empty one

When you take a still-missing screenshot, drop it in this folder with the exact filename listed below and the corresponding `<!-- SCREENSHOT NEEDED (...) -->` comment in the docs can be turned back into an `![alt](../images/<file>.png)` line.

---

## ✅ Done — wired into the docs

| Filename | Used on |
|:---------|:--------|
| `aim-tools-page.png` | `index.md` (hero), `features/aim-assist.md`, `features/triggers.md`, `features/anti-recoil.md`, `getting-started/first-aim.md` |
| `settings-page.png` | `configuration/settings-overview.md`, `getting-started/installation.md` (Switch DirectML/CUDA) |
| `gamepad-settings-vigem-ok.png` | `getting-started/installation.md` |
| `gamepad-tester.png` | `features/controller-mapping.md` |
| `mapping-page.png` | `features/controller-mapping.md` |
| `mapping-editor.png` | `features/controller-mapping.md` |
| `trigger-editor.png` | `features/triggers.md` |
| `calibration-wizard.png` | `features/calibration-wizard.md` |
| `recoil-patterns-dialog.png` | `features/recoil-patterns.md` |
| `ocr-regions-dialog.png` | `features/ocr.md` |
| `autoplay-page.png` | `features/autoplay.md` |
| `autoplay-edit-profile.png` | `features/autoplay.md` (Profiles section) |
| `autoplay-edit-action.png` | `features/autoplay.md` (Actions section) |
| `tools-page.png` | `configuration/keybinds-hotkeys.md` (Magnifier + HWID Spoofer) |

## ⬜ Still needed

These pages currently have a `<!-- SCREENSHOT NEEDED (../images/<file>) -->` comment instead of an image.

### In-app dialogs / pages

| Filename | Description |
|:---------|:------------|
| `models-tab.png` | Models tab: local model list (left) + downloadable models strip (right), one model selected, Models/Configs toggle at top |
| `capture-source-selector.png` | Title bar's capture-source picker open, showing monitors / windows with thumbnails |
| `benchmark-results.png` | Performance Benchmark results dialog: per-size table (Resolution, FPS, ms, GPU%), recommended size highlighted, Apply/Cancel |
| `detection-masks-dialog.png` | Detection Masks dialog: masks list (left), preview canvas with red mask overlays (right), sliders below |
| `session-stats-card.png` | Settings page → Stats card: FPS, inference time, detections, shots fired, frames processed, session duration, Reset |
| `replay-card.png` | Settings page → Replay Buffer card: toggle, buffer-length slider, JPEG-quality slider, Save/Clear, "Frames buffered: N" status |
| `hidden-controllers-page.png` | Hidden Controllers subpage: 2–3 HID devices, each with name, status, Disable/Enable buttons |

### In-game overlays (need a game/demo running)

| Filename | Description |
|:---------|:------------|
| `detected-player-overlay.png` | Game with detected-player boxes drawn over enemies (ESP overlay) |
| `crosshair-overlay.png` | In-game with PowerAim's custom Plus crosshair (purple, 16 px, 4 px gap) over the game's reticle |
| `debug-overlay.png` | Game with PowerAim's Debug Overlay in a corner: FPS, inference time, detection count, locked target |
| `fov-ring.png` | Game with PowerAim's FOV ring drawn centered, showing radius + faint accent-color tint |

---

## Tips for taking the missing screenshots

1. **Set theme to Purple (default)** — Settings → Theme → Purple — to match the existing 14.
2. **Hide your username** if it appears in file paths (OCR / replay folder textbox). Use a sample like `Player`.
3. **For the in-game shots** (`detected-player-overlay`, `crosshair-overlay`, `debug-overlay`, `fov-ring`), use a free-to-distribute environment — CS2 workshop maps, the Aim Lab demo, etc. Avoid anything that looks like active cheating in a competitive title.
4. **Crop tightly.** The docs render at ~800 px wide; tight crops stay crisp.
