---
title: Dynamic Tools
parent: Features
nav_order: 17
---

# Dynamic Tools

The **Tools** page is a single list of expandable cards. A few **built-in tools** are always there —
the **Magnifier**, the **Crosshair**, **Anti-AFK** and the **HWID Spoofer** — and below them you can
build **your own tools**: small, named automations made of a sequence of actions, each startable by a
hotkey or a button.

<!-- SCREENSHOT NEEDED (../images/tools-page.png): the Tools page showing the built-in cards plus one
     custom tool, one card expanded to reveal its option inputs. -->

## The tool list

Every tool — built-in or custom — is one card with the same row:

- a **start button (▶)** that runs it once (for toggle tools like the Magnifier, Crosshair and
  Anti-AFK it toggles them on/off),
- a **start keybind** you can record (works globally, like every other PowerAim hotkey, including
  [combo keys](../configuration/keybinds-hotkeys)),
- and an **expander** that opens the tool's panel.

Custom tools also get **Edit** and **Delete**. The built-ins can't be edited or removed; their panel
keeps their own controls (the Magnifier's zoom keys and size sliders, the Crosshair's appearance, the
Anti-AFK interval, the spoofer's note).

> There's no separate "active" switch — a tool is simply run by its **▶** button or its keybind. If you
> don't want a tool active, just don't give it a keybind.

The **+** button at the bottom creates a new custom tool and opens the editor.

## Building a custom tool

A custom tool is an **ordered sequence of actions** that runs **once** each time you start it. Open the
editor (the **+** button, or **Edit** on a custom tool) — it opens as a full page with three parts:
the **name**, the **options**, and the **action sequence**.

### Action types

Add actions with the **+** in the Actions section; reorder them with the up/down arrows. Each action
has a type, picked from a dropdown at the top of its editor:

| Action | What it does |
|:-------|:-------------|
| **Move mouse** | Move the cursor. Choose **relative** (a delta sent through the active mouse backend) or **absolute** (screen pixels). |
| **Click** | Press a mouse button — left / right / middle — as **down**, **up**, or **down-and-up**. |
| **Send key(s)** | Send one or more keys. Uses the same multi-key control as triggers, with **record-sequence** and a **Sequential / Simultaneous** execution choice. |
| **Run program** | Launch an executable with arguments, optionally **as administrator** and optionally **waiting for it to exit**. |
| **Delay** | Wait a number of milliseconds before the next action. |
| **Set variable** | Assign a variable at runtime — `name = value` (the value can itself contain `{tokens}`). Later actions read it back as `{name}`. |
| **HTTP request** | Send a **GET / POST / PUT / DELETE** request, wait for the response, and store its body in a variable — plus `{name}.status` (HTTP code) and `{name}.ok` (true/false). URL, body and headers all accept `{tokens}`. |

> **Run only if … (per-action guard).** Every action has a guard at the bottom of its editor: pick an
> operator (`=`, `≠`, `>`, `<`, `contains`, `is true`, `is false`) and two values, or leave it on
> `always` to run unconditionally. The step runs only when the comparison holds. Both values accept
> `{tokens}`, so this is how you branch — see [Conditions and branching](#conditions-and-branching).

> **Send keys — execution mode.** *Simultaneous* sends every key at once (each still honouring its own
> recorded delay); *Sequential* sends them one after another. This mirrors the trigger's send behaviour.
> Use **record-sequence** to capture a real key sequence with its timing.

### Variables

Anywhere an action has a text field — a path, the mouse **X/Y**, a delay, a URL, a value to compare —
you can drop a `{token}`, and it's replaced with that variable's value when the tool runs. There are
three sources of variables.

**1. Options** are the variables you define on the tool. Each has a **name**, a **type**
(String / Number / Bool / Path / Enum), and a default value. Option values are editable **right on the
tool's card** (in the list) before you start it, so one tool can be reused with different inputs
without re-editing it.

**2. Runtime variables** are written by actions while the tool runs:

- **Set variable** assigns `name = value`.
- **HTTP request** stores its response body in the variable you name, plus `{name}.status` and
  `{name}.ok`.

Later actions read them back as `{name}` — so step 3 can act on what step 2 fetched.

**3. The live target** — the best current detection (highest confidence) — is available in every
action, refreshed each step:

| Variable | Value | Best used for |
|:---------|:------|:--------------|
| `{target.found}` | `true` / `false` | a guard — does step run only when something is detected? |
| `{target.confidence}` | `0..1` | a guard — e.g. only act when `> 0.8` |
| `{target.class}` | class name (e.g. `Enemy`) | a guard — act on a specific class |
| `{target.screenX}`, `{target.screenY}` | **absolute screen pixels** of the target centre | **Move mouse (absolute)** |
| `{target.dx}`, `{target.dy}` | offset from the **current cursor** to the target | **Move mouse (relative)** |
| `{target.x}`, `{target.y}` | normalized `0..1` inside the capture box | conditions (is the target left/right of centre?) |
| `{targets.count}` | number of detections this frame | a guard — e.g. `> 0` |

`screenX/Y` and `dx/dy` are computed exactly the way the aim engine maps a detection to the screen,
and rounded to whole pixels — so they drop straight into a **Move mouse** action.

> **Example — option as a variable.** Define an option `target` of type Number with value `100`, then
> set a *Move mouse* action's **X** to `{target}`. Running the tool moves to X = 100; change `target`
> on the card to 250 and it moves to 250.
>
> **Example — snap onto the best target.** A single *Move mouse* action, **relative**, with
> **X = `{target.dx}`** and **Y = `{target.dy}`**, guarded by `{target.found}` `is true`. Bind the tool
> to a key and it nudges the cursor onto whatever the model currently sees.

### Conditions and branching

Every action carries a **Run only if …** guard (operator + two values). Leave it on `always` to run
every time; otherwise the step runs only when the comparison holds. Because both sides accept
`{tokens}`, the guard is how you branch:

- **Gate a step:** give a *Click* action the guard `{target.confidence}` `>` `0.8` — it fires only on a
  confident detection.
- **if / else:** put two actions back to back — give the first the guard `{var1}` `is true` and the
  second `{var1}` `is false`. Exactly one runs.
- **React to a request:** after an *HTTP request* that stores into `resp`, guard the next step with
  `{resp.ok}` `is true` (or branch on `{resp.status}` `=` `200`).

### Saving

The editor edits a working copy — **Save** commits it, **Cancel / Back** discards every change
(including option and action edits). New tools only appear in the list after you save.

## Running a tool

Press the tool's start key or click ▶. The sequence runs once on a background thread:

- A **re-press while it's still running cancels the current run and starts a fresh one** (fire-once,
  restartable).
- If a run is cancelled mid-way, anything it was holding (a mouse button or key left in the *down*
  state) is **released automatically**, so a cancelled sequence can't leave an input stuck.

## The built-in tools

- **Magnifier** — a hotkey-toggled zoom window. Its card's start key toggles it; the panel keeps the
  zoom-in / zoom-out keys, the magnification / window-size sliders and a **scaling** mode
  (None / Smooth HQ / Enhanced).
- **Crosshair** — a hotkey-toggled crosshair overlay. The start key toggles it; the panel holds its
  appearance (shape, colour, size). (The crosshair settings moved here out of the overlay settings.)
- **Anti-AFK** — while toggled on, nudges the mouse a pixel and back on an interval so you aren't
  flagged idle (net cursor movement is zero). The panel sets the nudge interval.
- **HWID Spoofer** — opens the bundled spoofer tool.

> Existing Magnifier and Crosshair hotkeys carry over: your old toggle keys are migrated onto the
> matching tool card's start key on first launch.

## Tips

- Keep frequently-used macros as separate tools and bind each to its own key — combos like
  `Ctrl+Shift+1` work as start keys.
- Put a small **Delay** between a *down* and the matching *up* (or between a click and a key) when a
  game needs time to register each input.
- For a "run my external script" tool, a single **Run program** action with a `{path}` and `{args}`
  option is often all you need.

## Troubleshooting

- **A tool does nothing** — make sure the start key isn't shared with another binding. Run it once
  with the ▶ button to rule out the keybind.
- **A target variable is empty / `{target.found}` is false** — nothing is being detected right now
  (the model must be loaded and actively detecting). `screenX/Y` and `dx/dy` only have a value while a
  target is present.
- **`{name}` shows up literally** — the option name and the token must match exactly (it's
  case-insensitive but must otherwise be identical); make sure the option exists on that tool.
- **"Run program" is blocked** — launching as administrator triggers a UAC prompt; if you cancel it,
  the action fails silently and the rest of the sequence continues.
- **A run got stuck once** — re-press the start key; that cancels the run and releases any held input.
