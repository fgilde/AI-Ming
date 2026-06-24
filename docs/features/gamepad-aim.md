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

## Gamepad backends (Send Mode)

The virtual controller can be driven through several backends, picked on the Gamepad settings page. The active one is the `GamepadSendMode` value:

| Send Mode | What it does | Use it for |
|:----------|:-------------|:-----------|
| **ViGEm** *(default)* | Creates a virtual **Xbox 360** controller via the ViGEmBus driver. | **Real games — this is the one to use.** |
| **vJoy** | Drives a vJoy virtual device (generic DirectInput joystick). | Games / tools that read vJoy specifically. |
| **XInputHook** | Spawns an external `XInputEmu.exe` (from `Resources/XInputEmu/`) targeting a chosen game process and sends inputs to it over **UDP** (127.0.0.1:13000). Requires the **Gamepad process** (target window) to be set. | Niche XInput-injection scenarios. |
| **Internal** | Builds the controller state purely **in memory** — no virtual device is registered, so real games cannot see it. | Testing the pipeline / mapping logic only. |
| **None** | No sender is created. | Disable gamepad output. |

{: .warning }
**XInputHook is experimental and x86-only.** The bundled `XInputHook.dll` cannot inject into 64-bit games, so it fails for most modern titles. **Internal is for testing only** — because nothing is registered with the OS, real games receive no input. For actually playing a game, use **ViGEm**.

When you select vJoy, ViGEm, or Internal, the Gamepad settings page also exposes the [Hidden Controllers]({{ '/features/hidden-controllers/' | relative_url }}) options (auto-hide toggle + HidHide path) so the game sees only the virtual pad.

## Configuration

The aim sensitivity sliders still apply, but their values now map to **stick deflection** rather than **pixel movement**:

| Setting | What changes |
|:--------|:-------------|
| **Mouse Sensitivity** | Acts as a stick-deflection scale. Tune by feel. |
| **EMA Smoothening** | Same as before — smooths the target before deflection is computed. |
| **Movement Path** | Same as before. |
| **GamepadMinimumLT / RT** | (Settings page) how far the analog triggers must travel before they count as pressed — see below. |

### Gamepad trigger thresholds

When a **physical** controller trigger feeds a binding (a trigger source, gun-switch, etc.), PowerAim only treats it as "pressed" once the analog pull crosses a threshold. Two sliders on **Settings → Input Settings** set this:

| Setting | Field | Default | Meaning |
|:--------|:------|:--------|:--------|
| **GamepadMinimumLT** | `SliderSettings.GamepadMinimumLT` | `0.7` | LT counts as pressed at ≥ 70% travel |
| **GamepadMinimumRT** | `SliderSettings.GamepadMinimumRT` | `0.7` | RT counts as pressed at ≥ 70% travel |

The reader normalises the raw trigger value to `0.0–1.0` and fires the press when `value >= threshold`. Sliders range `0.1–1.0`. Lower the value for a hair-trigger; raise it to avoid accidental light pulls.

## Diagnostics & tester

Two tools help confirm the gamepad path actually reaches your game.

**Gamepad diagnostics** — a live panel on the Gamepad settings page (also available as a dialog) shows:

- The **XInput slot map** (slots 0–3): which are connected and each one's live RT/LT/button readout, so you can spot whether your real pad is sitting in slot 0 (the slot most games read) and crowding out the virtual pad.
- **Sender status**: active send mode, the concrete sender type, and `CanWork`.
- A context-aware **suggestion** (e.g. "ViGEm bus missing", "switch to XInputHook", or "enable Hide physical controller").
- A **Fire test RT pulse** button that briefly pushes RT to full so you can watch which slot lights up.

**Gamepad tester** — a pop-out window (`GamepadTesterWindow`, opened from **Gamepad settings → Open Gamepad Tester**) that visualises the controller live and floats above other windows, so you can keep it open while editing. See [Controller Mapping]({{ '/features/controller-mapping/' | relative_url }}) for the mapping-side view of the same tester.

**Driver install buttons** — the Gamepad settings page also has in-app installers: **Install vJoy** (runs the bundled `vJoySetup.exe`, shown when vJoy mode isn't working yet) and **Install HidHide** (runs the bundled HidHide installer, shown when HidHide isn't found). For ViGEm, a button links to `vigembusdriver.com` when the bus driver is missing.

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
