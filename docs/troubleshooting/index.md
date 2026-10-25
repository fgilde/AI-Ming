---
title: Troubleshooting
nav_order: 7
has_children: true
permalink: /troubleshooting/
---

# Troubleshooting

When things go wrong, start here. Each page walks through diagnostics for a specific symptom.

## By symptom

- **[Gamepad Not Detected]({{ '/troubleshooting/gamepad-not-detected' | relative_url }})** — virtual gamepad doesn't appear in games, ViGEm/HidHide issues
- **[Model Won't Load]({{ '/troubleshooting/model-wont-load' | relative_url }})** — ONNX errors, missing classes, "model failed to load"
- **[Low FPS]({{ '/troubleshooting/low-fps' | relative_url }})** — AI loop running below 60 FPS, lag, input feels heavy
- **[OCR Not Working]({{ '/troubleshooting/ocr-not-working' | relative_url }})** — Tesseract data, region setup, garbage results
- **[Controller Aim Has No Effect]({{ '/troubleshooting/controller-aim-no-effect' | relative_url }})** — virtual right-stick doesn't move the in-game camera

## General first-aid

Before diving into a specific page, try these:

1. **Toggle Global Active off and back on.** Resets the AI loop, capture, and senders.
2. **Reload the config.** Cogwheel → Open Quick Config. Clears any in-memory state divergence.
3. **Restart PowerAim.** Especially useful after driver installs (ViGEm, HidHide).
4. **Restart as admin.** Title bar → "Restart as admin" button. Some operations (Device Disable, HidHide management) require elevation.
5. **Check the in-app log.** Sidebar → Logs. PowerAim writes diagnostic output there.
6. **Run the Performance Benchmark.** Models tab → Run Benchmark. Confirms inference path is healthy.

If none of those help, find the relevant troubleshooting page below — most include checklists you can walk through.

## Reporting bugs

Found something the docs don't cover? Open an issue at [github.com/fgilde/AI-Ming/issues](https://github.com/fgilde/AI-Ming/issues). Include:

- PowerAim version (visible on the About page)
- Build flavor (DirectML / CUDA)
- Windows version
- GPU + driver version
- A short repro
- Bonus: a [replay buffer export]({{ '/features/replay-buffer' | relative_url }}) of the moment things went wrong
