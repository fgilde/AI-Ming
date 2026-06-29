---
title: Dynamic Tools
parent: Features
nav_order: 17
---

# Dynamic Tools

The **Tools** page is a single list of expandable cards. Two tools are always there — the
**Magnifier** and the **HWID Spoofer** — and below them you can build **your own tools**: small,
named automations made of a sequence of actions, each startable by a hotkey or a button.

<!-- SCREENSHOT NEEDED (../images/tools-page.png): the Tools page showing the Magnifier and HWID
     cards plus one custom tool, one card expanded to reveal its option inputs. -->

## The tool list

Every tool — built-in or custom — is one card with the same row:

- a **start button (▶)** that runs it once,
- a **start keybind** you can record (works globally, like every other PowerAim hotkey, including
  [combo keys](../configuration/keybinds-hotkeys)),
- an **enable toggle**,
- and an **expander** that opens the tool's panel.

Custom tools also get **Edit** and **Delete**. The built-ins can't be edited or removed; their panel
keeps their own controls (the Magnifier's zoom keys and size sliders, the spoofer's note).

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

> **Send keys — execution mode.** *Simultaneous* sends every key at once (each still honouring its own
> recorded delay); *Sequential* sends them one after another. This mirrors the trigger's send behaviour.
> Use **record-sequence** to capture a real key sequence with its timing.

### Options (variables)

Options are **variables** you define on the tool. Each has a **name**, a **type**
(String / Number / Bool / Path / Enum), and a default value:

- Reference an option inside any action's text field as `{name}` — a program's path or arguments, the
  mouse **X/Y**, or a **delay** in milliseconds.
- The option's current value is substituted when the tool runs.
- Option values are editable **right on the tool's card** (in the list) before you start it, so one
  tool can be reused with different inputs without re-editing it.

> **Example.** Define an option `target` of type Number with value `100`, then set a *Move mouse*
> action's **X** to `{target}`. Running the tool moves to X = 100; change `target` on the card to 250
> and it moves to 250.

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
  zoom-in / zoom-out keys and the magnification / window-size sliders.
- **HWID Spoofer** — opens the bundled spoofer tool. (Existing Magnifier hotkeys carry over: your old
  Magnifier toggle key is migrated onto the Magnifier card's start key on first launch.)

## Tips

- Keep frequently-used macros as separate tools and bind each to its own key — combos like
  `Ctrl+Shift+1` work as start keys.
- Put a small **Delay** between a *down* and the matching *up* (or between a click and a key) when a
  game needs time to register each input.
- For a "run my external script" tool, a single **Run program** action with a `{path}` and `{args}`
  option is often all you need.

## Troubleshooting

- **A tool does nothing** — check it's **enabled**, and that the start key isn't shared with another
  binding. Run it once with the ▶ button to rule out the keybind.
- **`{name}` shows up literally** — the option name and the token must match exactly (it's
  case-insensitive but must otherwise be identical); make sure the option exists on that tool.
- **"Run program" is blocked** — launching as administrator triggers a UAC prompt; if you cancel it,
  the action fails silently and the rest of the sequence continues.
- **A run got stuck once** — re-press the start key; that cancels the run and releases any held input.
