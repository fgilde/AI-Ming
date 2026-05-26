---
title: Gamepad Not Detected
parent: Troubleshooting
nav_order: 1
---

# Gamepad Not Detected

Symptoms:

- "Use Controller for Aim" toggle is greyed out
- Controller mapping is on but the game ignores PowerAim's gamepad output
- Gamepad settings page says "PowerAim cannot send Gamepad signals"
- Status text on the page is red

## Checklist

### 1. Confirm Send Mode

**Gamepad** sidebar → **Send Command Mode** dropdown.

- **ViGEm** (default) — needs ViGEmBus driver
- **vJoy** — needs vJoy driver
- **Internal** — pure C# fallback, lower compatibility
- **XInput Hook** — process-specific hook (advanced)
- **None** — disabled

Start with **ViGEm**. The other modes are mostly for special cases.

### 2. Install / repair ViGEmBus

On the Gamepad page, if ViGEm isn't detected you'll see a **"Get ViGEmBus driver"** button. Click it; it opens [vigembusdriver.com](https://vigembusdriver.com) in your browser.

1. Download the latest `ViGEmBus_Setup_x.x.xx_x64.exe`
2. Run as administrator
3. Reboot if prompted

Verify in PowerAim:

1. Click any other sidebar item
2. Click **Gamepad** again
3. The status should now say *"Great, PowerAim is ready to send Gamepad signals"*

### 3. Check the live diagnostics panel

The Gamepad settings page includes an embedded **Gamepad Diagnostics Panel**. It shows:

- Each physical controller slot (0–3) with its status
- The virtual controller's status
- Buttons to open the Gamepad Tester / Hidden Controllers / `joy.cpl`

If the virtual controller row is missing, ViGEm didn't load. If a physical controller row is missing, Windows isn't seeing your real controller.

### 4. Open the Gamepad Tester

**Gamepad page → Open Gamepad Tester** (also accessible from the Mapping page).

The tester shows the physical and virtual controller side by side. Press a button on your physical controller — both panels should light up. Press a mapped key on your keyboard — only the virtual panel should light up.

If the virtual panel never reacts:

- Mapping engine isn't running. Verify **Mapping Active** is on (sidebar → Mapping → Active toggle)
- The active profile has zero KB→Pad mappings. Check the profile editor.

### 5. Game sees my controller but inputs are wrong

The game is probably reading **both** your real and virtual controller. Two fixes:

- **HidHide** — cloak the real controller. See [Hidden Controllers]({{ '/features/hidden-controllers' | relative_url }}).
- **Device disable** — disable the real device temporarily. Also in Hidden Controllers.

### 6. ViGEmBus install succeeded but PowerAim still says "not ready"

Possible causes:

- **Wrong ViGEmBus version.** PowerAim uses Nefarius.ViGEm.Client; very old ViGEmBus versions are incompatible. Reinstall the latest.
- **Windows blocked the kernel driver.** Check Windows Settings → Update & Security → Recovery → Advanced startup → Restart → Troubleshoot → Advanced → Startup Settings → Disable driver signature enforcement. Reinstall ViGEmBus.
- **PowerAim isn't elevated and a previous run left the bus in a bad state.** Restart PowerAim as admin (title bar button).

### 7. "Restart as Admin" button doesn't appear

That means PowerAim is already elevated. No action needed.

If you want PowerAim to launch elevated automatically:

- Right-click `PowerAim.exe` → Properties → Compatibility → Run this program as administrator → OK

### 8. vJoy alternative

If ViGEm absolutely refuses to install:

1. **Gamepad page → Send Mode → vJoy**
2. Click **Install vJoy** — PowerAim ships the installer
3. Reboot

vJoy is less compatible than ViGEm (some games don't recognize it as a controller) but works as a fallback.

## Related pages

- [Controller Mapping]({{ '/features/controller-mapping' | relative_url }}) — the mapping engine itself
- [Hidden Controllers]({{ '/features/hidden-controllers' | relative_url }}) — HidHide + device disable
- [Gamepad Aim]({{ '/features/gamepad-aim' | relative_url }}) — "Use Controller for Aim" toggle
- [Controller Aim Has No Effect]({{ '/troubleshooting/controller-aim-no-effect' | relative_url }}) — if gamepad is detected but aim doesn't move the camera
