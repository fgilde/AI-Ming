namespace PowerAim.Config;

/// <summary>
///     Built-in aim "feel" presets surfaced in the profile editor's preset dropdown. Selecting one
///     overwrites only the tuning fields of the edited <see cref="AimProfile"/> (its name and
///     activation conditions are left intact). Values are starting points — fine-tune from here.
///     <para>
///     The aim is layered: an always-on core (aim at a detection, move a proportional fraction of the
///     remaining offset each frame) plus two opt-in, ego-motion-immune quality layers — aim-point
///     <see cref="AimProfile.SmoothingMode"/> (None / EMA / adaptive 1€) and stable multi-target
///     <see cref="AimProfile.UseTargetTracking"/> (identity + switch hysteresis, aiming only at the raw
///     last detection). A preset just picks sensible values for those layers.
///     </para>
///     <para>
///     Sensitivity is the per-60Hz approach fraction, measured in <b>FOV-box pixels</b> (resolution-
///     independent; higher = snappier, lower = smoother). By default one pixel ≈ one mouse count (the
///     slider absorbs the game's sensitivity); run the calibration wizard to set
///     <see cref="AimProfile.CalibratedPixelsPerCount"/> and the move converts the offset into EXACT
///     counts so the same slider feels identical in every game.
///     </para>
/// </summary>
public sealed record AimPreset(string Key, string DisplayName, Action<AimProfile> Apply)
{
    public static readonly IReadOnlyList<AimPreset> All = new List<AimPreset>
    {
        new("balanced", "Balanced (recommended)", p =>
        {
            // The safe default: firm enough to pull on target, low enough that it can't overshoot.
            // Adaptive 1€ smoothing, and tracking OFF so it locks on the instant a target appears
            // (target tracking adds a brief acquire delay — better for the deliberate presets). Start here.
            p.SmartAim = true;
            p.Sensitivity = 0.45;
            p.DeadzonePx = 3;
            p.SmoothingMode = AimSmoothingMode.OneEuro;
            p.OneEuroMinCutoff = 1.0;
            p.OneEuroBeta = 0.7;
            p.UseTargetTracking = false; // simple sticky-nearest — immediate, predictable
            p.RandomAimPoint = false;
        }),
        new("smooth", "Smooth tracking", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.30;        // gentle approach
            p.DeadzonePx = 3;
            p.SmoothingMode = AimSmoothingMode.OneEuro;
            p.OneEuroMinCutoff = 0.8;    // a touch more standstill smoothing
            p.OneEuroBeta = 0.7;
            p.UseTargetTracking = true;
            p.CoastFrames = 10;
            p.SwitchFrames = 10;         // very sticky lock
            p.SwitchMarginPct = 0.30;
            p.RandomAimPoint = false;
        }),
        new("snappy", "Snappy / flick", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.75;        // fast convergence
            p.DeadzonePx = 2;
            p.SmoothingMode = AimSmoothingMode.None;   // no smoothing — maximum responsiveness
            p.UseTargetTracking = false;               // snap straight to whoever is nearest
            p.RandomAimPoint = false;
        }),
        new("precise", "Precise (high DPI)", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.15;        // very fine, slow — for high-DPI mice (issue #10)
            p.DeadzonePx = 4;
            p.SmoothingMode = AimSmoothingMode.OneEuro;
            p.OneEuroMinCutoff = 0.6;    // heavy standstill smoothing
            p.OneEuroBeta = 0.5;
            p.UseTargetTracking = true;
            p.CoastFrames = 10;
            p.SwitchFrames = 10;
            p.SwitchMarginPct = 0.35;
            p.RandomAimPoint = false;
        }),
        new("humanized", "Humanized", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.40;
            p.DeadzonePx = 4;
            p.SmoothingMode = AimSmoothingMode.OneEuro;
            p.OneEuroMinCutoff = 1.0;
            p.OneEuroBeta = 0.7;
            p.UseTargetTracking = true;
            p.CoastFrames = 8;
            p.SwitchFrames = 8;
            p.SwitchMarginPct = 0.30;
            p.RandomAimPoint = true;     // random point in the region per engagement
        }),
        new("legacy", "Legacy (no tracking)", p =>
        {
            // The old single-target path — no smart aim. Kept for comparison.
            p.SmartAim = false;
            p.Sensitivity = 0.25;
            p.RandomAimPoint = false;
        }),
    };

    public static AimPreset? ByKey(string key)
    {
        foreach (var preset in All)
            if (preset.Key == key) return preset;
        return null;
    }
}
