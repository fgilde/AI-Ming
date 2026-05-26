---
title: Per-Game Profiles
parent: Configuration
nav_order: 2
---

# Per-Game Profiles

PowerAim can **auto-pause** when you alt-tab to a non-game and **auto-switch** profiles when you alt-tab between games. Both behaviors are driven by foreground-process matching.

## What it does

PowerAim continuously watches which window is in the foreground. When you switch:

- The AI loop pauses if the new foreground is a recognised non-game (or no whitelist match)
- Each Trigger, AutoPlay profile, and Controller Mapping profile decides whether to activate based on its `MatchProcess` pattern

The result: one PowerAim install, multiple games, zero per-game manual switching.

## How to enable

Both toggles are on the **Settings → Active Processes** card.

| Setting | What it does | Default |
|:--------|:-------------|:--------|
| **Auto Pause on Focus Loss** | Pause AI loop when foreground is a non-game | On |
| **Auto Switch Profile** | Honor each entry's `MatchProcess` pattern | On |
| **Game Process Patterns** | Whitelist of process names that count as "games" | empty |

If both toggles are off, every trigger / mapping / autoplay profile fires whenever its other conditions are satisfied — no process gating.

## Match patterns

`MatchProcess` is a string pattern matched against the foreground process name (without `.exe`).

Supported syntax (via `ProcessMatcher`):

| Pattern | Matches |
|:--------|:--------|
| `cs2` | Exactly `cs2.exe` (case-insensitive) |
| `cs2|valorant` | Either `cs2.exe` or `valorant.exe` |
| `*game*` | Any process name containing "game" |
| `cs?` | Three-letter process starting with `cs` |
| (empty) | Always matches |

Patterns are case-insensitive.

## Setting per-entry patterns

Every Trigger, AutoPlay Profile, and Controller Mapping Profile has an optional **Match Process** field in its editor.

### Triggers

**Aim Tools → AutoTrigger → Edit a trigger → Match Process**

Example: a "Headshot trigger" with `MatchProcess = cs2|valorant` only fires in CS2 and Valorant.

### AutoPlay Profiles

**AutoPlay → Edit a profile → Match Process**

Example: an "FPS AutoPlay" profile with `MatchProcess = csgo|cs2` only activates in CS games. Switching to Battlefield would automatically swap to a different AutoPlay profile.

### Controller Mapping Profiles

**Mapping → Edit a profile → Match Process**

Example: a "Driving mapping" profile with `MatchProcess = forza*|nfs*` activates whenever any Forza or NFS executable is in the foreground.

## Game Process Patterns whitelist

If your game isn't being detected as a game (Auto-Pause keeps pausing you), add it to the **Game Process Patterns** comma-separated list on the Active Processes card.

If the whitelist is empty, PowerAim uses a built-in fallback list of common non-games (Chrome, Firefox, Code, terminals, PowerAim itself, etc.). Anything not on the non-game list counts as a game.

## Tips

- **Use specific names.** `valorant.exe` is more reliable than `val*` (which would also match `validate.exe`).
- **Multiple games on one trigger:** use the pipe (`cs2|valorant|csgo`).
- **Profile per game.** Maintain one Trigger / Mapping / AutoPlay profile per supported game. Enable them all; the process filter decides which one is active.
- **Combine with directions.** A mapping profile with `MatchProcess = forza_horizon5` + direction `KB → Pad` activates only for Forza Horizon 5 sessions.

## Troubleshooting

- **Trigger doesn't fire even though I'm in the game** — verify the process name. Open Task Manager and check the exact executable name; remove any `.exe` for the pattern.
- **Multiple profiles fire at once** — that's by design. Per-feature, only one is active *for that feature*. So one Trigger + one Mapping + one AutoPlay all active at once is normal.
- **Auto-Pause never engages** — check Game Process Patterns. If your game is in the list, PowerAim treats it as a game (never pauses). Either remove it or rely on the foreground change to non-game windows.
- **PowerAim itself causes Auto-Pause** — PowerAim's process is on the built-in non-game list. When PowerAim is the foreground window, the AI loop pauses; that's intentional.
