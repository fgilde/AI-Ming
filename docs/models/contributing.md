---
title: Contributing Models
parent: Models
nav_order: 3
---

# Contributing Models

PowerAim's in-app **Downloadable Models** tab pulls from `fgilde/AI-Ming` plus a configured upstream community model repo and merges them transparently. If the same filename exists in both, the **newer commit wins**; on a tie, PowerAim's repo wins. Users see one combined list.

## Naming convention

If you'd like to be credited, name your model:

```
[Game Name] by [Your Handle].onnx
```

If you'd rather stay anonymous:

```
[Game Name].onnx
```

## How to upload

Submit to PowerAim's repo:

1. Fork `https://github.com/fgilde/AI-Ming`
2. Drop your `.onnx` into the `models/` folder of your fork
3. Open a Pull Request against `main`
4. Once merged, your model appears in everyone's in-app **Downloadable Models** tab on next refresh

## Quality bar

We don't reject models — anyone can submit. But for your model to be **useful**:

- Train on varied data (different maps, weapons, lighting, view angles)
- Export with dynamic input axis (`dynamic=True`)
- Include a `names` metadata field for multi-class models
- Test in-game first — open PowerAim, enable **Show Detected Player**, and verify confidences are reasonable

See [Training Your Own]({{ '/models/training-your-own' | relative_url }}) for the full pipeline.

## Removing a model

If you want a model removed, open another PR removing the file, or open an Issue against the repo where it lives.

## Tips for high-quality models

- **YOLOv8 architecture exported to ONNX with NHWC → NCHW input order**
- **Input image size from 192 to 1280** (multiple of 32) — PowerAim picks this up automatically from the ONNX metadata
- **Multi-class supported** — `names` custom metadata field with Ultralytics convention (e.g. `{0: "Enemy", 1: "Teammate"}`)
- **Thousands of varied images** — PowerAim's detection quality is bottlenecked by training data, not by inference
- **Test in-game with `Show Detected Player` enabled** — verify class confidences are reasonable

---

Thank you for contributing! PowerAim and the wider community benefit massively from shared models.
