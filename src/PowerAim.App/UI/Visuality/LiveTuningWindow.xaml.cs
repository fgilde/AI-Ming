using System.Windows;
using System.Windows.Input;
using PowerAim.AI;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Extensions;
using Nextended.UI.WPF.Extensions;

namespace PowerAim.UI.Visuality;

/// <summary>
///     Small always-on-top window with the response sliders of one aim profile and of the gamepad output, to
///     tune them while the preview or the game runs. Edits go straight into the profile like its editor page.
/// </summary>
public partial class LiveTuningWindow
{
    private static LiveTuningWindow? _open;
    private readonly AimProfile _profile;

    public LiveTuningWindow(AimProfile profile)
    {
        _profile = profile;
        InitializeComponent();
        DataContext = this;
        Build();
        // Another config replaces the profile and pad objects bound here: edits would go nowhere.
        AppConfig.ConfigLoaded += OnConfigLoaded;
        Closed += (_, _) => AppConfig.ConfigLoaded -= OnConfigLoaded;
    }

    private void OnConfigLoaded(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Close);

    /// <summary>Open (or bring forward) the window for the profile driving the aim, else the selected or first one.</summary>
    public static void Open(Window? owner)
    {
        var aim = AppConfig.Current.AimSettings;
        var profile = aim.Profiles.FirstOrDefault(p => p.Id == AimProfileManager.Instance.EffectiveProfileId)
                      ?? aim.ActiveProfile ?? aim.Profiles.FirstOrDefault();
        if (profile == null) return;
        if (_open != null && ReferenceEquals(_open._profile, profile)) { _open.Activate(); return; }
        _open?.Close();
        _open = new LiveTuningWindow(profile) { Owner = owner };
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    /// <summary>Back to the defaults of a new profile / new controller settings — response tuning only.</summary>
    public static void ResetResponse(AimProfile profile, ControllerSettings pad)
    {
        var p = new AimProfile();
        profile.Sensitivity = p.Sensitivity;
        profile.AimSpeedMultiplier = p.AimSpeedMultiplier;
        profile.LatencyCompensationMs = p.LatencyCompensationMs;
        profile.ReactionDelayMs = p.ReactionDelayMs;
        profile.DeadzonePx = p.DeadzonePx;
        var c = new ControllerSettings();
        pad.MaxOutputPct = c.MaxOutputPct;
        pad.ManualOverridePct = c.ManualOverridePct;
        pad.AntiStrafePct = c.AntiStrafePct;
        pad.StickTurnSpeedPxPerSec = c.StickTurnSpeedPxPerSec;
    }

    private void Build()
    {
        SettingsPanel.Children.Clear();
        var pad = AppConfig.Current.ControllerSettings;

        SettingsPanel.AddTitle(string.IsNullOrWhiteSpace(_profile.Name) ? "—" : _profile.Name, false);
        SettingsPanel.AddSlider(Locale.MouseSensitivity, Locale.Sensitivity, 0.001, 0.001, 0.0001, 1, false, 4, false)
            .BindTo(() => _profile.Sensitivity);
        SettingsPanel.AddSlider(Locale.AimSpeedMultiplier, "×", 0.5, 0.1, 1, 10, false, 1, false)
            .BindTo(() => _profile.AimSpeedMultiplier);
        SettingsPanel.AddSlider(Locale.AimLatencyCompensation, "ms", 5, 5, 0, 200, false, 0, false)
            .InitWith(s => s.ToolTip = Locale.AimLatencyCompensationTooltip)
            .BindTo(() => _profile.LatencyCompensationMs);
        SettingsPanel.AddSlider(Locale.AimReactionDelay, "ms", 10, 10, 0, 500, false, 0, false)
            .BindTo(() => _profile.ReactionDelayMs);
        SettingsPanel.AddSlider(Locale.AimDeadzone, Locale.Pixels, 1, 1, 0, 20)
            .BindTo(() => _profile.DeadzonePx);

        SettingsPanel.AddTitle(Locale.StickOutputTitle, false);
        SettingsPanel.AddSlider(Locale.StickTurnSpeed, "px/s", 50, 100, 100, 20000, false, 0, false)
            .BindTo(() => pad.StickTurnSpeedPxPerSec);
        SettingsPanel.AddSlider(Locale.StickMaxOutput, "%", 1, 1, 5, 100, false, 0, false)
            .BindTo(() => pad.MaxOutputPct);
        SettingsPanel.AddSlider(Locale.StickManualOverride, "%", 1, 1, 0, 100, false, 0, false)
            .BindTo(() => pad.ManualOverridePct);
        SettingsPanel.AddSlider(Locale.StickAntiStrafe, Locale.Amount, 1, 1, 0, 50, false, 0, false)
            .BindTo(() => pad.AntiStrafePct);

        SettingsPanel.AddButton(Locale.ResetResponseDefaults).Reader.Click += (_, _) =>
        {
            ResetResponse(_profile, pad);
            Build();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
