using Core;
using PowerAim.InputLogic;
using Microsoft.Xaml.Behaviors.Core;
using Nextended.Core;
using Nextended.Core.Extensions;
using Nextended.Core.Helper;
using Nextended.UI.Helper;
using PowerAim.Other;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Localizations;
using PowerAim.Models;
using PowerAim.Types;
using PowerAim.UILibrary;
using PowerAim.Visuality;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PowerAim;

public partial class MainWindow
{
    #region Main Variables

    public static MainWindow? Instance => Application.Current.Dispatcher.Invoke(() => Application.Current.MainWindow as MainWindow);


    private bool _uiCreated;
    private FileManager _fileManager;
    // AntiRecoilManager (UI-thread DispatcherTimer that called MouseManager.DoAntiRecoil)
    // has been removed — the legacy anti-recoil now lives in AntiRecoilAction so it
    // honours GlobalActive / the BETA toggle / pausing like every other action.


    private bool _currentlySwitching;
    private FrameworkElement? _currentScrollViewer;

    // Keyed by filename. Each entry carries the resolved GitHub source (fork or upstream)
    // so the download click can hit the right repo directly. See FileManager.RetrieveAndMergeFromRepos.
    private readonly Dictionary<string, GitHubFile> _availableModels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GitHubFile> _availableConfigs = new(StringComparer.OrdinalIgnoreCase);

    #endregion Main Variables

    #region Loading Window

    public AppConfig? Config { get; internal set; }

    public InputBindingManager? BindingManager { get; private set; }


    private readonly GlobalSearchController _search;

    public MainWindow()
    {
        InitializeComponent();

        _search = new GlobalSearchController(this, GlobalSearchButton, GlobalSearchPopup, GlobalSearchBox,
            GlobalSearchResults, GlobalSearchHint, () => CurrentMenu, (menu, animate) => NavigateTo(menu, animate));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(OpenSearchCommand, (_, _) => _search.Open()));

        var writer = new TextBoxStreamWriter(OutputTextBox);
        Console.SetOut(writer);
        AppConfig.ConfigLoaded += (s, e) => CreateUI();
        Console.WriteLine(Locale.InitUIMessage);
        Config = AppConfig.Load();

        DataContext = this;

        LoadLastModel();

        if (!string.IsNullOrEmpty(ApplicationConstants.ShowOnly))
        {
            Sidebar.Visibility = Visibility.Collapsed;
            _ = SwitchScrollPanels(FindName(ApplicationConstants.ShowOnly) as ScrollViewer);
            CurrentMenu = ApplicationConstants.ShowOnly;
        }

        //Console.WriteLine(JsonConvert.SerializeObject(Dictionary.toggleState));
        Console.WriteLine(Locale.UICompleteMessage);
        Console.WriteLine("Compiled with Cuda: " + ApplicationConstants.IsCudaBuild);

