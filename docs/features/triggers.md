---
title: Triggers
parent: Features
nav_order: 2
---

# Triggers (AutoTrigger)

PowerAim's trigger system is a complete rewrite of the original Aimmy autotrigger. Each profile holds an **arbitrary number of independent triggers** — each with its own keys, actions, intersection checks, and timing.

![Aim Tools page showing the Auto Trigger card](../images/aim-tools-page.png)

## What it does

While **AutoTrigger** is on, each defined trigger watches its keys + intersection rules and fires its configured actions when both are satisfied.

Use cases:

- Auto-fire when the crosshair sits inside the head area (`HeadIntersectingCenter`)
- Auto-throw a grenade after holding G for half a second (Delay + Action)
- Pre-aim with charge mode: hold the trigger, release when the head enters the kill zone
- Fire only while `LMB AND Shift` are both held, but never while `R` or `Tab` are held (anti-trigger)

## How to enable

1. **Aim Tools → AutoTrigger → toggle on**
2. The trigger list shows your current triggers. Each row has:
   - Name + active state
   - Quick description (keys + actions + intersection)
   - Toggle, Edit, Delete buttons
3. Click **+ New Trigger** at the bottom to create one, or **Edit** to open the full editor.

## The trigger editor

The trigger editor is the heart of the system. Open it via **Edit** on any trigger row.

![Trigger editor](../images/trigger-editor.png)

### Identification

- **Name** — free text, shown in the trigger list and in the active-trigger status line
- **Enabled** — master switch for this trigger
- **Match Process** — optional process-name pattern. When set, this trigger only activates while the matching process is in the foreground. Supports wildcards (`*`, `?`) and pipes (`cs2|valorant`). See [Per-game profiles]({{ '/configuration/per-game-profiles' | relative_url }}).

### Trigger keys

