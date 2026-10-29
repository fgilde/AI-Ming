using System.Windows;
using System.Windows.Input;
using PowerAim.AI;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Extensions;
using Nextended.UI.WPF.Extensions;

namespace PowerAim.UI.Visuality;

/// <summary>Editor for the team colour filter in <see cref="AISettings"/> (mode, HSV range, sample strips).</summary>
public partial class TeamColorDialog
{
    public TeamColorDialog()
    {
        InitializeComponent();
        DataContext = this;

        var ai = AppConfig.Current.AISettings;
        SettingsPanel.AddDropdown(Locale.TeamColorMode, ai.TeamColorMode, v => ai.TeamColorMode = v);
        SettingsPanel.AddSlider(Locale.TeamHueMin, "°", 1, 5, 0, 360, false, 0, false).BindTo(() => ai.TeamHueMin);
        SettingsPanel.AddSlider(Locale.TeamHueMax, "°", 1, 5, 0, 360, false, 0, false)
            .InitWith(s => s.ToolTip = Locale.TeamHueTooltip).BindTo(() => ai.TeamHueMax);
        SettingsPanel.AddSlider(Locale.TeamSatMin, "%", 1, 5, 0, 100, false, 0, false).BindTo(() => ai.TeamSatMin);
        SettingsPanel.AddSlider(Locale.TeamSatMax, "%", 1, 5, 0, 100, false, 0, false).BindTo(() => ai.TeamSatMax);
        SettingsPanel.AddSlider(Locale.TeamValMin, "%", 1, 5, 0, 100, false, 0, false).BindTo(() => ai.TeamValMin);
        SettingsPanel.AddSlider(Locale.TeamValMax, "%", 1, 5, 0, 100, false, 0, false).BindTo(() => ai.TeamValMax);
        SettingsPanel.AddToggle(Locale.TeamSampleAbove).BindTo(() => ai.TeamSampleAbove);
        SettingsPanel.AddToggle(Locale.TeamSampleBelow).BindTo(() => ai.TeamSampleBelow);
        SettingsPanel.AddToggle(Locale.TeamSampleLeft).BindTo(() => ai.TeamSampleLeft);
        SettingsPanel.AddToggle(Locale.TeamSampleRight).BindTo(() => ai.TeamSampleRight);
        SettingsPanel.AddSlider(Locale.TeamSampleExtent, "%", 1, 5, 1, 200, false, 0, false)
            .InitWith(s => s.ToolTip = Locale.TeamSampleExtentTooltip).BindTo(() => ai.TeamSampleExtentPct);
        SettingsPanel.AddSlider(Locale.TeamMatch, "%", 1, 5, 1, 100, false, 0, false)
            .InitWith(s => s.ToolTip = Locale.TeamMatchTooltip).BindTo(() => ai.TeamMatchPct);
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
