namespace PowerAim.Config;

/// <summary>
///     Built-in aim "feel" presets surfaced in the profile editor's preset dropdown. Selecting one
///     overwrites only the tuning fields of the edited <see cref="AimProfile"/> (its name and
///     activation conditions are left intact). Values are starting points — fine-tune from here.
///     <para>
///     Sensitivity is the per-60Hz-frame approach fraction of the target's offset measured in
///     <b>FOV-box pixels</b> (resolution-INDEPENDENT after the capture-scale fix — it no longer scales
///     with screen width). Higher = snappier / firmer settle, lower = smoother. The exact "right"
///     value still depends on the game's in-game mouse sensitivity (the app moves a proportional
///     amount and the closed loop converges; it does not calibrate counts-to-pixels), so treat these
///     as safe baselines and nudge to taste. Far targets are acquired fast regardless (the per-frame
///     move is clamped), so the value mainly governs the close-range settle.
///     </para>
/// </summary>
public sealed record AimPreset(string Key, string DisplayName, Action<AimProfile> Apply)
{
    public static readonly IReadOnlyList<AimPreset> All = new List<AimPreset>
    {
        new("balanced", "Balanced (recommended)", p =>
        {
            // The safe default: firm enough to pull on target, low enough that it can't overshoot/
            // run away on typical FPS sensitivities. Start here.
            p.SmartAim = true;
            p.Sensitivity = 0.45;
            p.DeadzonePx = 3;
            p.CoastFrames = 8;           // bridge YOLO drop-outs
            p.SwitchFrames = 8;          // sticky target
            p.SwitchMarginPct = 0.30;
            p.LeadTimeMs = 0;            // no velocity lead — avoids ego-motion drift
            p.UseOneEuro = true;
            p.OneEuroMinCutoff = 1.0;
            p.OneEuroBeta = 0.7;
            p.RandomAimPoint = false;
        }),
        new("smooth", "Smooth tracking", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.30;        // gentle approach
            p.DeadzonePx = 3;
            p.CoastFrames = 8;
            p.SwitchFrames = 8;          // very sticky target
            p.SwitchMarginPct = 0.30;
            p.LeadTimeMs = 0;
            p.UseOneEuro = true;
            p.OneEuroMinCutoff = 1.0;
            p.OneEuroBeta = 0.7;
            p.RandomAimPoint = false;
        }),
        new("snappy", "Snappy / flick", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.75;        // fast convergence
            p.DeadzonePx = 2;
            p.CoastFrames = 5;
            p.SwitchFrames = 3;          // switches targets quickly
            p.SwitchMarginPct = 0.15;
            p.LeadTimeMs = 0;
            p.UseOneEuro = true;
            p.OneEuroMinCutoff = 1.5;    // more responsive, less smoothing
            p.OneEuroBeta = 1.0;
            p.RandomAimPoint = false;
        }),
        new("precise", "Precise (high DPI)", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.15;        // very fine, slow — for high-DPI mice (issue #10)
            p.DeadzonePx = 4;
            p.CoastFrames = 10;
            p.SwitchFrames = 10;
            p.SwitchMarginPct = 0.35;
            p.LeadTimeMs = 0;
            p.UseOneEuro = true;
            p.OneEuroMinCutoff = 0.6;    // heavy standstill smoothing
            p.OneEuroBeta = 0.5;
            p.RandomAimPoint = false;
        }),
        new("humanized", "Humanized", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.40;
            p.DeadzonePx = 4;
            p.CoastFrames = 7;
            p.SwitchFrames = 8;
            p.SwitchMarginPct = 0.30;
            p.LeadTimeMs = 0;
            p.UseOneEuro = true;
            p.OneEuroMinCutoff = 1.0;
            p.OneEuroBeta = 0.7;
            p.RandomAimPoint = true;     // random point in the region per engagement
        }),
        new("legacy", "Legacy (no tracking)", p =>
        {
            // The old single-target path — no SORT tracker / 1€ filter. Kept for comparison.
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
