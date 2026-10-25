---
title: Documentation
layout: default
nav_order: 1
permalink: /documentation/
description: PowerAim documentation — installation, features, configuration and internals.
---

# PowerAim documentation
{: .fs-9 }

Everything about setting up, configuring and understanding PowerAim — an AI-powered aim
alignment tool for Windows, built on .NET 10 and WPF.
{: .fs-6 .fw-300 }

[Get started]({{ '/getting-started/' | relative_url }}){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 }
[Back to the main site]({{ '/' | relative_url }}){: .btn .fs-5 .mb-4 .mb-md-0 .mr-2 }
[View on GitHub](https://github.com/fgilde/AI-Ming){: .btn .fs-5 .mb-4 .mb-md-0 }

![PowerAim main window]({{ '/images/main.png' | relative_url }})

---

## What is PowerAim?

PowerAim captures the screen, runs a YOLOv8 ONNX model on the frame, and nudges the mouse (or a
virtual controller) toward the detected target. It works entirely from the outside — it never
reads or writes game memory and injects no code. Everything is **fully configurable**, **100%
local**, and **100% free** — no ads, no key system, no paywalled features.

It's built around a decoupled service architecture, persistent multi-target tracking, a complete
trigger system, a Fluent-styled UI, gamepad / mapping / AutoPlay support, localization in
9 languages, dynamic model sizes, and a fast capture & inference pipeline.

{: .important }
PowerAim is free to use. For the license, terms and credits, see
[LicenseInfo](https://github.com/fgilde/AI-Ming/blob/main/LicenseInfo.md).

{: .note }
**Start here — keybinds are the most powerful part of PowerAim.** Any binding can be a chord across
keyboard, mouse and gamepad at once (even `Ctrl + Q + Gamepad LT`), and you can bind toggles, profiles,
custom tools and whole configs — combine them to build complete, game-specific workflows from a single
key. See **[Keybinds & Hotkeys]({{ '/configuration/keybinds-hotkeys' | relative_url }})**.

## Find your way around

- **[Getting started]({{ '/getting-started/' | relative_url }})** — install, first aim, system requirements and best practices.
- **[Features]({{ '/features/' | relative_url }})** — aim assist, triggers, anti-recoil, OCR, gamepad, AutoPlay, overlays and more.
- **[Configuration]({{ '/configuration/' | relative_url }})** — settings, the config file, keybinds and per-game profiles.
- **[Advanced]({{ '/advanced/' | relative_url }})** — architecture and localization.

Looking for downloads and the changelog? Head to the **[main site]({{ '/' | relative_url }})**.
