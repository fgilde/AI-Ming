---
title: OCR Not Working
parent: Troubleshooting
nav_order: 4
---

# OCR Not Working

Symptoms:

- OCR is enabled but the dialog shows empty results
- OCR returns garbage characters
- "Tessdata not found" error
- OCR makes PowerAim sluggish

## Checklist

### 1. Tessdata installed?

PowerAim needs `eng.traineddata` to OCR English text. Default location:

```
%LocalAppData%\PowerAim\tessdata\eng.traineddata
```

The OCR Regions dialog has a **"Download Tessdata"** button that grabs it for you. If the download fails:

1. Download `eng.traineddata` manually from the [Tesseract releases](https://github.com/tesseract-ocr/tessdata)
2. Drop it into `%LocalAppData%\PowerAim\tessdata\`
3. Restart OCR (toggle off / on)

You can also point the **Tessdata Path** field (Settings → HUD OCR card) at a custom folder.

### 2. Engine actually enabled?

Two switches must both be on:

- **Settings → HUD OCR → Enable HUD OCR**
- The dialog's **Engine Enabled** checkbox (mirrors the same setting)

### 3. Region defined and enabled?

Open the OCR Regions dialog. Confirm:

- At least one region exists in the list
- The region's **Enabled** checkbox is ticked
- The region's rectangle covers the text you want to read

Use the **Test** button to OCR the region once without waiting for the polling tick.

### 4. Region rectangle is correct?

OCR regions are in **screen-pixel coordinates** (not normalized). If you change:

- **Resolution** (monitor change, fullscreen vs. windowed)
- **DPI scaling**
- **In-game HUD scale**

…the region won't align anymore. Re-draw it.

### 5. Results are garbage

Try in this order:

1. **Increase the region size.** Tesseract works best on text that's ~30+ pixels tall. Cropped tightly is worse than slightly oversized.
2. **Try the Invert toggle.** Tesseract trained on black-on-white expects dark text. White-on-dark HUDs need Invert.
3. **Tune the Threshold.** Default 140. Lower for darker backgrounds, higher for lighter.
4. **Use the right Kind.** Number = digits only; Health = number + slash; Text = free-form. Wrong Kind = wrong post-processing.

### 6. OCR is slow

Tesseract is CPU-bound. Costs:

- ~5 ms per small region (e.g. an ammo counter)
- ~50 ms for a large region with lots of text

If you have 5 regions × 50 ms each, that's 250 ms per tick — close to your interval. Lower the count or shrink the regions.

**OCR Interval** (Settings → HUD OCR) controls polling frequency. 500 ms is the default; 1000 ms is fine for health/ammo; below 200 ms you'll feel the CPU load.

### 7. OCR works in the dialog but not at runtime

The dialog uses a working copy. Make sure you clicked **Save** to commit changes. Cancelling the dialog discards them.

### 8. Region jitters by a pixel each frame

That's the binarization threshold being right at the edge. Adjust Threshold up or down by ~20 to give a clearer cutoff.

## Related pages

- [OCR Feature]({{ '/features/ocr' | relative_url }})
- [Settings Overview]({{ '/configuration/settings-overview#hud-ocr' | relative_url }})
