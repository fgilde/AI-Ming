using Core;
using InputLogic;
using Microsoft.Xaml.Behaviors.Core;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Nextended.Core;
using Nextended.Core.Extensions;
using Nextended.Core.Helper;
using Nextended.UI.Helper;
using Other;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.InputLogic.HidHide;
using PowerAim.Localizations;
using PowerAim.Models;
using PowerAim.MouseMovementLibraries.GHubSupport;
using PowerAim.Other;
using PowerAim.Types;
using PowerAim.UILibrary;
using PowerAim.Visuality;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using UILibrary;
using Visuality;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;

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


    public MainWindow()
    {
        InitializeComponent();

        // Ctrl+F → open global search. The KeyBinding in XAML references this command; we have
        // to provide the binding here so the input gesture actually dispatches.
        CommandBindings.Add(new System.Windows.Input.CommandBinding(OpenSearchCommand, OpenSearch_Executed));

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

    private global::Visuality.ConfigLabelOverlay? _configLabelOverlay;

    private void ShowConfigLabelOverlay()
    {
        if (_configLabelOverlay != null) return;
        try
        {
            _configLabelOverlay = new global::Visuality.ConfigLabelOverlay(this);
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
        (string Id, StoredInputBinding Bind)[] keybindDefaults =
        [
            (nameof(b.MagnifierKeybind),         b.MagnifierKeybind),
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
            RefreshGpuPickerLabel();
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        if (isRecreating && menu is not null)
        {
            _ = NavigateTo(menu, false);
        }
    }

    internal AIManager AIManager => AIManager.Instance;

    internal void FillMenus()
    {
        UpdateModelText();
        ModelContextMenu.Items.Clear();
        ModelContextMenu.Items.AddRange(ModelListBox.ToMenuItems(item =>
        {
            LoadModel(item.Header.ToString());
        },
        (i, item) => i <= 9 ? KeyGestureConvertHelper.CreateFromString($"Ctrl + Shift + {i}") : null));
        ModelContextMenu.Items.Add(new Separator());
        var downloadableMenu = new System.Windows.Controls.MenuItem()
        {
            Header = Locale.DownloadableModelsHeader
        };
        ModelContextMenu.Items.Add(downloadableMenu);
        downloadableMenu.Items.AddRange(_availableModels.Keys.Select(s => new System.Windows.Controls.MenuItem()
        {
            Header = s,
            Command = new ActionCommand(() =>
            {
                downloadableMenu.IsEnabled = false;
                ADownloadGateway.DownloadAsync(s, "models").ContinueWith(task =>
                {
                    if (task.Result)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LoadModel(s);
                            FillMenus();
                        });
                    }
                });
            })
        }));

        MenuItemOpenCfg.Items.Clear();
        MenuItemOpenCfg.Items.AddRange(ConfigsListBox.ToMenuItems(
            item => LoadConfig(Path.Combine(Path.GetDirectoryName(AppConfig.DefaultConfigPath), item.Header?.ToString())),
            (i, item) => i <= 9 ? KeyGestureConvertHelper.CreateFromString($"Ctrl + {i}") : null)
        );
    }

    private void UpdateModelText()
    {
        if (Config is not null)
            ModelContextMenu.Header = $"{Config.LastLoadedModel} ({AIManager?.PredictionLogic?.ExecutionProvider})";
    }

    private void LoadModel(string? modelName = null)
    {
        FileManager.AIManager?.Dispose();
        FileManager.AIManager = null;
        FileManager.CurrentlyLoadingModel = false;
        LoadLastModel(modelName);
    }

    private void LoadLastModel(string? modelName = null)
    {
        modelName ??= Config.LastLoadedModel;
        var lastLoaded = Path.Combine(ApplicationConstants.ModelsBasePath, modelName);
        var modelPath = File.Exists(lastLoaded) ? lastLoaded : Path.Combine(ApplicationConstants.ModelsBasePath, ApplicationConstants.DefaultModel);
        if (File.Exists(modelPath) && !FileManager.CurrentlyLoadingModel &&
            FileManager.AIManager?.IsModelLoaded != true)
        {
            // A model is about to load — keep the empty-state card in its "loading" look so the
            // "no model" message doesn't flash during the brief load.
            ModelLoadPending = true;
            _ = _fileManager.LoadModel(Path.GetFileName(modelPath), modelPath);
        }
        else if (!IsModelLoaded)
        {
            // Nothing to load (no model file present) — reveal the empty-state message now.
            ModelLoadPending = false;
        }
        UpdateModelText();
    }

    /// <summary>
    ///     Copies the bundled default model (<c>Resources\default.onnx</c>, shipped next to the exe)
    ///     into <c>bin\models\</c> if it isn't there yet, then loads it. Backs the "Load default
    ///     model" button on <see cref="UILibrary.NoModelCard"/>. Throws a localized
    ///     <see cref="FileNotFoundException"/> if the bundled file is missing so the card can show it.
    /// </summary>
    public async Task LoadDefaultModelAsync()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var modelsDir = Path.Combine(baseDir, "bin", "models");
        Directory.CreateDirectory(modelsDir);
        var dest = Path.Combine(modelsDir, ApplicationConstants.DefaultModel);

        if (!File.Exists(dest))
        {
            var src = Path.Combine(baseDir, "Resources", ApplicationConstants.DefaultModel);
            if (!File.Exists(src))
                throw new FileNotFoundException(Locale.LoadDefaultModelMissing);
            File.Copy(src, dest);
        }

        // Switch the empty-state card to its loading look while the model spins up.
        ModelLoadPending = true;
        await _fileManager.LoadModel(ApplicationConstants.DefaultModel,
            Path.Combine(ApplicationConstants.ModelsBasePath, ApplicationConstants.DefaultModel));
    }


    public bool IsModelLoaded => FileManager.AIManager?.IsModelLoaded ?? false;
    public bool IsNotModelLoaded => !IsModelLoaded;

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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _ = CheckUpdate(false);
        KnownIssuesDialog.ShowIf(this);
        //SetupWizard.ShowIfFirstRun(this);
        AboutSpecs.Content =
            $"{GetProcessorName()} • {GetVideoControllerName()} • {GetFormattedMemorySize()}GB RAM";

        if (GamepadTester is not null)
            GamepadTester.BackRequested += (_, _) => _ = NavigateTo(nameof(GamepadSettings));

        WireHelpPanel();
        WireAboutPage();

        UpdateAdminButton();
        InitGpuPicker();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // The window-wide "click anywhere to drag" behaviour eats events bound for interactive
        // controls that live inside the window (Buttons in Popups, Sliders, TextBoxes, the drag
        // Thumbs on the layout boxes, …) — DragMove blocks the message loop synchronously, so
        // the inner control never sees its MouseUp and Click never fires.
        // Walk up the visual tree from the click source and skip DragMove if we find anything
        // interactive between us and the Window.
        if (e.OriginalSource is DependencyObject d && IsInsideInteractiveControl(d))
            return;
        try { DragMove(); }
        catch { /* DragMove can throw if mouse-state shifted under us; nothing actionable */ }
    }

    private static bool IsInsideInteractiveControl(DependencyObject node)
    {
        for (int i = 0; i < 32 && node is not null; i++)
        {
            switch (node)
            {
                case PowerAim.UILibrary.AKeyChanger:                       // keybind editor (its min-time popup uses MouseUp)
                case System.Windows.Controls.Primitives.ButtonBase:        // Button, ToggleButton, RepeatButton…
                case System.Windows.Controls.Primitives.Thumb:             // layout-drag handles
                case System.Windows.Controls.Primitives.Popup:             // search popup, hidden-sections popup
                case System.Windows.Controls.Primitives.TextBoxBase:       // TextBox, RichTextBox
                case System.Windows.Controls.Slider:
                case System.Windows.Controls.ComboBox:
                case System.Windows.Controls.ComboBoxItem:
                case System.Windows.Controls.ListBox:
                case System.Windows.Controls.ListBoxItem:
                case System.Windows.Controls.MenuItem:
                case System.Windows.Controls.PasswordBox:
                case System.Windows.Controls.Primitives.ScrollBar:
                    return true;
            }
            // Climb both visual and logical parents — popups live in the logical tree of the
            // placement target but their content tree is detached from the window visually.
            node = (node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D)
                ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                : System.Windows.LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    ///     Relaunches PowerAim with admin rights via ShellExecute "runas". Visible in the
    ///     topbar only when the current process isn't already elevated (see UpdateAdminButton).
    /// </summary>
    private void RestartAsAdminButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe))
            {
                new global::Visuality.NoticeBar(Locale.ResolveExePathFailed, 4000).Show();
                return;
            }
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            };
            System.Diagnostics.Process.Start(psi);
            // Quit the unelevated instance so we don't have two running side by side.
            Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User dismissed the UAC prompt — leave the unelevated instance alone.
            new global::Visuality.NoticeBar(Locale.UacDeclined, 3000).Show();
        }
        catch (Exception ex)
        {
            new global::Visuality.NoticeBar(Locale.RestartAsAdminFailedFormat.FormatWith(ex.Message), 5000).Show();
        }
    }

    /// <summary>
    ///     Hides the "Restart as admin" button when we're already elevated.
    /// </summary>
    private void UpdateAdminButton()
    {
        if (RestartAsAdminButton is null) return;
        RestartAsAdminButton.Visibility = PowerAim.Class.Native.DeviceHide.IsElevated()
            ? Visibility.Collapsed
            : Visibility.Visible;
    }


    #endregion Loading Window




    #region Config Loader

    internal void LoadConfig(string path = AppConfig.DefaultConfigPath)
    {
        if (path == AppConfig.Current.Path)
            return;
        AppConfig.Current.Save();
        Console.WriteLine(Locale.LoadingConfigMessage + path);
        Config = AppConfig.Load(path);
        OnPropertyChanged(nameof(Config));

        if (!string.IsNullOrEmpty(AppConfig.Current.SuggestedModelName) && AppConfig.Current.SuggestedModelName != "N/A")
            MessageDialog.Show(
                $"{Locale.ModelSuggestionText}:\n" + AppConfig.Current.SuggestedModelName,
                Locale.SuggestedModel,
                MessageDialog.DialogButtons.OK,
                MessageDialog.DialogIcon.Info,
                owner: this);
        LoadModel();
    }

    #endregion Config Loader

    #region Anti Recoil Config Loader

    private void LoadAntiRecoilConfig(string path = Constants.AntiRecoilConfigBasePath + "\\Default.cfg",
        bool loading_outside_startup = false)
    {
        if (!string.IsNullOrEmpty(path))
        {
            AppConfig.Current.AntiRecoilSettings.Load<AntiRecoilSettings>(path);
            if (loading_outside_startup)
                CreateUI();
        }
    }

    #endregion Anti Recoil Config Loader

    #region Open Folder

    private void OpenFolderB_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton)
            Process.Start("explorer.exe", Path.Combine(Directory.GetCurrentDirectory(), "bin", clickedButton.Tag.ToString()));
    }

    #endregion Open Folder

    #region System Information

    private static string? GetProcessorName()
    {
        return GetSpecs.GetSpecification("Win32_Processor", "Name");
    }

    private static string? GetVideoControllerName()
    {
        return GetSpecs.GetSpecification("Win32_VideoController", "Name");
    }

    private static string? GetFormattedMemorySize()
    {
        var totalMemorySize = long.Parse(GetSpecs.GetSpecification("CIM_OperatingSystem", "TotalVisibleMemorySize")!);
        return Math.Round(totalMemorySize / (1024.0 * 1024.0), 0).ToString();
    }

    #endregion System Information

    #region Fancy UI Calculations

    private double currentGradientAngle;

    private double CalculateAngleDifference(double targetAngle, double fullCircle, double halfCircle, double clamp)
    {
        var angleDifference = (targetAngle - currentGradientAngle + fullCircle) % fullCircle;
        if (angleDifference > halfCircle) angleDifference -= fullCircle;
        return Math.Max(Math.Min(angleDifference, clamp), -clamp);
    }

    #endregion Fancy UI Calculations

    #region Window Handling

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        await CheckUpdate(true);
    }

    private async Task CheckUpdate(bool showNotice)
    {
        try
        {
            CheckForUpdates.IsEnabled = false;
            UpdateCheckStatusLabel.Content = Locale.CheckingForUpdates;
            await Task.Delay(500);
            var updateManager = new UpdateManager();
            var hasUpdate = await updateManager.CheckForUpdate(ApplicationConstants.ApplicationVersion, ApplicationConstants.RepoOwner, ApplicationConstants.RepoName, ApplicationConstants.IsCudaBuild);
            UpdateCheckStatusLabel.Content = hasUpdate ? Locale.UpdateAvailable : Locale.NoUpdateAvailable;
            if (hasUpdate)
            {
                CheckForUpdates.Content = Locale.InstallUpdate;
            }
            if (!hasUpdate)
            {
                if (showNotice)
                    new NoticeBar(Locale.YouAreAlreadyOnTheLatestVersion, 5000).Show();
            }
            else
            {
                new UpdateDialog(updateManager) { Owner = Application.Current.MainWindow }.ShowDialog();
            }

            updateManager.Dispose();
        }
        finally
        {
            CheckForUpdates.IsEnabled = true;
        }
    }

    #endregion Window Handling


    internal override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(IsModelLoaded))
        {
            OnPropertyChanged(nameof(IsNotModelLoaded));
            ModelLoadPending = false; // the load attempt resolved (loaded or failed)
        }
        base.OnPropertyChanged(propertyName);
    }

    public void CallPropertyChanged(string name)
    {
        OnPropertyChanged(name);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        OutputTextBox.Clear();
    }

    private void MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        new ConfigSaver { Owner = this }.ShowDialog();
    }

    private void MenuItemSaveAs_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog() { Filter = Locale.FilterConfig };
        if (dlg.ShowDialog() == true)
        {
            AppConfig.Current.Save(dlg.FileName);
        }
    }

    private void OpenConfig_CLick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog() { Filter = Locale.FilterConfig };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            LoadConfig(dlg.FileName);
        }
    }

    private void ShowKnownIssues_Click(object sender, RoutedEventArgs e)
    {
        KnownIssuesDialog.ShowIf(this, true);
    }

    private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void AKeyChanger_ModelOnGlobalKeyPressed(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
    {
        var args = e.Value;
        var modelToLoad = args.Sender.Tag?.ToString();
        if (modelToLoad is not null)
        {
            try
            {
                ModelListBox.SelectedIndex = ModelListBox.Items.IndexOf(modelToLoad);
            }
            catch
            {
                Check.TryCatch<Exception>(() => LoadModel(modelToLoad));
            }
        }
    }

    private void AKeyChanger_ConfigOnGlobalKeyPressed(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
    {
        var args = e.Value;
        var configToLoad = args.Sender.Tag?.ToString();
        if (configToLoad is not null)
        {
            try
            {
                ConfigsListBox.SelectedIndex = ConfigsListBox.Items.IndexOf(configToLoad);
            }
            catch
            {
                Check.TryCatch<Exception>(() => LoadConfig(Path.Combine(Path.GetDirectoryName(AppConfig.DefaultConfigPath), configToLoad)));
            }
        }
    }

    private MagnifierDialog? _magnifier;
    private void ToggleMagnifier(bool? show = null)
    {
        if (_magnifier is null && show is null or true)
        {
            _magnifier = new MagnifierDialog();
            _magnifier.Show();
        }
        else if (_magnifier is not null && show is null or false)
        {
            _magnifier.Close();
            _magnifier = null;
        }
    }

    private void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        var model = (sender as FrameworkElement)?.Tag?.ToString();
        if (!string.IsNullOrEmpty(model))
            DeleteModel(model);
    }

    private void DeleteModel(string model, bool confirmed = false)
    {
        var path = Path.Combine(Constants.ModelsBasePath, model);
        if (File.Exists(path))
        {
            if (!confirmed)
            {
                var res = MessageDialog.Show(
                    Locale.ConfirmModelDelete.FormatWith(model), Locale.DeleteModel,
                    MessageDialog.DialogButtons.YesNo,
                    MessageDialog.DialogIcon.Question,
                    owner: this,
                    defaultResult: MessageDialog.DialogResult.No);
                if (res == MessageDialog.DialogResult.No)
                    return;
            }
            File.Delete(path);
        }
    }

    private void DeleteConfig_Click(object sender, RoutedEventArgs e)
    {
        var cfg = (sender as FrameworkElement)?.Tag?.ToString();
        if (!string.IsNullOrEmpty(cfg))
            DeleteConfig(cfg);
    }

    private void DeleteConfig(string cfg, bool confirmed = false)
    {
        var path = Path.Combine(Path.GetDirectoryName(AppConfig.DefaultConfigPath), cfg);
        if (File.Exists(path))
        {
            if (!confirmed)
            {
                var res = MessageDialog.Show(
                    Locale.ConfirmConfigDelete.FormatWith(cfg), Locale.DeleteConfig,
                    MessageDialog.DialogButtons.YesNo,
                    MessageDialog.DialogIcon.Question,
                    owner: this,
                    defaultResult: MessageDialog.DialogResult.No);
                if (res == MessageDialog.DialogResult.No)
                    return;
            }
            File.Delete(path);
        }
    }

    private void ShowWizzard_CLick(object sender, RoutedEventArgs e)
    {
        SetupWizard.ShowIfFirstRun(this, true);
    }
}