---
title: Gamepad Aim
parent: Features
nav_order: 15
---

# Gamepad Aim ("Use Controller for Aim")

A toggle that makes the entire aim pipeline drive the **virtual right stick** instead of synthesizing mouse motion. Use it for games that accept only gamepad input.

## What it does

When `UseControllerForAim` is on, PowerAim's aim/move loop:

1. Computes the per-tick mouse-delta exactly as it would for SendInput
2. Translates that delta into a right-stick deflection
3. Writes the deflection to the virtual ViGEm gamepad

Triggers can also be routed to gamepad buttons (`GamepadButton.A`, `GamepadSlider.RightTrigger`, etc.) via the trigger editor.

The result: the game sees gamepad input, never knows there's a keyboard hooked up.

## Prerequisites

- **ViGEmBus driver installed.** Without it, the toggle stays disabled. See [Installation]({{ '/getting-started/installation#5-optional-install-vigembus' | relative_url }}).
- **A working gamepad sender.** On the Gamepad settings page, the **Send Mode** must be ViGEm (default) or one of the other working backends.

## How to enable

1. **Aim Tools → AimConfig → Use Controller for Aim → toggle on**
2. PowerAim's aim loop now writes to the virtual stick instead of the mouse

The toggle is greyed out until ViGEm is set up. Its tooltip explains why if it's disabled.

## Configuration

The aim sensitivity sliders still apply, but their values now map to **stick deflection** rather than **pixel movement**:

| Setting | What changes |
|:--------|:-------------|
| **Mouse Sensitivity** | Acts as a stick-deflection scale. Tune by feel. |
| **EMA Smoothening** | Same as before — smooths the target before deflection is computed. |
| **Movement Path** | Same as before. |
| **GamepadMinimumLT / RT** | (Settings page) thresholds below which trigger pulls are ignored. |

## Tips

- **Calibrate again after enabling.** The Calibration Wizard works with whatever input method is active — re-run it for gamepad-aim feel.
- **Combine with [Controller Mapping]({{ '/features/controller-mapping' | relative_url }}).** Use Controller Mapping to wire your WASD keys to the virtual left stick; PowerAim drives the right stick. Result: full virtual gamepad input from your KB+M.
- **Hide your physical controller while doing this.** Otherwise some games sum inputs from both pads. See [Hidden Controllers]({{ '/features/hidden-controllers' | relative_url }}).

## Troubleshooting

- **Toggle is greyed out** — ViGEm isn't ready. Open the [Gamepad Not Detected]({{ '/troubleshooting/gamepad-not-detected' | relative_url }}) guide.
- **Game doesn't react to aim** — see [Controller Aim Has No Effect]({{ '/troubleshooting/controller-aim-no-effect' | relative_url }}).
- **Aim drifts even with mouse still** — the AI loop is sending residual deltas. Toggle off and back on; if it persists, the deadzone (`StickDeadzone` on the mapping profile) might be too low.
