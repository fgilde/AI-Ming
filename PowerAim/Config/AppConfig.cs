using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using PowerAim.AILogic;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
using PowerAim.InputLogic.HidHide;
using PowerAim.MouseMovementLibraries.GHubSupport.dist;
using PowerAim.Types;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;


namespace PowerAim.Config;

public class AppConfig : BaseSettings
{
    public const string DefaultConfigPath = "bin\\configs\\Default.cfg";

    [JsonIgnore]
    public string? Path
    {
        get;
        set => SetField(ref field, value);
    }

    public static AppConfig Current { get; private set; }

    public string LastLoadedModel { get; set; } = "N/A";

    public string LastLoadedConfig = "N/A";
    public string SuggestedModelName => SliderSettings.SuggestedModel;
    
    /// <summary>User-chosen accent colour (ARGB hex). Drives the whole theme on top of the neutral base.</summary>
    public string AccentColorHex { get; set; } = ColorToHex(ThemePalette.PurplePalette.AccentColor);

    /// <summary>Accent colour used while the global "active" toggle is on (ARGB hex).</summary>
    public string ActiveAccentColorHex { get; set; } = ColorToHex(ThemePalette.GreenPalette.AccentColor);

    /// <summary><see cref="System.Windows.Media.Color"/> view over <see cref="AccentColorHex"/> for the picker.</summary>
    [JsonIgnore]
    public System.Windows.Media.Color AccentColorValue
    {
        get => HexToColor(AccentColorHex, ThemePalette.PurplePalette.AccentColor);
        set => AccentColorHex = ColorToHex(value);
    }

    [JsonIgnore]
    public System.Windows.Media.Color ActiveAccentColorValue
    {
        get => HexToColor(ActiveAccentColorHex, ThemePalette.GreenPalette.AccentColor);
        set => ActiveAccentColorHex = ColorToHex(value);
    }

