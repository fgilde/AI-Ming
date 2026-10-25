---
title: Quick Config
parent: Configuration
nav_order: 6
---

# Quick Config

Quick Config is a tiny always-on-screen **config tab** plus a pop-up **switcher**, so you can see and
swap your whole setup without opening the main window — handy mid-game.

<!-- SCREENSHOT NEEDED (../images/quick-config.png): the floating config tab showing the active
     config label, with the Quick Config switcher popped out below it listing a few configs, the
     active one highlighted, each with a keybind chip, and a "Save current as…" row. -->

## The floating config tab

A small floating tab shows the **current config's label** (`AppConfig.EffectiveConfigLabel`). It's a
separate top-most window, excluded from screen capture like the rest of PowerAim's UI.

- **Click the label** to rename the active config inline (Enter to commit, Esc to cancel) — this sets
  `ConfigLabel` on the config.
- **Click the tab** to open the Quick Config switcher just beneath it.

## The switcher

The switcher lists every config in `bin\configs`:

- The **active config is highlighted** (accent dot + bold).
- **Click another** to switch to it — the (heavy, UI-rebuilding) load is deferred until the pop-up
  closes, so the switch is smooth.
- Each row has a **per-config keybind** chip. It reuses the **same binding** as the Models & Configs
  page (stored under the `CONFIG` prefix + the file name), so setting it in either place updates the
  one shared hotkey. Pressing the key loads that config — even while Global Active is off (the config
  hotkeys deliberately ignore the Global-Active gate). Combos work here too — see
  [Keybinds & Hotkeys]({{ '/configuration/keybinds-hotkeys' | relative_url }}).
- A **"Save current as…"** row opens the normal save dialog.

The pop-up closes when it loses focus — except while you're **mid-recording a keybind** (capturing a
mouse-button binding moves focus off the window, which would otherwise dismiss it before the bind lands).

## Tips

- **Bind a key per config** to flip between, say, a *practice* and a *match* setup instantly, without
  alt-tabbing — the same binds you may already have set on the Models & Configs page show up here.
- **Label your configs** from the tab so the floating label is meaningful at a glance (e.g. "CS2 –
  rifle", "Apex – controller").
- The config hotkeys ignore the Global-Active gate on purpose, so you can switch configs even when
  everything else is paused.

## See also

- [Config File]({{ '/configuration/config-file' | relative_url }}) — what a config stores and where.
- [Per-Game Profiles]({{ '/configuration/per-game-profiles' | relative_url }}) — auto-switch by process.
- [Keybinds & Hotkeys]({{ '/configuration/keybinds-hotkeys' | relative_url }}) — combos and the binding system.
