---
title: Models
nav_order: 4
has_children: true
permalink: /models/
---

# Models

PowerAim runs **YOLOv8 ONNX models** for object detection. Models are interchangeable: download a different one and PowerAim picks up the new input size and class list automatically.

This section covers:

- **[Using Models]({{ '/models/using-models' | relative_url }})** — switching models, the in-app store, the benchmark
- **[Training Your Own]({{ '/models/training-your-own' | relative_url }})** — labelling data and training a fresh model from scratch
- **[Contributing Models]({{ '/models/contributing' | relative_url }})** — submitting a model to the community store

## How models work in PowerAim

- Models are `.onnx` files in `bin\models\`
- Input shape is read from the ONNX metadata — both **fixed** (e.g. 640×640) and **dynamic** (any multiple of 32) are supported
- Multi-class models are handled — the class names list comes from the model's `names` custom metadata field (Ultralytics convention `{0: "Enemy", 1: "Teammate"}`)
- Inference runs via **ONNX Runtime** with two execution providers:
  - **DirectML** (default build) — works on every DX12 GPU
  - **CUDA** (`_cuda` build) — NVIDIA only, ~10–25% faster
- Both providers fall back to CPU if the GPU path fails

## Where models come from

- **Bundled** — `bin\models\` ships several Universal models out of the box
- **In-app Store** — Models tab → Downloadable Models. Merges PowerAim's `models/` folder with a configured upstream community model repo
- **Trained yourself** — see [Training Your Own]({{ '/models/training-your-own' | relative_url }})
- **Manually placed** — drop any `.onnx` into `bin\models\` and click refresh

{: .note }
The merge rule for the in-app store: filename match between repos → newer commit wins; tie → fork wins. Users see one combined list.
