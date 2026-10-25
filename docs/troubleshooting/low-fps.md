---
title: Low FPS
parent: Troubleshooting
nav_order: 3
---

# Low FPS

Symptoms:

- Debug Overlay shows FPS below 30
- Aim feels "draggy" or laggy
- Predictions overshoot consistently
- Inference time spikes above 30 ms

## Checklist

### 1. Run the benchmark

**Models & Configs → Models card → Run Benchmark**.

The benchmark measures FPS / inference time / GPU% across 320 / 416 / 512 / 640 / 800 image sizes and recommends the highest size that still clears 60 FPS.

Pick the recommended size. If even 320 doesn't clear 60 FPS, your hardware is below PowerAim's playable threshold for this model — try a smaller model.

### 2. Switch builds (DirectML ↔ CUDA)

If you're on NVIDIA, the **CUDA build** is typically 10–25% faster than DirectML. Settings → UI Settings → Switch to CUDA.

If you're on AMD or Intel, stick with DirectML — CUDA won't run.

### 3. Lower the image size

**Aim Tools → Model Settings → Image Size Override**.

`320` is the safe lower bound; `416` is a good middle ground; `640` is the default for dynamic models. Smaller = much faster but lower-detail detections.

### 4. Cap Max Inference FPS

**Aim Tools → PredictionConfig → Max Inference FPS**.

Counter-intuitively, capping at 60 or 90 can *help* — the AI loop becomes deterministic instead of running at whatever the GPU can momentarily produce. This also lets your GPU breathe between frames.

### 5. Close background GPU consumers

- Chrome / Edge with lots of tabs
- OBS Studio with the game window source
- Discord with Hardware Acceleration on
- NVIDIA Broadcast / Camo / other camera virtualization

PowerAim shares the GPU with anything that uses Direct3D. The more it has to share with, the slower inference gets.

### 6. Reduce capture work

- **Use DXGI, not GDI.** PowerAim auto-picks DXGI when available; if you're on RDP or an old Windows version, GDI might be in play.
- **Disable ESP overlay drawing methods you don't need.** ESPConfig → Drawing Method. The `DesktopDC` method is heavy and disables Global Active for safety; `WPF Window Canvas` is the cheapest.
- **Make FOV smaller.** Smaller FOV = smaller captured region = less work.

### 7. Disable expensive features temporarily

- **Replay Buffer** — JPEG encoding every frame costs CPU
- **Image-based Anti-Recoil** — phase correlation runs each frame
- **Show Detected Player with `OverlayFormGDI`** — GDI overlay redraw is heavy

Toggle them off one at a time and watch the FPS counter.

### 8. Check thermal throttling

A laptop running at 90+°C will throttle the GPU. PowerAim's loop will jitter rather than run steady. Lower the system power profile, clean the fans, or raise the laptop on a stand.

### 9. Update GPU drivers

DirectML and CUDA both improve with driver updates. If you haven't updated in a year, that's likely worth doing.

### 10. Try a smaller model

Some community models are 3× heavier than the bundled Universal models for marginal quality gains. The Models tab shows file sizes — bigger isn't always better.

## What's a reasonable target?

| Hardware | Reasonable FPS |
|:---------|:---------------|
| Integrated GPU | 30–60 |
| RX 580 / GTX 1060 | 60–100 |
| RTX 2060 / RX 5600 XT | 100–150 |
| RTX 3060+ | 150+ |

Below 60 FPS aim *feels* laggy. Below 30 FPS triggers fire late.

## Related pages

- [Models — Using Models]({{ '/models/using-models#performance-benchmark' | relative_url }})
- [Aim Assist]({{ '/features/aim-assist' | relative_url }})
- [Settings Overview]({{ '/configuration/settings-overview' | relative_url }})
