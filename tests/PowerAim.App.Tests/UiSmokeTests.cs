using System.Windows;
using System.Windows.Controls;
using PowerAim.AI;
using PowerAim.AI.Aiming;
using PowerAim.Config;
using PowerAim.UI.UILibrary;
using PowerAim.UI.Visuality;

namespace PowerAim.App.Tests;

/// <summary>The new dialogs and tune-panel entries build, bind and show without errors.</summary>
public class UiSmokeTests
{
    private static void EnsureConfig()
    {
        TestConfig.Ensure();
    }

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var d in Tree(child)) yield return d;
    }

    /// <summary>Titles of the helper-built rows (sliders, toggles, dropdowns, buttons).</summary>
    private static List<string> Titles(DependencyObject root) => Tree(root).Select(d => d switch
    {
        ASlider s => s.Label,
        AToggle t => t.Text,
        ADropdown dd => dd.DropdownTitle.Content?.ToString(),
        APButton b => b.Text,
        _ => null,
    }).OfType<string>().ToList();

    private static void ShowOffScreen(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -20000;
        w.Top = -20000;
        w.ShowActivated = false;
        w.Show();
        w.UpdateLayout();
        Pump();
    }

    /// <summary>Let deferred work run: the panel helpers configure rows only once they are visible.</summary>
    private static void Pump() => System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { },
        System.Windows.Threading.DispatcherPriority.ContextIdle);

    private static Window Host(UIElement content)
    {
        var w = new Window { Content = content, Width = 700, Height = 1400 };
        ShowOffScreen(w);
        return w;
    }

    [Fact]
    public void Class_rules_dialog_shows_a_localised_column_per_rule_field() => Sta.Run(() =>
    {
        EnsureConfig();
        var dialog = new ClassRulesDialog();
        dialog.Grid.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<ClassTargetSpec>
            { new() { ClassId = 0, Name = "enemy" } };
        try
        {
            ShowOffScreen(dialog);
            var headers = dialog.Grid.Columns.Select(c => c.Header?.ToString()).ToList();
            Assert.Contains(Locale.AimClassPriority, headers);
            Assert.Contains(Locale.AimClassAnchorY, headers);
            Assert.Equal(10, headers.Count);
            Assert.True(dialog.Grid.Columns.Single(c => (string)c.Header == Locale.AimClassId).IsReadOnly);
        }
        finally { dialog.Close(); }
    });

    [Fact]
    public void Team_colour_dialog_offers_every_setting_and_writes_through() => Sta.Run(() =>
    {
        EnsureConfig();
        var dialog = new TeamColorDialog();
        try
        {
            ShowOffScreen(dialog);
            var titles = Titles(dialog.SettingsPanel);
            foreach (var t in new[] { Locale.TeamColorMode, Locale.TeamHueMin, Locale.TeamValMax, Locale.TeamSampleRight, Locale.TeamMatch })
                Assert.Contains(t, titles);

            var hueMax = Tree(dialog.SettingsPanel).OfType<ASlider>().Single(s => s.Label == Locale.TeamHueMax);
            hueMax.Value = 222;
            Assert.Equal(222, AppConfig.Current.AISettings.TeamHueMax);
        }
        finally { dialog.Close(); }
    });

    [Fact]
    public void Aim_profile_tune_panel_shows_the_new_options_when_they_apply() => Sta.Run(() =>
    {
        EnsureConfig();
        var profile = new AimProfile { Name = "smoke", SmartAim = true, UseTargetTracking = true };
        var edit = new AimProfileEdit { Profile = profile };
        using var _ = new Closer(Host(edit));
        var titles = Titles(edit.TunePanel);

        foreach (var t in new[] { Locale.AimEngineLabel, Locale.AimLatencyCompensation, Locale.AimMinTrackedFrames,
                     Locale.AimTrackConfidenceDecay, Locale.AimClassRules, Locale.AimDynamicOffset,
                     Locale.AimAdditionalOffsetX, Locale.AimPoseTarget })
            Assert.Contains(t, titles);
        Assert.DoesNotContain(Locale.AimDynamicMinArea, titles);
        Assert.DoesNotContain(Locale.AimKeypointConfidence, titles);

        profile.DynamicOffset = true;
        profile.PoseTarget = PoseAimTarget.Head;
        Pump();
        titles = Titles(edit.TunePanel);
        Assert.Contains(Locale.AimDynamicMinArea, titles);
        Assert.Contains(Locale.AimKeypointConfidence, titles);

        var latency = Tree(edit.TunePanel).OfType<ASlider>().Single(s => s.Label == Locale.AimLatencyCompensation);
        latency.Value = 45;
        Assert.Equal(45, profile.LatencyCompensationMs);
    });

    [Fact]
    public void Aim_profile_resolution_and_engine_survive_the_mirror_to_globals() => Sta.Run(() =>
    {
        EnsureConfig();
        var profile = new AimProfile { Name = "mirror", AimEngine = AimEngine.V2, LatencyCompensationMs = 60, MinTrackedFrames = 4 };
        profile.Apply();

        Assert.Equal(AimEngine.V2, AppConfig.Current.AISettings.AimEngine);
        Assert.Equal(60, AppConfig.Current.AISettings.LatencyCompensationMs);
        Assert.Equal(4, AppConfig.Current.AISettings.MinTrackedFrames);

        var copy = new AimProfile { Name = "copy" };
        copy.CaptureFromGlobals();
        Assert.Equal((AimEngine.V2, 60.0, 4), (copy.AimEngine, copy.LatencyCompensationMs, copy.MinTrackedFrames));
    });

    [Fact]
    public void Trigger_editor_offers_dwell_and_hold() => Sta.Run(() =>
    {
        EnsureConfig();
        var trigger = new ActionTrigger();
        var edit = new TriggerEdit { Trigger = trigger };
        using var _ = new Closer(Host(edit));
        var titles = Titles(edit.TimeSettings);

        Assert.Contains(Locale.AutoTriggerDwell, titles);
        Assert.Contains(Locale.AutoTriggerHoldWhileTrue, titles);

        Tree(edit.TimeSettings).OfType<ASlider>().Single(s => s.Label == Locale.AutoTriggerDwell).Value = 40;
        Assert.Equal(40, trigger.DwellMs);
    });

    private sealed class Closer(Window w) : IDisposable
    {
        public void Dispose() => w.Close();
    }
}
