---
title: OCR (HUD Reader)
parent: Features
nav_order: 9
---

# OCR HUD Reader

A periodic Tesseract-based OCR engine that reads named rectangular regions of your screen and exposes the recognized text. Use it to track health, ammo, score, or any other on-screen number — and feed the values into triggers or AutoPlay.

![OCR Regions dialog](../images/ocr-regions-dialog.png)
<!-- SCREENSHOT NEEDED: OCR Regions dialog open, master/detail layout — list of regions on the left, live preview + sliders on the right. -->

## What it does

On each tick (default every 500 ms), the OCR engine:

1. Captures each enabled region as a small Bitmap
2. Optionally binarizes it (threshold + invert)
3. Runs Tesseract 5.2 with English-language data
4. Post-processes per region kind (Number = digits only, Health = number + slash, Text = free-form)
5. Stores the result in `OcrService.Latest`

Other PowerAim subsystems (AutoPlay, triggers) can read those values; you can also see them live in the dialog.

## How to enable

1. **Settings → HUD OCR → Enable HUD OCR**
2. Click **Configure OCR Regions**
3. In the dialog, click **+ Add Region**
4. Draw the region directly on the captured frame (click-and-drag)
5. Pick the **Kind**: Text / Number / Health
6. Optionally enable **Invert** and adjust **Threshold** for clearer OCR
7. Hit **Test** to OCR the region once and see the result

## Tessdata download

Tesseract needs an `eng.traineddata` language model. On first launch the dialog shows a **Download Tessdata** button that grabs it into `%LocalAppData%\PowerAim\tessdata\`. If you'd rather supply your own, point the **Tessdata Path** field on the Settings card to your folder.

## Configuration options

### Settings card

| Setting | What it does | Default |
|:--------|:-------------|:--------|
| **Enable HUD OCR** | Master toggle | Off |
| **OCR Interval** | Polling interval in ms (100–5000) | 500 |
| **Tessdata Path** | Override the data folder | empty (= default) |

### Per-region

| Setting | What it does |
|:--------|:-------------|
| **Name** | Free text — used by consumers to look up the value |
| **X / Y / Width / Height** | Pixel rectangle on the captured frame |
| **Enabled** | Per-region toggle (lets you keep regions defined but inactive) |
| **Kind** | Text / Number / Health — drives post-processing |
| **Invert** | Invert the binarized image (for white-on-dark HUDs) |
| **Threshold** | Binarization cutoff (0–255). 140 default. |

## Tips

- **Keep regions tight.** 100×40 around the ammo number is way faster than scanning a 400×400 corner.
- **Binarize when the font is anti-aliased.** Tesseract works best on clean black-on-white. Try the Invert toggle if the font is light on dark.
- **500 ms is fine for health.** Don't drop the interval below 200 ms unless you really need it — OCR is CPU-bound and adds latency.
- **Health = "75/100".** The Health kind strips spaces but preserves the slash so consumers can split it.

## Use cases

- **Auto-reload when ammo is low.** A trigger reads OCR ammo, fires `R` when ammo < 5.
- **AutoPlay aggression.** AutoPlay knows when health is low and biases toward retreat actions.
- **Stream overlay.** Pipe OCR values out to your stream without OBS plugins.

## Troubleshooting

- **OCR returns garbage** — sharper image needed: tighten the region, bump threshold, try Invert.
- **No `tessdata` found** — verify the Tessdata Path on the Settings card points at a folder containing `eng.traineddata`.
- **OCR seems off** — confirm the region is correct on the live preview in the dialog. The capture source must include your HUD.
- **High CPU** — raise OCR Interval or disable the more expensive regions.
- **Tessdata download failed** — drop `eng.traineddata` manually into `%LocalAppData%\PowerAim\tessdata\` from the [official Tesseract releases](https://github.com/tesseract-ocr/tessdata).
