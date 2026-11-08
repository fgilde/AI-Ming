---
title: PowerAim Models
parent: Models
nav_order: 4
---

# Models trained for PowerAim

These models are in the in-app store (Models tab, Downloadable Models) under the name "… by PowerAim". Each
one detects a single class, `enemy`, and was trained as YOLO11s at 640 px on public datasets from
[Roboflow Universe](https://universe.roboflow.com/). Heads and teammates in those datasets were left out of
the labels; images that showed marked teammates were left out entirely.

The minimum confidence column is where the model was most accurate on its validation images. Set **AI Minimum
Confidence** in Settings to about that value when you use the model.

| Model | Min. confidence | mAP50 | Trained on |
|:------|:---------------:|:-----:|:-----------|
| Modern Warfare 4 (10K) | 40 % | 0.91 | [mw4](https://universe.roboflow.com/bo7v1/mw4-is3t6) by bo7v1, [acuity-mw4-take-2](https://universe.roboflow.com/ai-model-yhlik/acuity-mw4-take-2) by ai-model-yhlik |
| Battlefield 6 (6.3K) | 40 % | 0.84 | [Battlefield 6](https://universe.roboflow.com/bf6-pszfx/battlefield-6-5aepi-avm2i) by bf6, [Battlefield 6 players](https://universe.roboflow.com/enes-wwfxp/battlefield-6-players-zvu5a-zzet8) by enes, [battlefield](https://universe.roboflow.com/madara-zumvi/battlefield-ryrmn) by madara |
| Warzone (6.5K) | 50 % | 0.87 | [warzone](https://universe.roboflow.com/radek-k-3yxsc/warzone-jcmmi) by radek k (public domain), [cod-mw-warzone](https://universe.roboflow.com/kolly-ku5ew/cod-mw-warzone) by kolly |
| Black Ops 6 & 7 (1.2K) | 35 % | 0.61 | [Black Ops 6 SheeksterZens](https://universe.roboflow.com/spectre-divide/black-ops-6-sheeksterzens) by Spectre Divide, [Black Ops 6](https://universe.roboflow.com/project-locker-s2uxl/black-ops-6-9w79t) by Project Locker, [black-ops-enemy-tracking](https://universe.roboflow.com/cagganite-outlook-com/black-ops-enemy-tracking) by cagganite |
| Delta Force (4.4K) | 40 % | 0.62 | [Delta Force](https://universe.roboflow.com/yolov11-hs6o5/delta-force-wtowy) by yolov11, [Delta Force](https://universe.roboflow.com/asd-uk3k2/delta-force-a6yrz) by asd, [Delta Force Hawk Ops](https://universe.roboflow.com/rust-ai-cheat/delta-force-hawk-ops-8pmic) by rust-ai-cheat |
| Splitgate 2 (19.6K) | 45 % | 0.97 | [giga_data_splitgate](https://universe.roboflow.com/splitgate2dataset/giga_data_splitgate) by splitgate2dataset (public domain) |

The number in brackets is the number of images the model learned from. Black Ops and Delta Force had the
smallest or least consistent datasets and miss more enemies than the others. The Splitgate score is probably
higher than what you will see in the game, because its validation images come from the same recordings as the
training images.

## Licence

The datasets are licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) unless marked public
domain above. The credits above and in each model file (ONNX metadata `sources`) are the attribution the
licence asks for. The models were trained with [Ultralytics](https://github.com/ultralytics/ultralytics).
