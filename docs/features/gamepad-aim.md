---
title: Gamepad Aim
parent: Features
nav_order: 15
---

# Gamepad Aim

Drive the **virtual right stick** instead of synthesising mouse motion, so the game sees gamepad input. Useful for titles that accept only gamepad input or that explicitly reject keyboard hooks.

## How to enable

Gamepad aim is no longer a separate toggle — it's one of the entries in the unified [Movement Method]({{ '/features/mouse-input-methods' | relative_url }}) dropdown:

**Settings → Input Settings → Movement Method → Gamepad**

When picked, every component that previously sent mouse deltas (aim, anti-recoil, AutoPlay aim hints) routes through `InputSender.Move`, which writes to the ViGEm right-stick instead of the system mouse. Triggers can still be routed to gamepad buttons (`GamepadButton.A`, `GamepadSlider.RightTrigger`, etc.) via the trigger editor.

> NOTE: Older docs referenced a separate "Use Controller for Aim" toggle in AimConfig. That toggle is gone — its setting is migrated into the Movement Method dropdown on first config load.

## Prerequisites

- **ViGEmBus driver installed.** Without it, picking **Gamepad** in the dropdown pops a `MessageDialog` ("Gamepad not ready") offering to navigate to the Gamepad settings page. See [Installation]({{ '/getting-started/installation#5-optional-install-vigembus' | relative_url }}).
- **A working gamepad sender.** On the Gamepad settings page, the **Send Mode** must be ViGEm (default) or one of the other working backends.

The **Gamepad** option stays *selectable* even when no virtual controller is configured, so you can preselect it (the warning fires, then you fix the underlying issue and try again).

## Configuration

The aim sensitivity sliders still apply, but their values now map to **stick deflection** rather than **pixel movement**:

| Setting | What changes |
|:--------|:-------------|
| **Mouse Sensitivity** | Acts as a stick-deflection scale. Tune by feel. |
| **EMA Smoothening** | Same as before — smooths the target before deflection is computed. |
| **Movement Path** | Same as before. |
| **GamepadMinimumLT / RT** | (Settings page) thresholds below which trigger pulls are ignored. |

## Tips

- **Calibrate again after switching to Gamepad.** The Calibration Wizard works with whatever movement method is active — re-run it for the gamepad-aim feel.
- **Combine with [Controller Mapping]({{ '/features/controller-mapping' | relative_url }}).** Use Controller Mapping to wire your WASD keys to the virtual left stick; PowerAim drives the right stick. Result: full virtual gamepad input from your KB+M.
- **Hide your physical controller while doing this.** Otherwise some games sum inputs from both pads. See [Hidden Controllers]({{ '/features/hidden-controllers' | relative_url }}).
- **Anti-recoil follows the same path.** Any active anti-recoil profile rides the virtual right stick automatically — no separate switch.

## Troubleshooting

- **"Gamepad not ready" dialog when I pick Gamepad** — ViGEm isn't set up. Open the [Gamepad Not Detected]({{ '/troubleshooting/gamepad-not-detected' | relative_url }}) guide; the dialog has a shortcut to the Gamepad settings page.
- **Game doesn't react to aim** — see [Controller Aim Has No Effect]({{ '/troubleshooting/controller-aim-no-effect' | relative_url }}).
- **Aim drifts even with mouse still** — the AI loop is sending residual deltas. Switch Movement Method off Gamepad and back on; if it persists, the deadzone (`StickDeadzone` on the mapping profile) might be too low.
- **Selected Gamepad but anti-recoil still nudges the mouse** — only the action's *destination* changed; the source still has to be PowerAim. If the mouse cursor itself moves, something outside PowerAim is the source.
