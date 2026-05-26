using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PowerAim;
using PowerAim.Config;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Mapping;
using PowerAim.Types;

namespace PowerAim.UILibrary;

/// <summary>
///     Sidebar-style list of <see cref="ControllerMappingProfile"/>. Each row exposes
///     toggle/edit/duplicate/delete. Edit opens the dedicated <c>MappingEditPage</c> in the main
///     window (analog to <c>OpenTriggerEditor</c>).
/// </summary>
public partial class MappingProfileList : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ProfilesProperty = DependencyProperty.Register(
        nameof(Profiles), typeof(ObservableCollection<ControllerMappingProfile>),
        typeof(MappingProfileList),
        new PropertyMetadata(new ObservableCollection<ControllerMappingProfile>()));

    public ObservableCollection<ControllerMappingProfile> Profiles
    {
        get => (ObservableCollection<ControllerMappingProfile>)GetValue(ProfilesProperty);
        set => SetValue(ProfilesProperty, value);
    }

    public MappingProfileList()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void SwallowMouse(object sender, MouseButtonEventArgs e) => e.Handled = true;

    /// <summary>
    ///     Hotkey routed back from <see cref="AKeyChanger"/>: flip the bound profile's
    ///     <see cref="ControllerMappingProfile.Enabled"/> so the per-row binding doubles as a
    ///     mid-game "switch profile" hotkey. Same pattern as <c>TriggerList.ApplyBindingEnabled</c>.
    /// </summary>
    private void ApplyBindingEnabled(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
    {
        if (e.Value.Sender.Tag is ControllerMappingProfile profile)
            profile.Enabled = !profile.Enabled;
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ControllerMappingProfile p)
            MainWindow.Instance.OpenMappingEditor(p);
    }

    private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ControllerMappingProfile p) return;
        var clone = new ControllerMappingProfile
        {
            Name = p.Name + " (copy)",
            Enabled = false,
            MatchProcess = p.MatchProcess,
            MouseToStickSensitivity = p.MouseToStickSensitivity,
            StickToMouseSensitivity = p.StickToMouseSensitivity,
            StickDeadzone = p.StickDeadzone,
            StickAntiDeadzone = p.StickAntiDeadzone,
            StickMouseExponent = p.StickMouseExponent,
            StickResponseCurve = p.StickResponseCurve,
            InvertMouseY = p.InvertMouseY,
        };
        foreach (var m in p.Mappings)
        {
            clone.Mappings.Add(new InputMapping
            {
                SourceKind = m.SourceKind, SourceCode = m.SourceCode,
                TargetKind = m.TargetKind, TargetCode = m.TargetCode,
                Enabled = m.Enabled,
                Activator = m.Activator,
                LongPressMs = m.LongPressMs,
                ModifierKind = m.ModifierKind,
                ModifierCode = m.ModifierCode,
            });
        }
        Profiles.Add(clone);
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ControllerMappingProfile p) return;
        if (PowerAim.Visuality.MessageDialog.Confirm(
                $"Delete mapping profile '{p.Name}'?",
                "Delete profile",
                owner: Window.GetWindow(this),
                icon: PowerAim.Visuality.MessageDialog.DialogIcon.Warning,
                defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No))
        {
            Profiles.Remove(p);
        }
    }

    private void AddBlank_Click(object sender, RoutedEventArgs e)
    {
        var p = new ControllerMappingProfile { Name = $"Profile {Profiles.Count + 1}" };
        Profiles.Add(p);
        MainWindow.Instance.OpenMappingEditor(p);
    }

    private void AddFpsPreset_Click(object sender, RoutedEventArgs e)
    {
        var preset = MergeFpsPresets();
        Profiles.Add(preset);
        MainWindow.Instance.OpenMappingEditor(preset);
    }

    private static ControllerMappingProfile MergeFpsPresets()
    {
        var kbToPad = MappingPresets.NewFpsKbToPad();
        var padToKb = MappingPresets.NewFpsPadToKb();
        var combined = new ControllerMappingProfile
        {
            Name = "FPS preset",
            Enabled = true,
            MouseToStickSensitivity = kbToPad.MouseToStickSensitivity,
            StickToMouseSensitivity = padToKb.StickToMouseSensitivity,
        };
        foreach (var m in kbToPad.Mappings) combined.Mappings.Add(m);
        foreach (var m in padToKb.Mappings) combined.Mappings.Add(m);
        return combined;
    }
}
