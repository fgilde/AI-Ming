using PowerAim.AI.Aiming;
using PowerAim.Config;
using PowerAim.UI.Visuality;

namespace PowerAim.App.Tests;

[Collection(nameof(TestConfig))]
public class LiveTuningTests
{
    [Fact]
    public void Reset_restores_the_response_defaults_and_nothing_else()
    {
        var profile = new AimProfile { Name = "tuned", Sensitivity = 0.9, AimSpeedMultiplier = 3, LatencyCompensationMs = 80,
            ReactionDelayMs = 120, DeadzonePx = 9, AimEngine = AimEngine.V2, StickyRadiusPx = 300 };
        var pad = new ControllerSettings { MaxOutputPct = 40, ManualOverridePct = 60, AntiStrafePct = 30, StickTurnSpeedPxPerSec = 900, GameDeadzonePct = 7 };

        LiveTuningWindow.ResetResponse(profile, pad);

        var fresh = new AimProfile();
        var freshPad = new ControllerSettings();
        Assert.Equal((fresh.Sensitivity, fresh.AimSpeedMultiplier, fresh.LatencyCompensationMs, fresh.ReactionDelayMs, fresh.DeadzonePx),
            (profile.Sensitivity, profile.AimSpeedMultiplier, profile.LatencyCompensationMs, profile.ReactionDelayMs, profile.DeadzonePx));
        Assert.Equal((freshPad.MaxOutputPct, freshPad.ManualOverridePct, freshPad.AntiStrafePct, freshPad.StickTurnSpeedPxPerSec),
            (pad.MaxOutputPct, pad.ManualOverridePct, pad.AntiStrafePct, pad.StickTurnSpeedPxPerSec));
        // Not response tuning: engine choice, sticky radius and the game's deadzone stay.
        Assert.Equal((AimEngine.V2, 300.0, 7.0), (profile.AimEngine, profile.StickyRadiusPx, pad.GameDeadzonePct));
    }

    [Fact]
    public void The_window_shows_the_profile_sliders_and_writes_through() => Sta.Run(() =>
    {
        TestConfig.Ensure();
        var profile = new AimProfile { Name = "live" };
        var window = new LiveTuningWindow(profile) { Left = -20000, Top = -20000, ShowActivated = false };
        try
        {
            window.Show();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            var slider = FindSlider(window, Locale.AimLatencyCompensation);
            slider.Value = 55;
            Assert.Equal(55, profile.LatencyCompensationMs);
        }
        finally { window.Close(); }
    });

    private static PowerAim.UI.UILibrary.ASlider FindSlider(System.Windows.DependencyObject root, string label)
    {
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root).OfType<System.Windows.DependencyObject>())
        {
            if (child is PowerAim.UI.UILibrary.ASlider s && s.Label == label) return s;
            try { return FindSlider(child, label); } catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("slider not found: " + label);
    }
}
