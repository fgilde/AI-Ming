---
title: System Requirements
parent: Getting Started
nav_order: 1
---

# System Requirements

PowerAim runs on Windows 10 and 11 (x64). It is a desktop WPF app built on .NET 10 with native dependencies for screen capture, inference, and virtual input devices.

## Minimum

| Component | Requirement |
|:----------|:------------|
| OS | Windows 10 1903+ or Windows 11 (64-bit) |
| CPU | 4-core x64, 2.5 GHz or faster |
| RAM | 4 GB |
| GPU | DirectX 11 capable, 2 GB VRAM (any DirectML-compatible GPU) |
| Disk | 500 MB for the app + bundled models |
| .NET | .NET Runtime 10 (x64) |
| VC++ Redist | x64 Visual C++ 2015-2022 Redistributable |

## Recommended

| Component | Requirement |
|:----------|:------------|
| CPU | 8-core x64, 3.5 GHz or faster |
| RAM | 16 GB |
| GPU | RTX 2060 / RX 5600 XT or better, 6 GB+ VRAM |
| Display | 144 Hz or higher (DXGI capture matches your refresh rate) |

The bundled **Performance Benchmark** (Models tab → "Run Benchmark") tells you exactly which model resolution your hardware can sustain at 60+ FPS. See [Models — Using models]({{ '/models/using-models' | relative_url }}).

## Optional drivers

PowerAim works without any of these, but some features depend on them.

### ViGEmBus (recommended for any gamepad feature)

Required for PowerAim to **emit** virtual Xbox 360 controller events. Without it:

- The "Use controller for aim" toggle stays disabled
- Controller mapping (KB → Pad direction) cannot synthesize gamepad input
- The trigger system cannot send `GamepadSlider.RightTrigger`-style actions

Install from [vigembusdriver.com](https://vigembusdriver.com) or via PowerAim's **Gamepad settings** page — there's a "Get ViGEmBus driver" button that opens the site for you.

### HidHide (optional)

Cloaks your physical Xbox controller from games when running mapping profiles. Useful when the game would otherwise see *both* your real and virtual controllers at once. PowerAim ships the installer — see [Hidden Controllers]({{ '/features/hidden-controllers' | relative_url }}).

### Tesseract data (`eng.traineddata`)

Only needed for the [OCR HUD reader]({{ '/features/ocr' | relative_url }}). PowerAim downloads it on first use into `%LocalAppData%/PowerAim/tessdata`.

### Ollama

Only needed for [AutoPlay]({{ '/features/autoplay' | relative_url }}). PowerAim talks to an Ollama instance at `http://localhost:11434` (configurable). Install from [ollama.com](https://ollama.com) and pull a vision model like `moondream`, `llava:7b`, or `qwen2.5-vl:7b`.

### vJoy (optional alternative to ViGEm)

The Gamepad settings page also offers vJoy as a virtual gamepad backend. ViGEm is the default and recommended choice — vJoy is kept as a fallback for older systems.

## DirectML vs. CUDA build

PowerAim ships **two builds**:

| Build | When to use |
|:------|:------------|
| **DirectML** (default) | Works on every DX12 GPU — NVIDIA, AMD, Intel Arc, integrated. Smaller download. |
| **CUDA** (`_cuda` variant) | NVIDIA cards only, but typically 10–25% faster inference for the same model size. Requires CUDA 12.x runtime on the system. |

The Settings page has a one-click **"Switch to DirectML / CUDA"** button if you want to swap builds without uninstalling.

{: .note }
If you're not sure which build you have, look at the title bar — CUDA builds show "(CUDA)" after the version number on the About page.
