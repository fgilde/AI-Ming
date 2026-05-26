using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.InputLogic.Mapping;
using PowerAim.Extensions; // AddToggleWithKeyBind + BindTo helpers
using RadioButton = System.Windows.Controls.RadioButton;

namespace PowerAim.Visuality;

/// <summary>
///     Top-level mapping page. Deliberately small — it owns just three things:
///     <list type="number">
///       <item>The "Mapping active" master toggle (with global hotkey via <c>AddToggleWithKeyBind</c>).</item>
///       <item>A radio-button direction picker (Both / KB→Pad / Pad→KB).</item>
///       <item>A profile list (<see cref="UILibrary.MappingProfileList"/>) that delegates the
///             actual editor opening to <c>MainWindow.OpenMappingEditor</c>.</item>
///     </list>
///     The full editor lives in a separate page (<c>MappingEditPage</c>) hosting
///     <c>UILibrary.MappingEdit</c> — same pattern as the trigger editor.
/// </summary>
public partial class MappingPage : System.Windows.Controls.UserControl
{
    private bool _activeToggleBuilt;

    public MappingPage()
    {
        InitializeComponent();
        BuildDirectionPicker();
        BindProfiles();
        // BuildActiveToggle needs MainWindow.BindingManager which isn't reliably available during
        // XAML construction (the MappingPage child of MainWindow can be instantiated before
        // Application.MainWindow is set). Defer to Loaded — runs once the visual tree is wired up
        // so BindingManager is guaranteed non-null.
        Loaded += (_, _) =>
        {
            EnsureActiveToggleBuilt();
            UpdateEngineStatus();
        };
        MappingEngine.Instance.PropertyChanged += (_, _) =>
            Dispatcher.BeginInvoke(new Action(UpdateEngineStatus));
        // React to MappingActive flips so the status line refreshes (e.g. when toggled by hotkey).
        if (AppConfig.Current?.ToggleState != null)
        {
            AppConfig.Current.ToggleState.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ToggleState.MappingActive))
                    Dispatcher.BeginInvoke(new Action(UpdateEngineStatus));
            };
        }
    }

    /// <summary>
    ///     Master "Mapping active" toggle — uses <c>AddToggleWithKeyBind</c> so the hotkey,
    ///     persisted state and visual stay in sync via the existing central plumbing (same as
    ///     Global Active / Auto Trigger / Anti Recoil). Runs from <see cref="Loaded"/> so that
    ///     <c>MainWindow.Instance.BindingManager</c> is guaranteed available.
    /// </summary>
    private void EnsureActiveToggleBuilt()
    {
        if (_activeToggleBuilt) return;
        var bm = MainWindow.Instance?.BindingManager;
        if (bm == null) return;
        ActiveHost.Children.Clear();
        ActiveHost.AddToggleWithKeyBind("Mapping active", "MappingActive", bm)
            .BindTo(() => AppConfig.Current.ToggleState.MappingActive);
        _activeToggleBuilt = true;
    }

    private void BuildDirectionPicker()
    {
        DirectionStack.Children.Clear();
        var options = new (string label, MappingDirection dir, string tip)[]
        {
            ("Both ↔",                MappingDirection.Both,                 "Both directions fire — KB+M drives the virtual controller AND your physical controller drives KB+M."),
            ("Keyboard → Controller", MappingDirection.KeyboardToController, "Only KB+M-sourced mappings fire. Use to play gamepad-only games with KB+M."),
            ("Controller → Keyboard", MappingDirection.ControllerToKeyboard, "Only controller-sourced mappings fire. Use to play KB+M-only games with a controller."),
        };
        foreach (var opt in options)
        {
            var b = new RadioButton
            {
                Content = opt.label,
                GroupName = "MappingDirection",
                Margin = new Thickness(0, 0, 14, 0),
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13,
                IsChecked = AppConfig.Current?.MappingDirection == opt.dir,
                ToolTip = opt.tip,
                Tag = opt.dir,
            };
            b.SetResourceReference(RadioButton.ForegroundProperty, "FluentTextPrimary");
            b.Checked += (_, _) =>
            {
                if (b.Tag is MappingDirection d && AppConfig.Current != null)
                    AppConfig.Current.MappingDirection = d;
            };
            DirectionStack.Children.Add(b);
        }
    }

    /// <summary>
    ///     Hook ProfileList up to whichever collection lives on the currently-loaded
    ///     <see cref="AppConfig.Current"/>. Subtle ordering trap: <see cref="MappingPage"/> is a
    ///     child of <see cref="MainWindow"/>, which means the WPF parser instantiates it inside
    ///     <c>MainWindow.InitializeComponent()</c> — which runs BEFORE <c>AppConfig.Load()</c> in
    ///     the MainWindow constructor. So at MappingPage-ctor time, <c>AppConfig.Current</c> is
    ///     <c>null</c>. The previous code bailed on that null and never wired the
    ///     <c>ConfigLoaded</c> handler, which is why profiles added via "+ FPS preset" or
    ///     "+ New profile" never persisted — the auto-save hook was missing entirely.
    /// </summary>
    private void BindProfiles()
    {
        // Always subscribe — handler will fire when AppConfig.Load completes, even if Current is
        // null right now.
        AppConfig.ConfigLoaded += (_, e) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AttachToActiveConfig(e.Value);
            }));
        };
        // If Current is already loaded (e.g. config reload), attach immediately.
        if (AppConfig.Current != null)
            AttachToActiveConfig(AppConfig.Current);
    }

    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _autoSaveHandler;
    private ObservableCollection<ControllerMappingProfile>? _wiredProfiles;

    private void AttachToActiveConfig(AppConfig cfg)
    {
        var profiles = cfg.ControllerMappingProfiles;
        if (profiles == null) return;
        // Detach from any previously-wired collection (config reload case).
        if (_wiredProfiles != null && _autoSaveHandler != null)
        {
            try { _wiredProfiles.CollectionChanged -= _autoSaveHandler; } catch { }
        }
        _wiredProfiles = profiles;
        ProfileList.Profiles = profiles;
        _autoSaveHandler = (_, _) => AppConfig.Current?.Save();
        profiles.CollectionChanged += _autoSaveHandler;
        BuildDirectionPicker();
        UpdateEngineStatus();
        // Also persist when the master toggle is flipped — without this, MappingActive=true never
        // hits disk unless the user uses the Exit button (and we can't rely on that).
        if (cfg.ToggleState != null)
        {
            cfg.ToggleState.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ToggleState.MappingActive))
                    AppConfig.Current?.Save();
            };
        }
        // Also persist when individual profiles fire INotifyPropertyChanged (Enabled toggle, name
        // edit, MatchProcess change). Re-bind on collection change so newly-added profiles are
        // covered too.
        AttachProfilePropertyAutoSave(profiles);
        profiles.CollectionChanged += (_, _) => AttachProfilePropertyAutoSave(profiles);
    }

    private void AttachProfilePropertyAutoSave(ObservableCollection<ControllerMappingProfile> profiles)
    {
        foreach (var p in profiles)
        {
            p.PropertyChanged -= ProfilePropAutoSave;
            p.PropertyChanged += ProfilePropAutoSave;
        }
    }
    private void ProfilePropAutoSave(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        AppConfig.Current?.Save();
    }

    private void UpdateEngineStatus()
    {
        var engine = MappingEngine.Instance;
        var active = engine.ActiveProfile?.Name ?? "(none)";
        bool masterOn = AppConfig.Current?.ToggleState?.MappingActive == true;
        bool anyEnabledProfile = AppConfig.Current?.ControllerMappingProfiles?.Any(p => p.Enabled) == true;
        string hint = "";
        if (!masterOn && anyEnabledProfile)
            hint = "  —  Mapping active is OFF. Flip the master toggle above to activate the profile.";
        else if (masterOn && !anyEnabledProfile)
            hint = "  —  No profile is enabled. Toggle one in the list below.";
        EngineStatusText.Text = $"Engine: {engine.Status} · Active profile: {active}{hint}";
    }
}
