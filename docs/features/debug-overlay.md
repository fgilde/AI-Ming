---
title: Debug Overlay
parent: Features
nav_order: 5
---

# Debug Overlay

A topmost diagnostic panel that displays the AI pipeline's live state — FPS, inference time, detection count, current target, prediction trail.

<!-- SCREENSHOT NEEDED (../images/debug-overlay.png): Game running with the PowerAim Debug Overlay in the top-left corner, showing FPS, inference time, detection count, currently locked target. -->

## What it does

The Debug Overlay is a compact panel — small enough to fit in a corner of the screen — that updates every UI tick with:

- **FPS** — instantaneous frames per second of the AI loop
- **Inference time (ms)** — how long the last ONNX forward pass took
- **Detections** — number of detection boxes on the current frame
- **Shots Fired** — total triggers fired this session
- **Frames Processed** — running count
- **Tactical Actions** — AutoPlay actions taken this session
- **Session Duration** — `hh:mm:ss`
- **OCR readings** — every enabled OCR region with its live recognised value (hidden when OCR is off)
- **AutoPlay activity log** — a rolling list of the last ~8 strategic intents / tactical actions / OCR cues with timestamps (hidden when AutoPlay is off)

The panel **follows the capture source**: it subscribes to `ICapture.PropertyChanged` and repositions live when you change the captured monitor or process window from the title bar — no more stranded overlays after picking a non-primary monitor.

It also shows the Sticky-Aim lock state, the predicted aim point, and a tracer to the current target — so you can see at a glance what PowerAim is "thinking."

## How to enable

**Settings → Overlays → Show Debug Overlay**

You can also bind a global hotkey to flip it on/off via the keybind chip next to the toggle.

## Sent-input visualizer

**Settings → Overlays → Show sent-input visualizer** adds a live input diagram inside the debug overlay, showing exactly what PowerAim is sending — useful for confirming that triggers, mapping, and gamepad aim are emitting the inputs you expect.

The visualizer draws:

- A **keyboard + mouse** diagram — keys and mouse buttons glow as PowerAim presses them, and the mouse shows an arrow indicating the current aim-movement direction.
- An **Xbox controller** diagram — face buttons / bumpers glow, the triggers fill as they're pulled, and the sticks deflect as PowerAim moves them (e.g. the right stick during [gamepad aim]({{ '/features/gamepad-aim' | relative_url }})).

{: .tip }
Turn this on while testing a [controller mapping]({{ '/features/controller-mapping' | relative_url }}) or a charge-mode [trigger]({{ '/features/triggers' | relative_url }}) — it's the fastest way to confirm the right button is firing at the right moment without alt-tabbing.

## Tips

- **Run it once when setting up a new model.** If "Detections" stays at 0 in a game where you can see enemies, the model isn't recognizing the game's art style — switch models or use the [Models guide]({{ '/models/using-models' | relative_url }}).
- **Watch the inference-time field.** If it's consistently > 16 ms you're below 60 FPS — run the [benchmark]({{ '/models/using-models#performance-benchmark' | relative_url }}) and consider a smaller image size.
- **Combine with the FOV Overlay.** When the FOV ring is on AND the debug overlay shows detections, you can visually confirm whether targets are inside the ring or being clipped.

## Troubleshooting

- **Debug Overlay is captured by my screen recorder** — disable **Hide UI from Capture** on the Settings page if you want OBS to see it.
- **"FPS 0.0" forever** — Global Active is off, or no model is loaded. The AI loop doesn't run otherwise.
- **Inference time spikes during alt-tab** — expected; capture pauses during foreground transitions.

See also: [Session Stats]({{ '/features/session-stats' | relative_url }}) for the same numbers in a non-overlay card on the Settings page.
