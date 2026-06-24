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

It started as a fork of [Babyhamsta/Aimmy](https://github.com/Babyhamsta/Aimmy) and has been
heavily reworked: a decoupled service architecture, persistent multi-target tracking, a complete
trigger-system overhaul, a Fluent-styled UI, gamepad / mapping / AutoPlay support, localization in
9 languages, dynamic model sizes, and a much faster capture & inference pipeline.

{: .important }
PowerAim is **source-available** under the **PolyForm Noncommercial** license. Commercial use,
including commercial forks, is prohibited. See [Source-Available]({{ '/advanced/source-available/' | relative_url }}).

## Find your way around

- **[Getting started]({{ '/getting-started/' | relative_url }})** — install, first aim, system requirements and best practices.
- **[Features]({{ '/features/' | relative_url }})** — aim assist, triggers, anti-recoil, OCR, gamepad, AutoPlay, overlays and more.
- **[Configuration]({{ '/configuration/' | relative_url }})** — settings, the config file, keybinds and per-game profiles.
- **[Advanced]({{ '/advanced/' | relative_url }})** — architecture, localization and the source-available license.

Looking for downloads and the changelog? Head to the **[main site]({{ '/' | relative_url }})**.
