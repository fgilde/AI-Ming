---
title: Configuration
nav_order: 5
has_children: true
permalink: /configuration/
---

# Configuration

This section is the **reference manual** for everything PowerAim persists to disk. Pair it with the [Features]({{ '/features/' | relative_url }}) section when you want to know *what* a setting does, and come here when you want to know *where* it's stored and how to manipulate it.

## Pages

- **[Settings overview]({{ '/configuration/settings-overview' | relative_url }})** — every option on the Settings page (Input, UI, Capture, Active Processes, Overlays, Stats, HUD OCR, Replay Buffer, AutoPlay Learning)
- **[Per-game profiles]({{ '/configuration/per-game-profiles' | relative_url }})** — `MatchProcess`, auto-switch, auto-pause
- **[Keybinds & hotkeys]({{ '/configuration/keybinds-hotkeys' | relative_url }})** — the global hotkey system
- **[Config file]({{ '/configuration/config-file' | relative_url }})** — JSON structure, location, manual editing

## The config file in 30 seconds

PowerAim stores everything in JSON. The active config file path is at:

```
%AppData%\AI-M\LastConfigPath.cfg
```

…which points to the actual config (default: `<install dir>\bin\configs\Default.cfg`). The file is plain JSON — you can edit it in any text editor while PowerAim is closed.

In-app, the "Configs" toggle on the **Models & Configs** page lets you download community configs (.cfg files) the same way you download models.

## Quick save / load

The cogwheel menu in the title bar offers:

- **Save as Quick Config** — save the current state as the active config
- **Save Config as...** — save with a chosen filename
- **Open Quick Config** — reload the current config
- **Open Other Config** — load a different config file
