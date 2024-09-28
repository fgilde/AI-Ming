using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Aimmy2.AILogic;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Contracts;
using Aimmy2.InputLogic.HidHide;
using Aimmy2.MouseMovementLibraries.GHubSupport.dist;
using Aimmy2.Types;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;


namespace Aimmy2.Config;

public class AppConfig : BaseSettings
{
    public const string DefaultConfigPath = "bin\\configs\\Default.cfg";

    [JsonIgnore]
    public string? Path
    {
        get => _path;
        set => SetField(ref _path, value);
    }

    public static AppConfig Current { get; private set; }

    public string LastLoadedModel { get; set; } = "N/A";
    
    public string LastLoadedConfig = "N/A";
    private CaptureSource _captureSource = AILogic.CaptureSource.MainScreen();
    private string? _path;
    private string? _language;
    private ObservableCollection<ActionTrigger> _triggers = new ()
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
    public string SuggestedModelName => SliderSettings.SuggestedModel;
    
    public string ThemeName { get; set; } = ThemePalette.PurplePalette.Name;
    public string ActiveThemeName { get; set; } = ThemePalette.GreenPalette.Name;
    public BindingSettings BindingSettings { get; set; } = new BindingSettings();
    public SliderSettings SliderSettings { get; set; } = new SliderSettings();
    public ToggleState ToggleState { get; set; } = new ToggleState();
    public MinimizeState MinimizeState { get; set; } = new MinimizeState();
    public DropdownState DropdownState { get; set; } = new DropdownState();
    public ColorState ColorState { get; set; } = new ColorState();
    public AntiRecoilSettings AntiRecoilSettings { get; set; } = new AntiRecoilSettings();
    public FileLocationState FileLocationState { get; set; } = new FileLocationState();

    public ObservableCollection<ActionTrigger> Triggers
    {
        get => _triggers;
        set => SetField(ref _triggers, value);
    }

    public string? Language
    {
        get => _language;
        set => SetField(ref _language, value);
    }

    public CaptureSource CaptureSource
    {
        get => _captureSource;
        set => SetField(ref _captureSource, value);
    }

    public static AppConfig Load(string? path = null)
    {
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