    private static string ColorToHex(System.Windows.Media.Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private static System.Windows.Media.Color HexToColor(string hex, System.Windows.Media.Color fallback)
    {
        try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;
    public BindingSettings BindingSettings { get; set; } = new BindingSettings();
    public SliderSettings SliderSettings { get; set; } = new SliderSettings();
    public ToggleState ToggleState { get; set; } = new ToggleState();
    public MinimizeState MinimizeState { get; set; } = new MinimizeState();
    public DropdownState DropdownState { get; set; } = new DropdownState();
    public ColorState ColorState { get; set; } = new ColorState();
    public AntiRecoilSettings AntiRecoilSettings { get; set; } = new AntiRecoilSettings();
    public FileLocationState FileLocationState { get; set; } = new FileLocationState();
    public OllamaSettings OllamaSettings { get; set; } = new OllamaSettings();
    public AISettings AISettings { get; set; } = new AISettings();
    public ActiveProcessSettings ActiveProcessSettings { get; set; } = new ActiveProcessSettings();
    public CrosshairSettings CrosshairSettings { get; set; } = new CrosshairSettings();
    public OcrSettings OcrSettings { get; set; } = new OcrSettings();
    public ReplaySettings ReplaySettings { get; set; } = new ReplaySettings();
    public AutoPlayLearningSettings AutoPlayLearningSettings { get; set; } = new AutoPlayLearningSettings();
    public LayoutConfiguration LayoutConfiguration { get; set; } = new LayoutConfiguration();

    public ObservableCollection<ControllerMappingProfile> ControllerMappingProfiles
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>
    ///     Which direction the mapping engine should apply right now. Independent of profile
    ///     contents — a single profile can declare button↔key pairs and the user toggles which
    ///     side drives without re-editing the profile.
    /// </summary>
    public MappingDirection MappingDirection
    {
        get;
        set => SetField(ref field, value);
    } = MappingDirection.Both;

    /// <summary>
    ///     Per-game rules that pause aim assist while a HUD OCR reading matches (e.g. "scoped",
    ///     "knife"). Evaluated by <see cref="PowerAim.AILogic.AimDisengage"/>.
    /// </summary>
    public ObservableCollection<AimDisengageRule> AimDisengageRules
    {
        get;
        set => SetField(ref field, value);
    } = new();

    public ObservableCollection<ActionTrigger> Triggers
    {
        get;
        set => SetField(ref field, value);
    } = new()
    {
        new ActionTrigger()
        {
            Name = "Primary Fire",
            Enabled = true,
            TriggerKeys = [GamepadSlider.LeftTrigger, MouseButtons.Right],
            TriggerKeysOperator = KeyOperator.Or,
            Delay = 0,
            BreakTime = 0,
            ExecutionIntersectionCheck = TriggerCheck.HeadIntersectingCenter,
            ExecutionIntersectionArea = RelativeRect.Default
        },
    };

    public ObservableCollection<AutoPlayProfile> AutoPlayProfiles
    {
        get;
        set => SetField(ref field, value);
    } = new()
    {
        new AutoPlayProfile
        {
            Name = "FPS Default",
            Enabled = false,
            OllamaModel = "moondream",
            DecisionInterval = 0.3,
            GameContext = "First-person shooter game. Control a player: move, jump, sprint, aim, shoot enemies. Enemies are other players/characters. Move towards objectives, engage enemies when visible, take cover when needed. Be aggressive - keep moving and shooting.",
            Actions = new ObservableCollection<AutoPlayAction>
            {
                new AutoPlayAction { Name = "move_forward", Description = "Move forward - use when path is clear or approaching enemies/objectives", Keys = [Keys.W, GamepadButton.Up], ActionType = AutoPlayActionType.Continuous },
                new AutoPlayAction { Name = "move_backward", Description = "Move backward - use to retreat from danger or reposition", Keys = [Keys.S, GamepadButton.Down], ActionType = AutoPlayActionType.Continuous },
                new AutoPlayAction { Name = "move_left", Description = "Strafe left - use to dodge or circle enemies", Keys = [Keys.A, GamepadButton.Left], ActionType = AutoPlayActionType.Continuous },
                new AutoPlayAction { Name = "move_right", Description = "Strafe right - use to dodge or circle enemies", Keys = [Keys.D, GamepadButton.Right], ActionType = AutoPlayActionType.Continuous },
                new AutoPlayAction { Name = "sprint", Description = "Sprint faster - combine with movement when covering distance", Keys = [Keys.ShiftKey, GamepadButton.LeftThumb], ActionType = AutoPlayActionType.Modifier },
                new AutoPlayAction { Name = "jump", Description = "Jump - use to clear obstacles or gain height advantage", Keys = [Keys.Space, GamepadButton.A], ActionType = AutoPlayActionType.Instant },
                new AutoPlayAction { Name = "shoot", Description = "Fire weapon - use when enemy is visible on screen", Keys = [MouseButtons.Left, GamepadSlider.RightTrigger], ActionType = AutoPlayActionType.Continuous },
                new AutoPlayAction { Name = "aim", Description = "Aim down sights - use for precision when enemy is at medium/long range", Keys = [MouseButtons.Right, GamepadSlider.LeftTrigger], ActionType = AutoPlayActionType.Modifier },
                new AutoPlayAction { Name = "reload", Description = "Reload weapon - use when safe and ammo might be low", Keys = [Keys.R, GamepadButton.X], ActionType = AutoPlayActionType.Instant },
                new AutoPlayAction { Name = "crouch", Description = "Crouch - use for cover or stealth", Keys = [Keys.ControlKey, GamepadButton.B], ActionType = AutoPlayActionType.Toggle },
            }
        }
    };

    public string? Language
    {
        get;
        set => SetField(ref field, value);
    }

    public CaptureSource CaptureSource
    {
        get;
        set => SetField(ref field, value);
    } = AILogic.CaptureSource.MainScreen();

    public static AppConfig Load(string? path = null)
    {
        //Current = new AppConfig();
        //return Current;
        if (path == null)
        {
            path = GetLastConfigPath();
            if(path == null || !File.Exists(path))
                path = DefaultConfigPath;
        }
        try
        {
            if (File.Exists(path))
            {
                SaveLastConfigPath(path);
                string json = File.ReadAllText(path);
                
                Current = JsonSerializer.Deserialize<AppConfig>(json);
                Current.Path = path;
                Current.LastLoadedConfig = path;
            }
            else
            {
                Current = new AppConfig(); 
            }
        }
        catch (Exception ex)
        {
            // Fehlerbehandlung hier hinzufügen
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            Current = new AppConfig();
        }
        ConfigLoaded?.Invoke(null, new EventArgs<AppConfig>(Current));
        Current.RaiseAllPropertiesChanged();
        return Current;
    }

    public static void BindToDataContext(FrameworkElement element)
    {
        element.DataContext = Current;
        ConfigLoaded += (sender, e) => element.DataContext = e.Value;
    }

    public void Save(string? path = null)
    {
        var cs = CaptureSource;
        path ??= Path ?? DefaultConfigPath;
        Save<AppConfig>(path);
    }

    public static event EventHandler<EventArgs<AppConfig>>? ConfigLoaded;

    private static void SaveLastConfigPath(string path)
    {
        var file = System.IO.Path.Combine(AppSettingsPath(), "LastConfigPath.cfg");
        File.WriteAllText(file, path);
    }

    private static string AppSettingsPath()
    {
        var folderPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-M");
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private static string? GetLastConfigPath()
    {
        var file = System.IO.Path.Combine(AppSettingsPath(), "LastConfigPath.cfg");
        return File.Exists(file) ? File.ReadAllText(file) : null;
    }

}
