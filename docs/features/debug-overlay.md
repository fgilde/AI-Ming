---
title: Debug Overlay
parent: Features
nav_order: 5
---

# Debug Overlay

A topmost diagnostic panel that displays the AI pipeline's live state — FPS, inference time, detection count, current target, prediction trail.

![Debug overlay floating over a game](../images/debug-overlay.png)
<!-- SCREENSHOT NEEDED: Game running with the PowerAim Debug Overlay in the top-left corner, showing FPS, inference time, detection count, currently locked target. -->

## What it does

The Debug Overlay is a compact panel — small enough to fit in a corner of the screen — that updates every UI tick with:

- **FPS** — instantaneous frames per second of the AI loop
- **Inference time (ms)** — how long the last ONNX forward pass took
- **Detections** — number of detection boxes on the current frame
- **Shots Fired** — total triggers fired this session
- **Frames Processed** — running count
- **Tactical Actions** — AutoPlay actions taken this session
- **Session Duration** — `hh:mm:ss`

It also shows the Sticky-Aim lock state, the predicted aim point, and a tracer to the current target — so you can see at a glance what PowerAim is "thinking."

## How to enable

**Settings → Overlays → Show Debug Overlay**

You can also bind a global hotkey to flip it on/off via the keybind chip next to the toggle.

## Tips

- **Run it once when setting up a new model.** If "Detections" stays at 0 in a game where you can see enemies, the model isn't recognizing the game's art style — switch models or use the [Models guide]({{ '/models/using-models' | relative_url }}).
- **Watch the inference-time field.** If it's consistently > 16 ms you're below 60 FPS — run the [benchmark]({{ '/models/using-models#performance-benchmark' | relative_url }}) and consider a smaller image size.
- **Combine with the FOV Overlay.** When the FOV ring is on AND the debug overlay shows detections, you can visually confirm whether targets are inside the ring or being clipped.

## Troubleshooting

- **Debug Overlay is captured by my screen recorder** — disable **Hide UI from Capture** on the Settings page if you want OBS to see it.
- **"FPS 0.0" forever** — Global Active is off, or no model is loaded. The AI loop doesn't run otherwise.
- **Inference time spikes during alt-tab** — expected; capture pauses during foreground transitions.

See also: [Session Stats]({{ '/features/session-stats' | relative_url }}) for the same numbers in a non-overlay card on the Settings page.
