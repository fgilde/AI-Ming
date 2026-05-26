---
title: FOV Overlay
parent: Features
nav_order: 6
---

# FOV Overlay

The FOV (Field-of-View) overlay draws a circle on your screen showing the region PowerAim will scan for targets. It's both a configuration tool and a visual reference during play.

![FOV ring drawn over a game](../images/fov-ring.png)
<!-- SCREENSHOT NEEDED: Game running with the PowerAim FOV ring drawn centered on the screen, showing the radius and a faint accent-color tint. -->

## What it does

The FOV circle defines the screen region used for inference: anything inside the ring is candidates for detection; anything outside is ignored. Smaller FOV = faster inference, fewer false positives, and tighter aim — but also fewer chances to acquire a far-away target.

PowerAim supports a **dynamic FOV**: while a hotkey is held, the FOV temporarily shrinks to a smaller size. Use this for precision aiming.

## How to enable

**Aim Tools → FOVConfig → FOV (toggle on)**

## Configuration options

All settings live on the **FOVConfig** card in **Aim Tools**.

| Setting | What it does | Default |
|:--------|:-------------|:--------|
| **FOV** | Master toggle for showing the ring | Off |
| **Dynamic FOV** | Enable hotkey-shrinking FOV | Off |
| **Dynamic FOV Keybind** | Key that shrinks the FOV while held | Left Mouse Button |
| **FOV Color** | Ring color (and a faint fill) | Accent |
| **FOV Size** | Diameter in pixels (10–640) | 640 |
| **Dynamic FOV Size** | Diameter while the keybind is held | 200 |
| **FOV Opacity** | Ring + fill alpha (0–1) | 1.0 |

## Tips

- **Match FOV Size to your reaction time.** If you're slow at flicks, a smaller FOV reduces noise; if you're fast, a larger FOV gives more options.
- **Dynamic FOV pairs naturally with ADS.** Bind Dynamic FOV Keybind to your right-mouse-button (aim-down-sights) and shrink to 200 px while scoped — you get tight precision aim only when zoomed.
- **Keep FOV Size ≤ Image Size.** PowerAim's inference image is `ImageSize × ImageSize` (default 640). If FOV is larger, parts of the ring fall outside the inference region. Either match them or use Image Size = 640 as a safe ceiling.

## Troubleshooting

- **Ring isn't centered** — same as the [crosshair issue]({{ '/features/crosshair-overlay#troubleshooting' | relative_url }}). The ring is drawn at the display center, not the game center.
- **Dynamic FOV doesn't engage** — verify the Dynamic FOV toggle is on AND the keybind is bound to a key you're actually pressing.
- **Ring visible to my recording software** — disable **Hide UI from Capture** on the Settings page if you want OBS to see it.
