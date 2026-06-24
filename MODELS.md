# Contributing Models to PowerAim

## Naming convention

If you'd like to be credited, name your model:

```
[Game Name / Model Name] by [Your Handle]
```

If you'd rather stay anonymous, just use `[Game Name / Model Name]`.

## How to upload a model

You can submit a model to either repository — both feed the same downloader.

### Option A — upload to PowerAim's fork

1. Fork `https://github.com/fgilde/AI-Ming`
2. Drop your `.onnx` file into the `models/` directory of your fork
3. Open a Pull Request against `main`
4. Once merged, your model appears in everyone's in-app **Downloadable Models** tab on next refresh

## Visual walkthrough

The screenshots below are taken from the upstream Aimmy guide — the GitHub workflow is identical for either repo.

<img src="readme_assets/DT1.png" />
<img src="readme_assets/DT2.png" />

Go to your fork's `models` folder:
<img src="readme_assets/DT3.png" />

Press **Add File → Upload files**:
<img src="readme_assets/DT4.png" />

Drag your model onto the upload area:
<img src="readme_assets/DT5.png" />

Commit:
<img src="readme_assets/DT6.png" />

Switch to the **Pull requests** tab:
<img src="readme_assets/DT7.png" />

Create a new pull request:
<img src="readme_assets/DT8.png" />

<img src="readme_assets/DT9.png" />

Submit:
<img src="readme_assets/DT10.png" />

## Tips for high-quality models

- Use a YOLOv8 architecture exported to ONNX with NHWC → NCHW input order
- Input image size can be anything from 192 to 1280 (multiple of 32); PowerAim picks this up automatically from the ONNX metadata
- Multi-class models are supported — list class names in the `names` custom metadata field (Ultralytics convention `{0: "Enemy", 1: "Teammate"}` etc.)
- Include thousands of varied images (different maps, weapons, lighting, view angles) — PowerAim's detection quality is bottlenecked by training data, not by inference
- Test in-game with **Show Detected Player** enabled to verify class confidences are reasonable

## Removing a model

If you want a model removed, open another PR removing the file, or open an Issue against the repo where it lives.

---

Thank you for contributing! PowerAim and the wider community benefit massively from shared models.
