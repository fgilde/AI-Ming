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
        {
            // Swallow duplicate events from a double-subscribed keybind control (see KeybindToggleGuard).
            if (!KeybindToggleGuard.ShouldHandle(profile)) return;
            profile.Enabled = !profile.Enabled;
            Notifier.Notify(profile.Name, profile.Enabled);
        }
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
            Name = p.Name + Locale.CopySuffix,
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
                string.Format(Locale.ConfirmDeleteMappingProfileFormat, p.Name),
                Locale.DeleteProfile,
                owner: Window.GetWindow(this),
                icon: PowerAim.Visuality.MessageDialog.DialogIcon.Warning,
                defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No))
        {
            Profiles.Remove(p);
        }
    }

    /// <summary>
    ///     Open a split-button menu offering an Empty profile plus the built-in presets. Reduces
    ///     the previous two-button row to one consistent affordance with all preset variants
    ///     discoverable in one place.
    /// </summary>
    private void NewProfileBtn_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        void Add(string header, Func<ControllerMappingProfile> factory)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) =>
            {
                var draft = factory();
                CreateDraftThenOpen(draft);
            };
            menu.Items.Add(item);
        }

        // Header item — non-clickable label.
        Add(Locale.EmptyProfile,                 () => new ControllerMappingProfile { Name = string.Format(Locale.ProfileDefaultNameFormat, Profiles.Count + 1) });
        menu.Items.Add(new Separator());
        var presetsHeader = new MenuItem { Header = Locale.PresetsHeader, IsEnabled = false, FontSize = 10 };
        menu.Items.Add(presetsHeader);
        Add(Locale.PresetFpsBoth,            MappingPresets.NewFpsBoth);
        Add(Locale.PresetFpsKbToPad,         MappingPresets.NewFpsKbToPad);
        Add(Locale.PresetFpsPadToKb,         MappingPresets.NewFpsPadToKb);
        Add(Locale.PresetDrivingKbToPad,     MappingPresets.NewDrivingKbToPad);
        Add(Locale.PresetControllerAsMouse,  MappingPresets.NewControllerAsMouse);

        menu.PlacementTarget = NewProfileBtn;
        menu.IsOpen = true;
    }

    /// <summary>
    ///     Open the editor on a draft profile that's NOT in the collection yet. Only on Save does
    ///     the commit callback append it — Discard then leaves the collection clean (no zombie
    ///     "Profile 4" rows from accidental clicks). Mirrors the trigger-editor pattern.
    /// </summary>
    private void CreateDraftThenOpen(ControllerMappingProfile draft)
    {
        MainWindow.Instance.OpenMappingEditor(draft, isNew: true, saved =>
        {
            if (saved != null) Profiles.Add(saved);
        });
    }
}