- **Trigger Keys** — keys/buttons that must be held for the trigger to fire. Can be any mix of keyboard keys, mouse buttons, gamepad buttons, and gamepad triggers (LT / RT).
- **Trigger Keys Operator** — `AND` (all keys held) or `OR` (any key held).
- **Anti-Trigger Keys** — keys that **block** firing while held (e.g. `R` so you don't fire mid-reload).
- **Anti-Trigger Keys Operator** — `AND` / `OR` for the anti-keys, same semantics.

### Actions

The list of inputs the trigger sends when it fires. Mix any of:

- Keyboard keys (`Keys.Space`, etc.)
- Mouse buttons (`MouseButtons.Left`)
- Gamepad buttons (`GamepadButton.A`)
- Gamepad sliders (`GamepadSlider.RightTrigger`)

**Execution Mode:**
- **Sequential** — fire the actions one after another, awaiting each before starting the next. If an action has a per-action `MinTime`, that wait is applied before it.
- **Simultaneous** — fire all actions at once (default)

Either way, each individual press is held down for a randomized [Fire Max Delay](#fire-max-delay-randomized-click-timing) before release.

### Intersection checks

Triggers can require the detected target to **intersect** the screen center in a specific way. Two checks are evaluated:

| Check | Meaning |
|:------|:--------|
| `None` | No spatial requirement — fires purely on the key check |
| `IntersectingCenter` | The full detection box must include the screen center |
| `HeadIntersectingCenter` | The head sub-region of the detection box must include the screen center |

For `HeadIntersectingCenter`, you also pick a sub-rectangle of the bounding box (the "head area"). The editor has a live preview showing where that sub-rectangle is relative to the detected player.

There are in fact **two** independent intersection checks, each with its **own** head-area rectangle:

| Check | Property | Head area |
|:------|:---------|:----------|
| Begin | `BeginIntersectionCheck` | `BeginIntersectionArea` |
| Execution | `ExecutionIntersectionCheck` | `ExecutionIntersectionArea` |

Without Charge Mode, only the **Execution** check (and its area) matters — it gates the fire. With Charge Mode on, the Begin check comes into play too (see below). Because each check owns its rectangle, you can require a loose line-up to *begin* and a precise head intersection to actually *fire*.

### Charge mode

**Charge Mode** is for charged shots — bows, railguns, anything you press-and-hold until the target lines up, then release. When it is on, the action is **pressed down** as soon as the **Begin** check is satisfied and **released** when the **Execution** check is satisfied:

- **Begin Intersection Check** — when satisfied, the trigger **starts holding** the action (e.g. starts holding LMB to pre-aim / charge).
- **Execution Intersection Check** — when satisfied, the trigger **completes** (releases the action, loosing the shot).

Use case: hold LMB while the enemy is in your wider FOV (`BeginIntersectionCheck = IntersectingCenter`), then release the click exactly when their head crosses the center (`ExecutionIntersectionCheck = HeadIntersectingCenter`).

### Timing

- **Delay** — wait this long after the keys/intersection are satisfied before firing (seconds).
- **Break Time** — minimum wait between consecutive fires (seconds). Used to throttle rapid-fire actions.

#### Fire Max Delay (randomized click timing)

The global **Fire Max Delay** slider (Settings page, `SliderSettings.FirePressDelay`, in seconds, default `0.1`) humanizes auto-fire. Whenever an action (or an auto-fire click) is pressed and released, the **press-to-release hold** is a *random* value between `0` and this maximum, so consecutive presses don't share an identical, machine-perfect duration. Set it lower for snappier fire, higher for more human-looking variance. It is global, shared by every trigger — not per-trigger.

### Needs detection

If **Needs Detection** is off, the trigger fires purely on the key state — useful for binding macros without a target requirement (e.g. "Press G + delay 500 ms + press LMB" = grenade-cook macro).

### OCR conditions

A trigger can be gated on live [OCR]({{ '/features/ocr' | relative_url }}) region values, so it only fires while the HUD shows a particular state — for example, only fire while ammo is above 5, or only while health is at least 50.

Conditions are held in an AND/OR-able **condition tree** (`OcrConditionTree`). Each leaf picks a defined OCR region, a comparison operator, and a target value; leaves are grouped under AND/OR nodes so you can express compound rules. The trigger only fires while the tree evaluates to `true`; an empty tree imposes no constraint.

| Operator | Fires when the region… |
|:---------|:-----------------------|
| **Greater than** | parses as a number greater than the value |
| **Greater or equal** | number ≥ the value |
| **Less than** | number < the value |
| **Less or equal** | number ≤ the value |
| **Equals** | matches the value (numeric, or trimmed case-insensitive text) |
| **Not equal** | does not match the value |
| **Contains** | text contains the value (case-insensitive) |
| **Not contains** | text does not contain the value |

The numeric comparisons require the region to read as a number; the text comparisons work on the raw recognized text.

{: .note }
Configs that predate the tree (a flat `OcrConditions` list) are migrated into the tree automatically on first load — your old conditions become AND-ed leaves.

#### Anti-OCR conditions (fire-except)

Alongside the positive tree there is a second, inverted tree (`AntiOcrConditionTree`). It is the OCR mirror of the [anti-trigger keys](#trigger-keys): when the anti-tree evaluates to `true`, the trigger is **blocked**. This lets you say "fire whenever ammo > 5 **except** when the equipped weapon is the knife" by putting the weapon check in the anti-tree instead of negating every positive condition. An empty anti-tree never blocks.

{: .important }
Both OCR trees are **only enforced while the OCR engine is on** (Settings → HUD OCR → Enable HUD OCR). If OCR is off, the conditions are ignored and the trigger behaves as if they weren't set. You must [define the OCR regions]({{ '/features/ocr' | relative_url }}) first — the names you give regions are what you select here.

## Default profile

PowerAim ships a default `Primary Fire` trigger:

```
Name: Primary Fire
Trigger Keys: LeftTrigger OR RightMouseButton
Action: LeftMouseButton
Execution: HeadIntersectingCenter
Charge Mode: off
```

## Tips

- **Use Anti-Trigger Keys liberally.** Adding `R, Tab, ScrollLock` to every fire-trigger's anti-keys saves you from accidental fires during reload / scoreboard / map view.
- **Charge Mode shines for sniper rifles.** Set `BeginIntersectionCheck = IntersectingCenter`, `ExecutionIntersectionCheck = HeadIntersectingCenter`. You pre-aim while the body enters the FOV, fire when the head crosses center.
- **Match Process lets you reuse one PowerAim install across games.** Create one trigger per game with `MatchProcess = cs2.exe`, `valorant.exe`, etc., and PowerAim only arms the trigger that matches the focused game.
- **Sequential execution + small Delay = scripted combo.** Useful for grenade pulls or quick-switch macros.

## Troubleshooting

- **Trigger doesn't fire at all** — check the master AutoTrigger toggle, the Global Active master toggle, and the trigger's own enabled state.
- **Trigger fires too often** — raise the Break Time slider.
- **Trigger fires too late** — lower the Delay slider, or lower `FirePressDelay` on the Settings page.
- **Charge Mode releases too early** — increase the size of the Execution Intersection Area.
- **Trigger ignores my anti-key** — verify the anti-key is actually a held key (not a one-shot press). The anti-key has to be `IsHolding` true at the moment of fire.
