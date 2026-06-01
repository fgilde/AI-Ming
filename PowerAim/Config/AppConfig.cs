using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using PowerAim.AILogic;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
using PowerAim.Types;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;


namespace PowerAim.Config;

public class AppConfig : BaseSettings
{
    public const string DefaultConfigPath = "bin\\configs\\Default.cfg";

    /// <summary>
    ///     Walks the loaded JSON and removes every <c>OcrConditionTree</c> /
    ///     <c>AntiOcrConditionTree</c> / <c>ConditionTree</c> property. Used as the fallback path
    ///     when a pre-polymorphism config can't be deserialized because its tree children are
    ///     empty <c>{}</c> objects with no <c>$type</c> discriminator. After stripping, the new
    ///     trees default-construct empty and <see cref="ActionTrigger.EnsureTreeMigrated"/> /
    ///     <see cref="AimDisengageRule.EnsureTreeMigrated"/> re-seed them from the legacy flat
    ///     <c>OcrConditions</c> / <c>RegionName</c> fields.
    /// </summary>
    private static string StripConditionTreesFromJson(string json)
    {
        try
        {
            var root = System.Text.Json.Nodes.JsonNode.Parse(json);
            if (root == null) return json;
            StripPropertyRecursive(root, "OcrConditionTree");
            StripPropertyRecursive(root, "AntiOcrConditionTree");
            StripPropertyRecursive(root, "ConditionTree");
            return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    private static void StripPropertyRecursive(System.Text.Json.Nodes.JsonNode node, string propertyName)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                if (obj.ContainsKey(propertyName)) obj.Remove(propertyName);
                foreach (var kv in obj.ToList())
                    if (kv.Value != null) StripPropertyRecursive(kv.Value, propertyName);
                break;
            case System.Text.Json.Nodes.JsonArray arr:
                foreach (var item in arr)
                    if (item != null) StripPropertyRecursive(item, propertyName);
                break;
        }
    }

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
        new ActionTrigger()
        {
            Name = "Rapid Fire",
            Enabled = false,
            NeedsDetection = false,
            TriggerKeys = [GamepadSlider.LeftTrigger, MouseButtons.Right],
            TriggerKeysOperator = KeyOperator.Or,
            Actions = [MouseButtons.Left, GamepadSlider.RightTrigger],
            Delay = 0,
            BreakTime = 0,
            ExecutionIntersectionCheck = TriggerCheck.None,
        },
        new ActionTrigger()
        {
            Name = "Auto Throw",
            Enabled = false,
            NeedsDetection = true,
            AntiTriggerKeys = [GamepadSlider.LeftTrigger, MouseButtons.Right],
            AntiTriggerKeysOperator = KeyOperator.And,
            Actions = [Keys.O, GamepadButton.RightShoulder],
            Delay = 0,
            BreakTime = 0,
            ExecutionIntersectionCheck = TriggerCheck.IntersectingCenter,
        },
        new ActionTrigger()
        {
            Name = "Ping",
            Enabled = true,
            NeedsDetection = true,
            TriggerKeys = [GamepadSlider.LeftTrigger],
            TriggerKeysOperator = KeyOperator.And,
            Actions = [MouseButtons.Middle, GamepadButton.Up],
            Delay = 0,
            BreakTime = 1,
            ExecutionIntersectionCheck = TriggerCheck.IntersectingCenter,
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
            DecisionInterval = 0.5,
            GameContext = "First-person shooter game. Control a player: move, jump, sprint, aim, shoot enemies. Enemies are other players/characters. Move towards objectives, engage enemies when visible, take cover when needed. Be aggressive - keep moving and shooting.",
            Actions = new ObservableCollection<AutoPlayAction>
            {
                new() { Name = "move_forward", Description = "Move forward - use when path is clear or approaching enemies/objectives", Keys = [Keys.W, GamepadButton.Up], ActionType = AutoPlayActionType.Continuous },
                new() { Name = "move_backward", Description = "Move backward - use to retreat from danger or reposition", Keys = [Keys.S, GamepadButton.Down], ActionType = AutoPlayActionType.Continuous },
                new() { Name = "move_left", Description = "Strafe left - use to dodge or circle enemies", Keys = [Keys.A, GamepadButton.Left], ActionType = AutoPlayActionType.Continuous },
                new() { Name = "move_right", Description = "Strafe right - use to dodge or circle enemies", Keys = [Keys.D, GamepadButton.Right], ActionType = AutoPlayActionType.Continuous },
                new() { Name = "sprint", Description = "Sprint faster - combine with movement when covering distance", Keys = [Keys.ShiftKey, GamepadButton.LeftThumb], ActionType = AutoPlayActionType.Modifier },
                new() { Name = "jump", Description = "Jump - use to clear obstacles or gain height advantage", Keys = [Keys.Space, GamepadButton.A], ActionType = AutoPlayActionType.Instant },
                new() { Name = "shoot", Description = "Fire weapon - use when enemy is visible on screen", Keys = [MouseButtons.Left, GamepadSlider.RightTrigger], ActionType = AutoPlayActionType.Continuous },
                new() { Name = "aim", Description = "Aim down sights - use for precision when enemy is at medium/long range", Keys = [MouseButtons.Right, GamepadSlider.LeftTrigger], ActionType = AutoPlayActionType.Modifier },
                new() { Name = "reload", Description = "Reload weapon - use when safe and ammo might be low", Keys = [Keys.R, GamepadButton.X], ActionType = AutoPlayActionType.Instant },
                new() { Name = "crouch", Description = "Crouch - use for cover or stealth", Keys = [Keys.ControlKey, GamepadButton.B], ActionType = AutoPlayActionType.Toggle },
                new() { Name = "interact", Description = "Interact / use / pick up - near doors, items, objectives, or to plant/defuse", Keys = [Keys.E, ((StoredInputBinding)GamepadButton.X).SetMinTime(0.4)], ActionType = AutoPlayActionType.Instant },
                new() { Name = "melee", Description = "Melee attack - when an enemy is point-blank and you're out of ammo or it's faster", Keys = [Keys.V, GamepadButton.RightThumb], ActionType = AutoPlayActionType.Instant },
                new() { Name = "grenade", Description = "Throw a grenade - against grouped enemies or to flush out cover", Keys = [Keys.G, GamepadButton.RightShoulder], ActionType = AutoPlayActionType.Instant },
                new() { Name = "switch_weapon", Description = "Switch weapon - when the current weapon is empty or wrong for the range", Keys = [Keys.Q, GamepadButton.Y], ActionType = AutoPlayActionType.Instant },
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

                try
                {
                    Current = JsonSerializer.Deserialize<AppConfig>(json);
                }
                catch (System.Text.Json.JsonException ex) when (ex.Message.Contains("Deserialization of interface or abstract types"))
                {
                    // Pre-polymorphism configs persisted OcrConditionNode children as empty {}
                    // objects (no type discriminator) — the abstract base can't deserialize them.
                    // Strip every *ConditionTree object from the JSON and retry; the legacy flat
                    // OcrConditions list survives untouched and the migration step below re-seeds
                    // the tree from it. The user loses nothing meaningful because those empty
                    // children carried no data in the first place.
                    Console.WriteLine($"[AppConfig] Stripping legacy abstract-typed condition trees from config: {ex.Message}");
                    json = StripConditionTreesFromJson(json);
                    Current = JsonSerializer.Deserialize<AppConfig>(json);
                }
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
        // Migrations that translate older config schemas into the new shapes. Each MigrateXyz
        // method is idempotent (gated by its own schema-version field) so re-loading the same
        // config doesn't double-seed anything.
        Current.AntiRecoilSettings?.MigrateLegacyIfNeeded();

        // Triggers: migrate the legacy flat OcrConditions list into the new OcrConditionTree
        // (AND group by default). Idempotent — re-running on an already-migrated config is a no-op.
        if (Current.Triggers != null)
            foreach (var t in Current.Triggers) t?.EnsureTreeMigrated();

        // AimDisengageRule: each existing rule had a single (RegionName, Comparison, Value) row;
        // seed each rule's new ConditionTree with that single leaf if it hasn't been migrated yet.
        if (Current.AimDisengageRules != null)
            foreach (var r in Current.AimDisengageRules) r?.EnsureTreeMigrated();

        // Migrate the deprecated ToggleState.UseControllerForAim flag into the unified
        // DropdownState.MouseMovementMethod enum. Old configs persist a separate bool that the
        // UI no longer surfaces — fold it into the new "Gamepad" movement method so users keep
        // their controller-aim setting across the upgrade.
        if (Current.ToggleState?.UseControllerForAim == true
            && Current.DropdownState != null
            && Current.DropdownState.MouseMovementMethod != MouseMovementMethod.Gamepad)
        {
            Current.DropdownState.MouseMovementMethod = MouseMovementMethod.Gamepad;
            // Clear the legacy flag so it doesn't keep re-forcing Gamepad on every load if the
            // user manually changes the method later.
            Current.ToggleState.UseControllerForAim = false;
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
