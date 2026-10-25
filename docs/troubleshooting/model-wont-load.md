---
title: Model Won't Load
parent: Troubleshooting
nav_order: 2
---

# Model Won't Load

Symptoms:

- Clicking a model in the Models tab does nothing
- Notice bar says "Failed to load model: ..."
- Aim Tools page stays in the "no model loaded" state

## Checklist

### 1. Is the file actually an ONNX file?

PowerAim accepts only `.onnx`. Verify:

- File extension is `.onnx`
- File size is reasonable (small models 5–20 MB, large 50–250 MB)
- File isn't 0 bytes (a failed download)

If you downloaded the file from a non-store source, re-download — corrupted ONNX files refuse to load.

### 2. Is the file in the right folder?

PowerAim looks in `<install dir>\bin\models\`. If the file is anywhere else, it won't appear in the local list.

### 3. Is the architecture supported?

PowerAim is built around **YOLOv8** ONNX (anchor-free, NCHW input, 1×3×H×W). Other architectures may fail to load. Specifically:

- YOLOv5 ONNX usually works (PowerAim handles both anchor-based and anchor-free output)
- YOLOv7 *should* work but isn't officially tested
- Non-YOLO ONNX (custom architectures) — likely fails

### 4. CUDA build trying to load on a non-NVIDIA system?

The CUDA build expects:

- An NVIDIA GPU
- The CUDA 12.x runtime installed on the system

If you have an AMD or Intel GPU and the CUDA build, PowerAim falls back to DirectML — but if CUDA itself crashes on load, the fallback never gets a chance. Switch to the DirectML build.

### 5. Test with a known-good model

Click any of the bundled `Universal*.onnx` files. They're known to work. If even bundled models fail, the issue is environmental (.NET, VC++, ONNX Runtime) rather than the model.

### 6. Read the in-app log

**Logs** sidebar item. ONNX Runtime errors are written here verbatim — they usually say exactly what's wrong:

- `Unsupported operator: ...` → model uses a custom op PowerAim doesn't support
- `Invalid input shape: ...` → input shape isn't what PowerAim expects
- `DXGI device removed` → GPU driver crashed; reboot

### 7. Re-install prerequisites

If models fail with cryptic native errors (`access violation`, `DllNotFound`), reinstall:

- .NET Runtime 10 (x64)
- Visual C++ 2015-2022 Redistributable (x64)

Both must be x64. PowerAim does **not** work with x86 variants.

### 8. Switch DirectML / CUDA build

**Settings → UI Settings → Switch to DirectML / CUDA**. If one build refuses to load a model, the other might handle it. CUDA in particular is more permissive about non-standard ops.

## Multi-class model class list is empty

If the model loads but the Target Classes dialog shows `Class 0`, `Class 1`, etc. instead of names — the model is missing the `names` custom metadata field. Either:

- Re-export from Ultralytics: `model.export(format="onnx")` automatically writes `names`
- Or live with the numeric IDs — they still work for filtering

## "Invalid input shape" specifically

PowerAim expects `[N, 3, H, W]` (NCHW) where N=1 and 3 is RGB channels. If your model is NHWC, re-export with NCHW order:

```python
model.export(format="onnx", dynamic=True, simplify=True)
```

## Related pages

- [Using Models]({{ '/models/using-models' | relative_url }})
- [Training Your Own]({{ '/models/training-your-own' | relative_url }})
