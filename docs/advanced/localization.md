---
title: Localization
parent: Advanced
nav_order: 2
---

# Localization

PowerAim ships with 9 UI languages, generated at compile time by the **kli.Localize** source generator.

## Supported languages

| Code | Language |
|:-----|:---------|
| `en-US` | English (United States) |
| `de-DE` | German (Germany) |
| `es-ES` | Spanish (Spain) |
| `fr-FR` | French (France) |
| `it-IT` | Italian (Italy) |
| `ru-RU` | Russian (Russia) |
| `tr-TR` | Turkish (Turkey) |
| `uk-UA` | Ukrainian (Ukraine) |
| `zh-CN` | Chinese (Simplified) |

The language defaults to the system locale; users can override it via **Settings → UI Settings → Language**. Changing the language reloads every visible string immediately — no restart needed.

## How it works

Translation strings live in JSON files under `PowerAim/Localizations/`:

```
Localizations/
├── Locale.json            # the source of truth (English keys + default values)
├── Locale_de-DE.json      # German overrides
├── Locale_en-US.json      # English overrides
├── Locale_es-ES.json
├── Locale_fr-FR.json
├── Locale_it-IT.json
├── Locale_ru-RU.json
├── Locale_tr-TR.json
├── Locale_uk-UA.json
└── Locale_zh-CN.json
```

At build time, **kli.Localize** parses these files and generates a static class `Locale` with one property per key. Strings are accessed in code as `Locale.AimAssist`, `Locale.AntiRecoil`, etc.

The active culture is set by `CultureInfo.CurrentUICulture`. PowerAim's main window listens for changes and rebuilds the UI on language switch.

## Adding a new language

1. Pick the BCP-47 culture code (e.g. `pl-PL` for Polish).
2. Add it to `Cultures.All` in `PowerAim/Localizations/Cultures.cs`:
   ```csharp
   new CultureInfo("pl-PL"),
   ```
3. Create `Locale_pl-PL.json` next to the existing files. Copy `Locale_en-US.json` as a starting template.
4. Translate the values. Keys must match `Locale.json` exactly.
5. Build PowerAim. The source generator regenerates `Locale.cs` and your language appears in the dropdown.

## Translation key naming

- Keys are PascalCase: `AimAssist`, `ImageSizeOverride`
- Help-text variants suffix with `Help` or `Tooltip`: `ImageSizeOverrideHelp`, `AutoPlayToggleTooltip`
- Format strings end with `Format`: `ActivePatternStatusFormat = "Active: {0} (strength {1:0.00})"`

## Verifying a translation

After adding a language:

1. Launch PowerAim
2. **Settings → Language → pick your new culture**
3. Walk through every sidebar page and check that all strings rendered
4. Hover over toggles / sliders — tooltips should also be translated

Untranslated keys fall back to the English value at runtime, so missing entries are easy to spot.

## Submitting a translation

Open a PR to `fgilde/AI-Ming` with the new `Locale_<culture>.json` file and the `Cultures.cs` change. The maintainers will pick it up.

## Tips

- **Match key lengths roughly.** Sidebar button labels are truncated with ellipsis at ~14 characters. Don't write 30-character translations there.
- **Watch for placeholders.** Format strings like `{0}` must appear in your translation, in the right order.
- **The OCR + AutoPlay model names stay English.** Those are model identifiers, not UI text — don't translate `moondream` or `llava:7b`.
