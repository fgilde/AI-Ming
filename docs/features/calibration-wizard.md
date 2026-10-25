---
title: Calibration Wizard
parent: Features
nav_order: 10
---

# Calibration Wizard
{: .no_toc }

{: .warning }
**This feature has been removed.** The sensitivity-calibration wizard is no longer part of
PowerAim. It existed to measure your in-game sensitivity and write a matching
`MouseSensitivity` value — but the aim pipeline is now a **closed-loop controller**: it observes
where the target actually is each frame and corrects toward it, so a one-off sensitivity
measurement is no longer needed.

## What replaced it

The current aim pipeline (default) tracks each target across frames and drives the mouse with a
frame-rate-independent damped controller. Instead of calibrating a fixed multiplier, you set how
*snappy* the correction is:

- **Mouse Sensitivity** — now the per-frame approach fraction. Higher = snappier, lower = smoother.
  Start around `0.25` and adjust to taste.
- **Aim presets** — pick a starting feel (smooth tracking, snappy/flick, precise, humanized) on the
  aim-profile editor instead of measuring a number.

See **[Aim Assist]({{ '/features/aim-assist' | relative_url }})** for the full, current aim
configuration.
