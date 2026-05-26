---
title: Mouse Input Methods
parent: Features
nav_order: 17
---

# Mouse Input Methods

PowerAim supports **five different mouse-input backends**. Pick whichever your game accepts (some anti-cheats reject the simple ones, others reject the fancy ones).

## The five methods

### 1. Mouse Event (default)

`mouse_event()` Win32 call. The simplest synthesizer; works everywhere; detectable by some anti-cheats.

### 2. SendInput

`SendInput()` Win32 call. Slightly higher-level than Mouse Event; generally indistinguishable from a real USB mouse to user-mode code.

### 3. LG HUB

Routes input through **Logitech G HUB**'s LUA Lovense scripting API. Requires LG HUB installed and a real Logitech G-series mouse connected. Many games trust LG HUB output because it presents as the real mouse.

### 4. Razer Synapse

Same idea, but through **Razer Synapse**'s SDK. Requires Synapse installed and a real Razer peripheral.

### 5. ddxoft Virtual Input Driver

A signed kernel virtual mouse driver from ddxoft. PowerAim falls back to MouseEvent if ddxoft can't load (driver not installed, conflict with another tool, etc.).

## How to switch

**Settings → Input Settings → Mouse Movement Method** dropdown.

The dropdown is at the top of the Input Settings card. Selecting a method that needs an external dependency (LG HUB / Razer / ddxoft) attempts to load it; if loading fails, PowerAim **automatically falls back** to MouseEvent and shows a notice bar explaining why.

## Which one should I use?

A rough decision tree:

1. **Start with MouseEvent.** Works everywhere, no install needed.
2. If your game ignores PowerAim's input → try **SendInput**.
3. If your game still ignores PowerAim → try **LG HUB** (need a Logitech G mouse) or **Razer Synapse** (need a Razer peripheral).
4. If you want something more universal than option 3 → try **ddxoft**.

## Tips

- **Re-calibrate after switching.** Different methods send slightly different deltas. Re-run the [Calibration Wizard]({{ '/features/calibration-wizard' | relative_url }}) after changing methods.
- **Keep LG HUB / Synapse minimized.** PowerAim talks to them via API — they don't need to be in the foreground.
- **Anti-cheat detection is a moving target.** What works today might not tomorrow. PowerAim is designed for accessibility and training; if your game's anti-cheat blocks PowerAim, that's the anti-cheat doing its job — please respect it.

## Troubleshooting

- **"LG HUB load failed"** — make sure LG HUB is installed (not Logitech Gaming Software) and a Logitech G-series mouse is connected
- **"Razer load failed"** — Razer Synapse must be installed AND a Razer peripheral connected
- **"ddxoft load failed"** — the kernel driver isn't installed or is conflicting with another virtual-input driver
- **Switched method but nothing changed** — PowerAim cached the previous method. Toggle Global Active off and on again