        // Floating config-label tab — a separate window that sits just above the title bar, so it
        // shows the config name OUTSIDE the shell without altering the main window's content.
        Loaded += (_, _) => ShowConfigLabelOverlay();
        LocationChanged += (_, _) => _configLabelOverlay?.Reposition();
        SizeChanged += (_, _) => _configLabelOverlay?.Reposition();
        StateChanged += (_, _) => _configLabelOverlay?.Reposition();
        Activated += (_, _) => _configLabelOverlay?.Reposition();
        Closed += (_, _) => { _configLabelOverlay?.Close(); _configLabelOverlay = null; };
    }

    private global::PowerAim.Visuality.ConfigLabelOverlay? _configLabelOverlay;

    private void ShowConfigLabelOverlay()
    {
        if (_configLabelOverlay != null) return;
        try
        {
            _configLabelOverlay = new global::PowerAim.Visuality.ConfigLabelOverlay(this);
            _configLabelOverlay.Show();
            _configLabelOverlay.Reposition();
        }
        catch
        {
            // The tab is cosmetic — never let it break startup.
            _configLabelOverlay = null;
        }
    }

    private void CreateUI()
    {
        string? menu = null;
        var isRecreating = IsInitialized && _uiCreated;
        if (isRecreating && CurrentMenu != nameof(AimMenu))
        {
            menu = CurrentMenu;
            _ = NavigateTo(nameof(AimMenu), false);
        }

        if (!isRecreating && !string.IsNullOrEmpty(AppConfig.Current?.Language))
        {
            Check.TryCatch<Exception>(() => CultureInfo.CurrentUICulture = new CultureInfo(AppConfig.Current.Language));
        }

        _uiCreated = false;
        Check.TryCatch<Exception>(GamepadManager.Init);

        PowerAim.Theme.ThemeManager.Apply();

        _currentScrollViewer = FindName(nameof(AimMenu)) as FrameworkElement;
        if (_currentScrollViewer is null) throw new NullReferenceException("CurrentScrollViewer is null");

        _fileManager?.Dispose();
        _fileManager = new FileManager(ModelListBox, SelectedModelNotifier, ConfigsListBox, SelectedConfigNotifier);

        // Needed to import annotations into MakeSense
        if (!File.Exists(Path.Combine(ApplicationConstants.LabelsBasePath, "labels.txt"))) File.WriteAllText(Path.Combine(ApplicationConstants.LabelsBasePath, "labels.txt"), "Enemy");

        if (BindingManager is not null)
        {
            BindingManager.OnBindingPressed -= BindingOnKeyPressed;
            BindingManager.OnBindingReleased -= BindingOnKeyReleased;
            BindingManager.Dispose();
        }

        BindingManager = new InputBindingManager();
        OnPropertyChanged(nameof(BindingManager));

        // Seed the default keybinds in one pass (was nine near-identical SetupDefault calls).
        // nameof(b.X) yields the same binding id ("X") the explicit calls used, so this is 1:1.
        var b = AppConfig.Current.BindingSettings;
        // NOTE: MagnifierKeybind is intentionally absent — the magnifier's start key is now the unified
        // per-tool keybind (USER_TOOL_<id>) registered by the Tools list row's AKeyChanger.
        (string Id, StoredInputBinding Bind)[] keybindDefaults =
        [
            (nameof(b.MagnifierZoomInKeybind),   b.MagnifierZoomInKeybind),
            (nameof(b.MagnifierZoomOutKeybind),  b.MagnifierZoomOutKeybind),
            (nameof(b.DynamicFOVKeybind),        b.DynamicFOVKeybind),
            (nameof(b.ModelSwitchKeybind),       b.ModelSwitchKeybind),
            (nameof(b.AntiRecoilKeybind),        b.AntiRecoilKeybind),
            (nameof(b.DisableAntiRecoilKeybind), b.DisableAntiRecoilKeybind),
            (nameof(b.Gun1Key),                  b.Gun1Key),
            (nameof(b.Gun2Key),                  b.Gun2Key),
        ];
        foreach (var (id, bind) in keybindDefaults)
            BindingManager.SetupDefault(id, bind);

        LoadAimMenu();
        LoadTools();
        LoadSettingsMenu();
        LoadGamepadSettingsMenu();
        LoadAutoPlayMenu();
        LoadCreditsMenu();
        LoadStoreMenuAsync();
        LoadGlobalUI();

        BindingManager.OnBindingPressed += BindingOnKeyPressed;
        BindingManager.OnBindingReleased += BindingOnKeyReleased;

        ModelListBox.EnsureRenderedAndInitialized();
        ConfigsListBox.EnsureRenderedAndInitialized();
        Width += 1;
        Task.Delay(10).ContinueWith(task => Dispatcher.Invoke(() => Width -= 1));
        // ModelMenuTabControl.SelectedIndex = 0;
        _uiCreated = true;

        // Instrument every page with drag-reorder + hide-X chrome and apply persisted layout.
        // Deferred so XAML+CreateUI children have finished landing in the visual tree.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            AttachLayoutManagers();
            BindHiddenBoxesPillForCurrentPage();
            // The active GPU is per-config (AISettings.InferenceGpuDeviceId). The picker popup reads
            // it live, but the title-bar pill label is only set on startup / manual change — refresh
            // it on every config load so it doesn't show the previous config's card.
            _gpuPicker?.RefreshLabel();
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        if (isRecreating && menu is not null)
        {
            _ = NavigateTo(menu, false);
        }
    }

    internal AIManager AIManager => AIManager.Instance;


    public bool IsModelLoaded => FileManager.AIManager?.IsModelLoaded ?? false;
    public bool IsNotModelLoaded => !IsModelLoaded;

    /// <summary>
    ///     Bottom status-strip text: the loaded model + its ACTIVE execution provider, or a
    ///     loading / failed / no-model state. Always reflects reality (the provider is the one ORT
    ///     actually negotiated, so a DirectML build shows "DirectML", a CPU fallback shows "Cpu"), so the
    ///     user can see at a glance what's really running.
    /// </summary>
    public string ModelStatusText
    {
        get
        {
            var model = Config?.LastLoadedModel;
            if (string.IsNullOrEmpty(model)) model = "—";
            if (FileManager.CurrentlyLoadingModel) return $"{model}  ·  …";
            var pl = FileManager.AIManager?.PredictionLogic;
            if (pl is { IsLoaded: true }) return $"{model}  ·  {pl.ExecutionProvider}";
            if (!string.IsNullOrEmpty(pl?.LoadError)) return $"{model}  ·  {Locale.ModelLoadFailed}";
            return $"{model}  ·  —";
        }
    }

    /// <summary>
    ///     True while a model load is in flight (and at startup before the first load attempt
    ///     resolves). Drives <see cref="UILibrary.NoModelCard"/>'s loading spinner so the "no model"
    ///     message doesn't flash during the brief startup load. Set to false once a load resolves
    ///     (success or failure) or when there is nothing to load.
    /// </summary>
    public bool ModelLoadPending
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ModelLoadPending));
            }
        }
    } = true;

    private void LoadGlobalUI()
    {
        TopCenterGrid.RemoveAll();
        TopCenterGrid.Add(new CaptureSourceSelect(), select =>
        {
            select.Selected += (sender, source) =>
            {
                LoadModel();
            };
        });
        TopCenterGrid.AddToggleWithKeyBind(Locale.GlobalActive, nameof(Locale.GlobalActive), BindingManager, toggle =>
        {
            toggle.BindTo(() => AppConfig.Current.ToggleState.GlobalActive);
            toggle.Changed += (s, e) => SetActive(e.Value);
        }, border => border.Background = Brushes.Transparent, changer =>
        {
            changer.BindButton.Margin = new Thickness(14, 7, 2, 7);
            changer.CanEditMinTime = true;
            // The Global Active hotkey must always work, even when the "keybinds only while Global
            // Active" gate is on — otherwise there'd be no way to switch it back on by hotkey.
            changer.IgnoreGlobalActiveGate = true;
        });
    }

    public void SetActive(bool active)
    {
        AppConfig.Current.ToggleState.GlobalActive = active;
        PowerAim.Theme.ThemeManager.Apply();
    }

    public Visibility GetVisibilityFor(string feature)
    {
        return ApplicationConstants.DisabledFeatures.Contains(feature) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void LoadStoreMenuAsync()
    {
        await LoadStoreMenu();
    }

    private GpuPickerController? _gpuPicker;

    /// <summary>Suppresses the spurious onSelect a combo raises while populating (auto-selects its first
    /// item) so building the model-settings dropdowns can't trigger a model reload at startup.</summary>
    private bool _buildingModelDropdowns;


    #endregion Loading Window




    #region Fancy UI Calculations

    private double currentGradientAngle;

    #endregion Fancy UI Calculations


    internal override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(IsModelLoaded))
        {
            OnPropertyChanged(nameof(IsNotModelLoaded));
            OnPropertyChanged(nameof(ModelStatusText)); // refresh the status-strip model/provider text
            ModelLoadPending = false; // the load attempt resolved (loaded or failed)
        }
        base.OnPropertyChanged(propertyName);
    }

    public void CallPropertyChanged(string name)
    {
        OnPropertyChanged(name);
    }

    private MagnifierDialog? _magnifier;

}