---
title: Movement Methods
parent: Features
nav_order: 17
---

# Movement Methods

PowerAim supports **six different output backends** for aim / recoil motion. Pick whichever your game accepts (some anti-cheats reject the simple ones, others reject the fancy ones). Five of them target the system mouse; the sixth drives a virtual gamepad right-stick.

The dropdown is **Settings → Input Settings → Movement Method**. Each entry shows a small device icon (a mouse glyph for mouse backends, a gamepad glyph for the Gamepad entry) so the difference is visible at a glance.

> NOTE: The control was previously labelled "Mouse Movement Method". It was renamed when the Gamepad option was folded in.

## The six methods

### 1. Mouse Event (default)

`mouse_event()` Win32 call. The simplest synthesizer; works everywhere; detectable by some anti-cheats.

### 2. SendInput

`SendInput()` Win32 call. Slightly higher-level than Mouse Event; generally indistinguishable from a real USB mouse to user-mode code.

### 3. LG HUB

Routes input through **Logitech G HUB**'s LUA Lovense scripting API. Requires LG HUB installed and a real Logitech G-series mouse connected. Many games trust LG HUB output because it presents as the real mouse.

### 4. Razer Synapse

Same idea, but through **Razer Synapse**'s SDK. Requires Synapse installed and a real Razer peripheral. The detector accepts every documented Razer Synapse process name (Synapse 3 *and* Synapse 4 — `RazerAppEngine`, `Razer Synapse Service`, etc.).

### 5. ddxoft Virtual Input Driver

A signed kernel virtual mouse driver from ddxoft. PowerAim falls back to MouseEvent if ddxoft can't load (driver not installed, conflict with another tool, etc.).

### 6. Gamepad (Virtual right-stick)

Drives a **ViGEm virtual Xbox right-stick** instead of synthesising mouse motion. The whole aim / recoil / AutoPlay-aim path routes through `InputSender.Move`, so picking Gamepad makes every component send stick deflection. See [Gamepad Aim]({{ '/features/gamepad-aim' | relative_url }}) for the full story.

The entry stays selectable even when ViGEm isn't ready — picking it then pops a `MessageDialog` ("Gamepad not ready") that offers to navigate straight to the Gamepad settings page.

## How to switch

**Settings → Input Settings → Movement Method** dropdown.

The dropdown is at the top of the Input Settings card. Selecting a method that needs an external dependency (LG HUB / Razer / ddxoft / ViGEm for Gamepad) attempts to load it; if loading fails, PowerAim **automatically falls back** to MouseEvent and shows a notice bar (or, for Gamepad, the dialog above) explaining why.

## Driver auto-install

Three of the mouse-movement methods rely on a helper driver/DLL. When you pick one and the dependency is missing, PowerAim offers to fetch or install it rather than just failing:

| Method | What it installs / checks |
|:-------|:--------------------------|
| **Razer Synapse** | If no Synapse process is running, offers to download and launch the Synapse installer (`rzr.to` mirror). It also auto-downloads `rzctl.dll` (from the `MarsQQ/rzctl` GitHub release) the first time, then asks you to re-select the method to load it. Finally it confirms a real Razer peripheral is present (a `Win32_PnPEntity` whose Manufacturer starts with "Razer") before enabling. |
| **LG HUB** | Opens a downloader dialog with mirrors for the Logitech G HUB installer. The downloaded file is verified by **exact size (41,131,424 bytes) and MD5 hash** before it hands off to the OS installer. Loading also requires that **HVCI / Memory Integrity (Core Isolation) is disabled** — if it's on, PowerAim refuses and tells you to turn it off. It additionally checks that the running G HUB build is a 2021-era version. |
| **ddxoft** | Auto-downloads `ddxoft.dll` (from a GitLab mirror) if it's missing. This driver **requires PowerAim to run as administrator** — if you're not elevated, PowerAim shows a warning and the method stays unusable until you relaunch as admin. |

{: .warning }
G HUB's downloader reminds you to disable "Automatic Updates" in G HUB after installing — a newer auto-updated build can break the scripting path PowerAim relies on, and the size/MD5 check is tied to that specific build.

## Which one should I use?

A rough decision tree:

1. **Start with MouseEvent.** Works everywhere, no install needed.
2. If your game ignores PowerAim's input → try **SendInput**.
3. If your game still ignores PowerAim → try **LG HUB** (need a Logitech G mouse) or **Razer Synapse** (need a Razer peripheral).
4. If you want something more universal than option 3 → try **ddxoft**.
5. If your game only accepts gamepad input → pick **Gamepad** (need ViGEm).

## Tips

- **Re-calibrate after switching.** Different methods send slightly different deltas. Re-run the [Calibration Wizard]({{ '/features/calibration-wizard' | relative_url }}) after changing methods.
- **Keep LG HUB / Synapse minimized.** PowerAim talks to them via API — they don't need to be in the foreground.
- **Anti-cheat detection is a moving target.** What works today might not tomorrow. PowerAim is designed for accessibility and training; if your game's anti-cheat blocks PowerAim, that's the anti-cheat doing its job — please respect it.

## Troubleshooting

- **"LG HUB load failed"** — make sure LG HUB is installed (not Logitech Gaming Software) and a Logitech G-series mouse is connected
- **"Razer load failed"** — Razer Synapse must be installed AND a Razer peripheral connected
- **"ddxoft load failed"** — the kernel driver isn't installed or is conflicting with another virtual-input driver
- **"Gamepad not ready" dialog** — ViGEm bus driver missing or no gamepad sender configured. Click through to the Gamepad settings page.
- **Switched method but nothing changed** — PowerAim cached the previous method. Toggle Global Active off and on again
