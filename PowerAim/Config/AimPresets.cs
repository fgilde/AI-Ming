namespace PowerAim.Config;

/// <summary>
///     Built-in aim "feel" presets surfaced in the profile editor's preset dropdown. Selecting one
///     overwrites only the tuning fields of the edited <see cref="AimProfile"/> (its name and
///     activation conditions are left intact). Values are starting points from the tracking/
///     smoothing research write-up — the user is expected to fine-tune from here.
/// </summary>
public sealed record AimPreset(string Key, string DisplayName, Action<AimProfile> Apply)
{
    public static readonly IReadOnlyList<AimPreset> All = new List<AimPreset>
    {
        new("smooth", "Smooth tracking", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.20;        // gentle approach
            p.DeadzonePx = 3;
            p.CoastFrames = 8;           // bridge YOLO drop-outs
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
            p.Sensitivity = 0.55;        // fast convergence
            p.DeadzonePx = 2;
            p.CoastFrames = 5;
            p.SwitchFrames = 3;          // switches targets quickly
            p.SwitchMarginPct = 0.15;
            p.LeadTimeMs = 20;           // slight lead for moving targets
            p.UseOneEuro = true;
            p.OneEuroMinCutoff = 1.5;    // more responsive, less smoothing
            p.OneEuroBeta = 1.0;
            p.RandomAimPoint = false;
        }),
        new("precise", "Precise (high DPI)", p =>
        {
            p.SmartAim = true;
            p.Sensitivity = 0.08;        // very fine, slow — for high-DPI mice (issue #10)
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
            p.Sensitivity = 0.18;
            p.DeadzonePx = 4;
            p.CoastFrames = 7;
            p.SwitchFrames = 8;
            p.SwitchMarginPct = 0.30;
            p.LeadTimeMs = 15;
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
