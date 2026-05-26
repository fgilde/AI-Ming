---
title: Hidden Controllers
parent: Features
nav_order: 16
---

# Hidden Controllers (HidHide + Device Disable)

When you're using PowerAim's virtual ViGEm controller, many games see **both** your physical controller and the virtual one — and sum their inputs, causing drift, dead inputs, or "controller disconnected" messages.

PowerAim ships two solutions:

1. **HidHide** (recommended) — cloaks specific HID devices from every app *except* a configured whitelist
2. **Device disable** (fallback) — disables the device in Device Manager

## When you need this

- You have a real Xbox / PlayStation / DualSense controller plugged in *and* you're using PowerAim's virtual ViGEm gamepad
- The game starts ignoring your aim input, or shows two controllers in its menu
- Stick deadzone behavior feels wrong (both pads contribute to the resting position)

## Solution 1 — HidHide

HidHide is a third-party kernel driver (open-source on GitHub) that cloaks devices on a per-app basis: the game doesn't see them, but PowerAim still does.

### Install

1. **Gamepad settings → Install HidHide** (PowerAim ships the installer)
2. Follow the prompts — a reboot is required
3. Back in PowerAim, the Auto Hide Controller toggle becomes available

### Auto-hide

Toggle **Auto Hide Controller** on the Gamepad settings page. PowerAim will:

1. Add itself to HidHide's app whitelist
2. Enable the master cloak
3. Hide your physical Xbox controller's HID interface from non-whitelisted processes

Toggling it off restores visibility.

### Manual control

The Gamepad settings page also surfaces a **HidHide Path** file locator if PowerAim can't auto-detect the install. The dialog shows the resolved path or a red error.

For more fine-grained control, launch HidHide's own UI (Start menu → HidHide Configuration Client) — PowerAim's integration uses the CLI, so the GUI's settings are compatible.

## Solution 2 — Device disable

Some setups can't run HidHide (locked-down enterprise machines, missing kernel-driver permissions). PowerAim ships a fallback: **disable the device in Device Manager** while PowerAim is running, and re-enable on exit.

### Use it

1. **Gamepad settings → Hidden Controllers** subpage (button on the page)
2. The page lists every HID gamepad device with a row per device
3. Click **Disable** to disable, **Enable** to re-enable
4. PowerAim remembers your choice and restores devices on app exit

This requires PowerAim to run as **administrator** — Device Manager API needs elevated rights. If not elevated, you'll see a "Restart as admin" button in the title bar (also clickable from this subpage).

<!-- SCREENSHOT NEEDED (../images/hidden-controllers-page.png): Hidden Controllers subpage listing 2-3 HID devices, each with a name, a status indicator, and Disable/Enable buttons. -->

### Restart as admin

The title bar has a hidden **Restart as admin** button that only appears when PowerAim is **not** elevated. Click it to relaunch with admin privileges — necessary for device disable and some HidHide operations.

## How to choose

| Situation | Recommended |
|:----------|:------------|
| Standard Windows install | **HidHide** |
| Locked-down work laptop | **Device disable** |
| You want zero kernel drivers | **Device disable** |
| You frequently swap between using PowerAim and not | **HidHide** (toggle the cloak, no reboot) |

## Reset HidHide

If you suspect a past PowerAim run left a device cloaked you can't un-cloak, the **HidHide Reset** option clears everything: cloak off, all devices unhidden, app whitelist cleared. The next time you toggle Auto Hide Controller back on, the configuration rebuilds from scratch.

## Troubleshooting

- **HidHide install fails** — usually a missing Windows Update or test-signing requirement. Make sure Windows is current.
- **Game still sees my physical controller after enabling HidHide** — verify Auto Hide Controller is on AND PowerAim was added to the whitelist (Gamepad settings shows status). Sometimes a game restart is needed.
- **Device Manager disable doesn't survive reboot** — Windows re-enables devices on reboot by default. PowerAim re-disables them on launch if you've toggled the row before.
- **"Restart as admin" button is missing** — that means PowerAim is already elevated. No action needed.
