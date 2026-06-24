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
        if (!File.Exists("bin\\labels\\labels.txt")) File.WriteAllText("bin\\labels\\labels.txt", "Enemy");

        if (BindingManager is not null)
        {
            BindingManager.OnBindingPressed -= BindingOnKeyPressed;
            BindingManager.OnBindingReleased -= BindingOnKeyReleased;
            BindingManager.Dispose();
        }

        BindingManager = new InputBindingManager();
        OnPropertyChanged(nameof(BindingManager));
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.MagnifierKeybind), AppConfig.Current.BindingSettings.MagnifierKeybind);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.MagnifierZoomInKeybind), AppConfig.Current.BindingSettings.MagnifierZoomInKeybind);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind), AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind);

        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind), AppConfig.Current.BindingSettings.DynamicFOVKeybind);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind), AppConfig.Current.BindingSettings.ModelSwitchKeybind);

        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind), AppConfig.Current.BindingSettings.AntiRecoilKeybind);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind), AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.Gun1Key), AppConfig.Current.BindingSettings.Gun1Key);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.Gun2Key), AppConfig.Current.BindingSettings.Gun2Key);

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
        var lastLoaded = Path.Combine("bin/models", modelName);
        var modelPath = File.Exists(lastLoaded) ? lastLoaded : Path.Combine("bin/models", ApplicationConstants.DefaultModel);
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
            Path.Combine("bin/models", ApplicationConstants.DefaultModel));
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
        //  this.HideForCapture();
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

    // ============================================================== INFERENCE GPU PICKER ====
    //
    // Lets the user push ONNX inference onto a secondary GPU so detection workloads don't
    // compete with the game for cycles on the primary card. Only renders when DXGI reports
    // more than one usable adapter — otherwise the chip stays hidden.

    private void InitGpuPicker()
    {
        if (GpuPickerButton is null) return;
        try
        {
            var adapters = AILogic.GpuAdapterEnumerator.List();
            // Show the chip whenever we have at least one real adapter — even on a single-GPU
            // system the user benefits from seeing which card is in use, AND if enumeration only
            // returned one entry on a known multi-GPU rig the popup surfaces the diagnostic log.
            if (adapters.Count == 0)
            {
                GpuPickerButton.Visibility = Visibility.Collapsed;
                Debug.WriteLine("[GpuPicker] No adapters returned. Log: " + AILogic.GpuAdapterEnumerator.LastLog);
                return;
            }
            // Show the pill whenever at least one real adapter is detected. Single-GPU users still
            // benefit from confirming which card is being used; multi-GPU users can swap.
            GpuPickerButton.Visibility = Visibility.Visible;
            RefreshGpuPickerLabel(adapters);
        }
        catch (Exception ex)
        {
            GpuPickerButton.Visibility = Visibility.Collapsed;
            Debug.WriteLine($"[GpuPicker] InitGpuPicker threw: {ex.Message}");
        }
    }

    private void RefreshGpuPickerLabel(IReadOnlyList<AILogic.GpuAdapterEnumerator.GpuAdapter>? adapters = null)
    {
        adapters ??= AILogic.GpuAdapterEnumerator.List();
        int selected = AppConfig.Current?.AISettings?.InferenceGpuDeviceId ?? 0;
        var match = adapters.FirstOrDefault(a => a.DeviceId == selected);
        // Fall back to the description of whatever's currently first if the saved index points at
        // a GPU that no longer exists (driver removed, eGPU unplugged).
        if (match.Description is null && adapters.Count > 0) match = adapters[0];
        GpuPickerLabel.Text = string.IsNullOrEmpty(match.Description)
            ? "GPU"
            : ShortenGpuName(match.Description);
    }

    /// <summary>
    ///     Strip the vendor prefix from typical adapter names so the chip in the titlebar stays
    ///     compact: "NVIDIA GeForce RTX 4090" → "RTX 4090". Falls back to the original when no
    ///     known prefix matches.
    /// </summary>
    private static string ShortenGpuName(string full)
    {
        string[] prefixes = ["NVIDIA GeForce ", "NVIDIA ", "AMD Radeon(TM) ", "AMD Radeon ", "AMD ", "Intel(R) ", "Intel "];
        foreach (var p in prefixes)
            if (full.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return full.Substring(p.Length).Trim();
        return full;
    }

    private void GpuPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (GpuPickerPopup is null || GpuPickerList is null) return;
        var adapters = AILogic.GpuAdapterEnumerator.List();
        int selectedId = AppConfig.Current?.AISettings?.InferenceGpuDeviceId ?? 0;

        GpuPickerList.Children.Clear();
        // No real adapters → tell the user where the diagnostic lives instead of leaving an empty
        // popup that looks broken.
        if (adapters.Count == 0)
        {
            var emptyMsg = new TextBlock
            {
                Text = "Keine GPU erkannt. Log:\n" + System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerAim_GpuEnum.log"),
                Padding = new Thickness(10, 8, 10, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            };
            GpuPickerList.Children.Add(emptyMsg);
        }
        foreach (var adapter in adapters)
        {
            bool isCurrent = adapter.DeviceId == selectedId;
            var rowBtn = new Button
            {
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 1, 0, 1),
                Background = isCurrent
                    ? (System.Windows.Media.Brush)FindResource("FluentSurface3")
                    : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = adapter.DeviceId,
            };
            var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = isCurrent ? "" : string.Empty, // U+E73E = Fluent CheckMark glyph
                FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 12,
                Width = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("FluentAccent"),
                Margin = new Thickness(0, 0, 8, 0),
            });
            stack.Children.Add(new TextBlock
            {
                Text = adapter.DisplayLabel,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("FluentTextPrimary"),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                FontSize = 13,
            });
            rowBtn.Content = stack;
            rowBtn.Click += GpuPickerRow_Click;
            GpuPickerList.Children.Add(rowBtn);
        }

        // Footer: refresh + open diagnostic log. Refresh re-enumerates (useful after a driver
        // install or eGPU plug-in); open-log surfaces enumeration details when the list looks wrong.
        var footer = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var refreshBtn = new Button
        {
            Content = "Refresh",
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 4, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontSize = 11,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        refreshBtn.Click += (_, _) =>
        {
            AILogic.GpuAdapterEnumerator.Invalidate();
            GpuPickerPopup.IsOpen = false;
            GpuPickerButton_Click(this, new RoutedEventArgs());
        };
        var openLogBtn = new Button
        {
            Content = "Open diagnostic log",
            Padding = new Thickness(8, 4, 8, 4),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontSize = 11,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        openLogBtn.Click += (_, _) =>
        {
            try
            {
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerAim_GpuEnum.log");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[GpuPicker] open log failed: {ex.Message}"); }
        };
        footer.Children.Add(refreshBtn);
        footer.Children.Add(openLogBtn);
        GpuPickerList.Children.Add(footer);

        GpuPickerPopup.IsOpen = true;
    }

    private void GpuPickerRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int newDeviceId }) return;
        if (AppConfig.Current?.AISettings is null) return;
        if (AppConfig.Current.AISettings.InferenceGpuDeviceId == newDeviceId)
        {
            GpuPickerPopup.IsOpen = false;
            return;
        }
        AppConfig.Current.AISettings.InferenceGpuDeviceId = newDeviceId;
        AppConfig.Current.Save();
        GpuPickerPopup.IsOpen = false;
        RefreshGpuPickerLabel();
        // Reload the currently active model so the new device-id actually takes effect — without
        // this the change only kicks in on the next manual reload / app restart.
        try { LoadModel(); }
        catch (Exception ex) { Debug.WriteLine($"[GpuPicker] LoadModel after GPU change failed: {ex.Message}"); }
    }

    // ===================================================================== LAYOUT MANAGER ====

    private readonly Dictionary<string, PageLayoutManager> _pageLayouts = new();
    private HiddenBoxesPill? _hiddenBoxesPill;

    private static readonly string[] _layoutManagedPages =
    [
        "AimMenu", "ModelMenu", "SettingsMenu", "AutoPlayMenu",
        "Tools", "Logs", "AboutMenu", "GamepadSettings"
    ];

    /// <summary>
    ///     Attach a <see cref="PageLayoutManager"/> to whichever pages already
    ///     have a fully-realised visual tree. Collapsed pages still lazy-attach on first nav via
    ///     <see cref="EnsurePageAttached"/>. Called after <see cref="CreateUI"/> finishes.
    /// </summary>
    private void AttachLayoutManagers()
    {
        // Do NOT clear _pageLayouts here. CreateUI() re-runs on every config load / language
        // change, but it only repopulates the inner StackPanels — the FluentCard Borders, named
        // panels and their already-attached chrome (drag + hide-×) survive. Throwing the managers
        // away and re-Attaching would yield empty new managers (DiscoverBoxes skips borders already
        // tagged from the first attach) while the × buttons still drive the old manager — so the
        // hidden-boxes pill would never update. Keeping the existing managers keeps the pill bound
        // to the same manager the × buttons use. EnsurePageAttached skips pages already attached.
        foreach (var name in _layoutManagedPages)
            EnsurePageAttached(name);
    }

    /// <summary>
    ///     Lazy attach for a single page. Idempotent in the success case.
    ///     <para>
    ///     A collapsed ScrollViewer hasn't been measured yet, so its template isn't applied
    ///     and its visual tree is empty — the initial bulk pass in <see cref="AttachLayoutManagers"/>
    ///     therefore inserts an empty PageLayoutManager for every off-screen page. When the user
    ///     later navigates to that page (it goes Visible, WPF realises the template), we need to
    ///     re-attach. So: if the existing entry has zero boxes, drop it and try again. Pages that
    ///     genuinely have no boxes will just keep ending up with an empty manager — harmless.
    ///     </para>
    /// </summary>
    private void EnsurePageAttached(string name)
    {
        if (_pageLayouts.TryGetValue(name, out var existing) && existing.Boxes.Count > 0)
            return;
        if (FindName(name) is not FrameworkElement page) return;
        _pageLayouts[name] = PageLayoutManager.Attach(name, page);
    }

    private void EnsureHiddenBoxesPill()
    {
        if (_hiddenBoxesPill is not null) return;
        // Inject the pill into the outermost Grid that hosts the page area. The first child of
        // MainWindow is a Grid (the row/column layout); we put the pill there with high Z-index.
        if (Content is Grid root)
            _hiddenBoxesPill = new HiddenBoxesPill(root);
    }

    private void BindHiddenBoxesPillForCurrentPage()
    {
        EnsureHiddenBoxesPill();
        _pageLayouts.TryGetValue(CurrentMenu ?? "", out var mgr);
        _hiddenBoxesPill?.Bind(mgr);
    }

    // ===================================================================== GLOBAL SEARCH ====

    /// <summary>Bound to Ctrl+F via Window.InputBindings in XAML.</summary>
    public static readonly System.Windows.Input.RoutedUICommand OpenSearchCommand =
        new("Open Search", nameof(OpenSearchCommand), typeof(MainWindow));

    private List<PowerAim.Class.SearchEntry>? _searchIndex;

    private void GlobalSearchButton_Click(object sender, RoutedEventArgs e)
    {
        OpenGlobalSearch();
    }

    private void OpenSearch_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        OpenGlobalSearch();
    }

    private void OpenGlobalSearch()
    {
        // Rebuild on each open — UI may have grown (e.g. profiles added, dialogs hosted).
        _searchIndex = PowerAim.Class.GlobalSearch.BuildIndex(this);
        GlobalSearchPopup.IsOpen = true;
        GlobalSearchBox.Text = "";
        RenderSearchResults("");
        Dispatcher.BeginInvoke(new Action(() => GlobalSearchBox.Focus()),
            System.Windows.Threading.DispatcherPriority.Render);
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => RenderSearchResults(GlobalSearchBox.Text);

    private void GlobalSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            GlobalSearchPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Enter)
        {
            // Activate the first result if there is one.
            if (GlobalSearchResults.Items.Count > 0
                && GlobalSearchResults.Items[0] is FrameworkElement first
                && first.Tag is PowerAim.Class.SearchEntry entry)
            {
                _ = ActivateSearchResult(entry);
            }
            e.Handled = true;
        }
    }

    private void RenderSearchResults(string query)
    {
        GlobalSearchResults.Items.Clear();
        if (_searchIndex is null) return;
        var matches = PowerAim.Class.GlobalSearch.Filter(_searchIndex, query);
        if (matches.Count == 0)
        {
            GlobalSearchHint.Text = Locale.NoMatches;
            return;
        }
        GlobalSearchHint.Text = Locale.SearchMatchesFormat.FormatWith(matches.Count);
        foreach (var entry in matches)
            GlobalSearchResults.Items.Add(BuildResultRow(entry));
    }

    private FrameworkElement BuildResultRow(PowerAim.Class.SearchEntry entry)
    {
        var border = new System.Windows.Controls.Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 2),
            CornerRadius = new CornerRadius(4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = entry,
            Background = System.Windows.Media.Brushes.Transparent,
            ToolTip = string.IsNullOrEmpty(entry.Description) ? null : entry.Description
        };
        var grid = new System.Windows.Controls.Grid();
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

        var sp = new System.Windows.Controls.StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = entry.Label,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextPrimary")
        });
        var sub = new TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Small"),
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextTertiary"),
            Text = string.IsNullOrEmpty(entry.MenuTag)
                ? entry.Category
                : $"{entry.Category}  ·  {entry.MenuTag.Replace("Menu", "")}"
        };
        sp.Children.Add(sub);
        System.Windows.Controls.Grid.SetColumn(sp, 0);
        grid.Children.Add(sp);
        border.Child = grid;

        border.MouseEnter += (_, _) => border.Background = (System.Windows.Media.Brush)FindResource("FluentSurface3");
        border.MouseLeave += (_, _) => border.Background = System.Windows.Media.Brushes.Transparent;
        border.MouseLeftButtonDown += async (_, _) => await ActivateSearchResult(entry);
        return border;
    }

    private async Task ActivateSearchResult(PowerAim.Class.SearchEntry entry)
    {
        GlobalSearchPopup.IsOpen = false;
        // Switch pages if the entry lives on a different one.
        if (!string.IsNullOrEmpty(entry.MenuTag) && CurrentMenu != entry.MenuTag)
        {
            try { await NavigateTo(entry.MenuTag, animate: true); }
            catch { /* navigation failure isn't fatal — flash will still try */ }
            // Give the section layout one render cycle before measuring scroll positions.
            await Task.Delay(220);
        }
        try { PowerAim.Class.GlobalSearch.RevealAndFlash(entry.Target); }
        catch { /* visual tree could be in a transient state — best-effort */ }
    }

    private const double SidebarCompactWidth = 48;
    private const double SidebarExpandedWidth = 220;
    private bool _sidebarExpanded;

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        _sidebarExpanded = !_sidebarExpanded;
        var target = _sidebarExpanded ? SidebarExpandedWidth : SidebarCompactWidth;
        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        Sidebar.BeginAnimation(FrameworkElement.WidthProperty, anim);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _magnifier?.Dispose();
        _fileManager.Dispose();
        FileManager.AIManager?.Dispose();
        GamepadManager.Dispose();

        FOV.Instance?.Close();

        if (AppConfig.Current.DropdownState.MouseMovementMethod == MouseMovementMethod.LGHUB) LGMouse.Close();

        AppConfig.Current.Save();
        Application.Current.Shutdown();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    #endregion Loading Window

    #region Menu Logic

    private string CurrentMenu = nameof(AimMenu);

    private async void MenuSwitch(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton && !_currentlySwitching)
        {
            var name = clickedButton.Tag?.ToString();
            if (name is not null && CurrentMenu != name)
            {
                await NavigateTo(name, true, clickedButton);
            }
        }
    }

    /// <summary>
    ///     Wire the embedded help panel's "back" button so it returns to whichever menu the user
    ///     was on before opening Help. Called once during UI bootstrap.
    /// </summary>
    private void WireHelpPanel()
    {
        if (HelpPanelHost != null)
        {
            HelpPanelHost.BackRequested -= HelpPanel_BackRequested;
            HelpPanelHost.BackRequested += HelpPanel_BackRequested;
        }
    }

    private string? _helpReturnTo;
    private void HelpPanel_BackRequested(object? sender, EventArgs e) =>
        _ = NavigateTo(_helpReturnTo ?? nameof(AimMenu));

    private Button? FindNavButton(string name)
    {
        return MenuButtons?.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == name)
            ?? MenuButtonsBottom?.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == name);
    }

    internal async Task NavigateTo(string name, bool animate = true, Button? clickedButton = null)
    {
        // Track the previously-active menu when entering the Help page so its Back button can
        // restore the user's location. Don't update on Help→Help re-entry (which would erase the
        // real back-target).
        if (name == nameof(HelpPage) && CurrentMenu != nameof(HelpPage))
            _helpReturnTo = CurrentMenu;

        if (SectionLabel is not null)
        {
            var section = string.Join(" ", name.Replace("Menu", "").SplitByUpperCase()).ToUpper();
            SectionLabel.Content = section == "AIM" ? Locale.MainSection : section;
        }

        clickedButton ??= FindNavButton(name);
        _currentlySwitching = true;
        if (clickedButton is not null && MenuHighlighter?.Parent is UIElement highlighterParent)
        {
            void Move()
            {
                try
                {
                    var transform = clickedButton.TransformToAncestor(highlighterParent);
                    var topInParent = transform.Transform(new System.Windows.Point(0, 0)).Y;
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(animate ? 220 : 0), MenuHighlighter, MenuHighlighter.Margin, new Thickness(0, topInParent, 0, 0));
                }
                catch
                {
                }
            }
            if (!clickedButton.IsLoaded || clickedButton.ActualHeight <= 0)
                clickedButton.Dispatcher.BeginInvoke(new Action(Move), System.Windows.Threading.DispatcherPriority.Loaded);
            else
                Move();
        }
        await SwitchScrollPanels(FindName(name) as FrameworkElement ?? throw new NullReferenceException("Page is null"), animate);
        CurrentMenu = name;
        // First-visit attachment: pages that started Collapsed need their visual tree walked
        // *after* they've been made visible. Wait one render tick so templates have applied.
        Dispatcher.BeginInvoke(new Action(() => EnsurePageAttached(name)),
            System.Windows.Threading.DispatcherPriority.Loaded);
        // Rebind the floating "hidden sections" pill to the new page's layout manager so it shows
        // counts for the page the user is actually looking at.
        Dispatcher.BeginInvoke(new Action(BindHiddenBoxesPillForCurrentPage),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private async Task SwitchScrollPanels(FrameworkElement movingScrollViewer, bool animate = true)
    {
        if (_currentScrollViewer is not null && _currentScrollViewer != movingScrollViewer)
        {
            _currentScrollViewer.Visibility = Visibility.Collapsed;
            _currentScrollViewer.Opacity = 1;
            _currentScrollViewer.RenderTransform = null;
        }

        movingScrollViewer.Visibility = Visibility.Visible;

        if (animate)
        {
            var translate = new System.Windows.Media.TranslateTransform(0, 8);
            movingScrollViewer.RenderTransform = translate;
            movingScrollViewer.Opacity = 0;

            var duration = TimeSpan.FromMilliseconds(180);
            var ease = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            };

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(8, 0, duration) { EasingFunction = ease };

            movingScrollViewer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideIn);

            await Task.Delay(duration);
            movingScrollViewer.RenderTransform = null;
        }
        else
        {
            movingScrollViewer.Opacity = 1;
            movingScrollViewer.RenderTransform = null;
        }

        _currentScrollViewer = movingScrollViewer;
        _currentlySwitching = false;
    }

    private void UnifiedSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = ((TextBox)sender).Text?.ToLower() ?? string.Empty;
        ApplySearchFilter(searchText);
    }

    private void ApplySearchFilter(string searchText)
    {
        FilterListBox(ModelListBox, searchText);
        FilterListBox(ConfigsListBox, searchText);
        FilterDownloadPanel(ModelStoreScroller, searchText);
        FilterDownloadPanel(ConfigStoreScroller, searchText);
    }

    private static void FilterListBox(System.Windows.Controls.ListBox? list, string searchText)
    {
        if (list is null) return;
        foreach (var item in list.Items)
        {
            if (list.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container) continue;
            var match = string.IsNullOrEmpty(searchText) || (item?.ToString()?.ToLower().Contains(searchText) ?? false);
            container.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void FilterDownloadPanel(Panel? panel, string searchText)
    {
        if (panel is null) return;
        foreach (var item in panel.Children.OfType<ADownloadGateway>())
        {
            var match = string.IsNullOrEmpty(searchText) || (item.Title.Content?.ToString()?.ToLower().Contains(searchText) ?? false);
            item.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SegModels_Click(object sender, RoutedEventArgs e)
    {
        SegModels.Style = (Style)FindResource("FluentSegmentActive");
        SegConfigs.Style = (Style)FindResource("FluentSegment");
        ModelsGroup.Visibility = Visibility.Visible;
        ConfigsGroup.Visibility = Visibility.Collapsed;
    }

    private void SegConfigs_Click(object sender, RoutedEventArgs e)
    {
        SegModels.Style = (Style)FindResource("FluentSegment");
        SegConfigs.Style = (Style)FindResource("FluentSegmentActive");
        ModelsGroup.Visibility = Visibility.Collapsed;
        ConfigsGroup.Visibility = Visibility.Visible;
    }

    private string? _triggerEditReturnTo;
    private PowerAim.Config.ActionTrigger? _triggerEditTarget;
    private bool _triggerEditIsNew;
    private Action<PowerAim.Config.ActionTrigger?>? _triggerEditCommit;
    private global::UILibrary.TriggerEdit? _triggerEditor;
    private bool _triggerEditDirty;
    private System.ComponentModel.PropertyChangedEventHandler? _triggerDirtyHandler;

    // Keeps the FOV-size sliders' max in lockstep with the active model ImageSize.

    public void OpenTriggerEditor(PowerAim.Config.ActionTrigger target, bool isNew, Action<PowerAim.Config.ActionTrigger?> commit)
    {
        _triggerEditReturnTo = CurrentMenu;
        _triggerEditTarget = target;
        _triggerEditIsNew = isNew;
        _triggerEditCommit = commit;
        if (!isNew) target.BeginEdit();
        TriggerEditTitle.Text = isNew ? Locale.AddTrigger : Locale.EditTrigger;
        TriggerEditName.Text = target.Name ?? "";
        _triggerEditDirty = false;
        TriggerEditDirty.Visibility = Visibility.Collapsed;
        if (_triggerEditor == null)
        {
            _triggerEditor = new global::UILibrary.TriggerEdit();
            TriggerEditorHost.Content = _triggerEditor;
        }
        _triggerEditor.Trigger = target;

        // Subscribe to property changes for dirty-tracking and live name update.
        // Use BeginInvoke so initial binding fan-out doesn't mark the form as dirty.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_triggerEditTarget, target)) return;
            _triggerDirtyHandler = (s, e) =>
            {
                _triggerEditDirty = true;
                TriggerEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.ActionTrigger.Name))
                {
                    TriggerEditName.Text = target.Name ?? "";
                }
            };
            target.PropertyChanged += _triggerDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(TriggerEditPage));
    }

    private void CloseTriggerEditor(bool save)
    {
        var target = _triggerEditTarget;
        var isNew = _triggerEditIsNew;
        var commit = _triggerEditCommit;
        var returnTo = _triggerEditReturnTo ?? nameof(AimMenu);

        // If trying to leave without saving, warn about unsaved changes.
        if (!save && _triggerEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage,
                Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return; // stay on editor
            if (res == MessageDialog.DialogResult.Yes) save = true; // proceed as if Save was clicked
            // No → discard (continue with save=false)
        }

        // Detach dirty handler
        if (target != null && _triggerDirtyHandler != null)
        {
            target.PropertyChanged -= _triggerDirtyHandler;
        }
        _triggerDirtyHandler = null;
        _triggerEditDirty = false;
        TriggerEditDirty.Visibility = Visibility.Collapsed;

        if (target != null && !isNew)
        {
            if (save) target.EndEdit();
            else target.CancelEdit();
        }

        SetSidebarLocked(false);
        _triggerEditTarget = null;
        _triggerEditCommit = null;
        if (_triggerEditor != null) _triggerEditor.Trigger = null!;
        commit?.Invoke(save ? target : null);
        _ = NavigateTo(returnTo);
    }

    // ===== AutoPlay profile editor (in-window page, analog to the trigger editor) =====
    private global::UILibrary.AutoPlayProfileEdit? _autoPlayEditor;
    private string? _autoPlayEditReturnTo;
    private PowerAim.Config.AutoPlayProfile? _autoPlayEditTarget;
    private bool _autoPlayEditIsNew;
    private Action<PowerAim.Config.AutoPlayProfile?>? _autoPlayEditCommit;
    private bool _autoPlayEditDirty;
    private System.ComponentModel.PropertyChangedEventHandler? _autoPlayDirtyHandler;

    public void OpenAutoPlayEditor(PowerAim.Config.AutoPlayProfile target, bool isNew, Action<PowerAim.Config.AutoPlayProfile?> commit)
    {
        _autoPlayEditReturnTo = CurrentMenu;
        _autoPlayEditTarget = target;
        _autoPlayEditIsNew = isNew;
        _autoPlayEditCommit = commit;
        if (!isNew) target.BeginEdit();
        AutoPlayEditTitle.Text = isNew ? Locale.AddAutoPlayProfile : Locale.EditAutoPlayProfile;
        AutoPlayEditName.Text = target.Name ?? "";
        _autoPlayEditDirty = false;
        AutoPlayEditDirty.Visibility = Visibility.Collapsed;
        if (_autoPlayEditor == null)
        {
            _autoPlayEditor = new global::UILibrary.AutoPlayProfileEdit();
            AutoPlayEditorHost.Content = _autoPlayEditor;
        }
        _autoPlayEditor.Profile = target;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_autoPlayEditTarget, target)) return;
            _autoPlayDirtyHandler = (s, e) =>
            {
                _autoPlayEditDirty = true;
                AutoPlayEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.AutoPlayProfile.Name))
                    AutoPlayEditName.Text = target.Name ?? "";
            };
            target.PropertyChanged += _autoPlayDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(AutoPlayEditPage));
    }

    private void CloseAutoPlayEditor(bool save)
    {
        var target = _autoPlayEditTarget;
        var isNew = _autoPlayEditIsNew;
        var commit = _autoPlayEditCommit;
        var returnTo = _autoPlayEditReturnTo ?? nameof(AutoPlayMenu);

        if (!save && _autoPlayEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage,
                Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return;
            if (res == MessageDialog.DialogResult.Yes) save = true;
        }

        if (target != null && _autoPlayDirtyHandler != null)
            target.PropertyChanged -= _autoPlayDirtyHandler;
        _autoPlayDirtyHandler = null;
        _autoPlayEditDirty = false;
        AutoPlayEditDirty.Visibility = Visibility.Collapsed;

        if (target != null && !isNew)
        {
            if (save) target.EndEdit();
            else target.CancelEdit();
        }

        SetSidebarLocked(false);
        _autoPlayEditTarget = null;
        _autoPlayEditCommit = null;
        // Intentionally NOT clearing _autoPlayEditor.Profile: the panel's UpdateDynamicUi binds to
        // Profile.DecisionInterval without a null-guard, so null would throw. The next open replaces it.
        commit?.Invoke(save ? target : null);
        _ = NavigateTo(returnTo);
    }

    private void AutoPlayEditBack_Click(object sender, RoutedEventArgs e) => CloseAutoPlayEditor(false);
    private void AutoPlayEditCancel_Click(object sender, RoutedEventArgs e) => CloseAutoPlayEditor(false);
    private void AutoPlayEditSave_Click(object sender, RoutedEventArgs e) => CloseAutoPlayEditor(true);

    // ===== AntiRecoil profile editor (in-window page, analog to TriggerEditPage / AutoPlayEditPage) =====
    private global::UILibrary.AntiRecoilProfileEdit? _antiRecoilEditor;
    private string? _antiRecoilEditReturnTo;
    private PowerAim.Config.AntiRecoilProfile? _antiRecoilEditTarget;
    private bool _antiRecoilEditIsNew;
    private Action<PowerAim.Config.AntiRecoilProfile?>? _antiRecoilEditCommit;
    private bool _antiRecoilEditDirty;
    private System.ComponentModel.PropertyChangedEventHandler? _antiRecoilDirtyHandler;

    /// <summary>
    ///     Open the AntiRecoil profile editor in-window (Page) instead of a modal dialog. Mirrors
    ///     <see cref="OpenAutoPlayEditor"/> exactly: BeginEdit on existing profiles so Cancel
    ///     rolls back, dirty-flag tracking for the unsaved-changes prompt, sidebar locked while
    ///     editing.
    /// </summary>
    public void OpenAntiRecoilEditor(PowerAim.Config.AntiRecoilProfile target, bool isNew, Action<PowerAim.Config.AntiRecoilProfile?> commit)
    {
        _antiRecoilEditReturnTo = CurrentMenu;
        _antiRecoilEditTarget = target;
        _antiRecoilEditIsNew = isNew;
        _antiRecoilEditCommit = commit;
        if (!isNew) target.BeginEdit();
        AntiRecoilEditTitle.Text = isNew ? Locale.AntiRecoilAddProfile : Locale.AntiRecoilProfileEdit;
        AntiRecoilEditName.Text = target.Name ?? "";
        _antiRecoilEditDirty = false;
        AntiRecoilEditDirty.Visibility = Visibility.Collapsed;
        if (_antiRecoilEditor == null)
        {
            _antiRecoilEditor = new global::UILibrary.AntiRecoilProfileEdit();
            AntiRecoilEditorHost.Content = _antiRecoilEditor;
        }
        _antiRecoilEditor.Profile = target;

        // Dirty-tracking via PropertyChanged — same shape as AutoPlay.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_antiRecoilEditTarget, target)) return;
            _antiRecoilDirtyHandler = (s, e) =>
            {
                _antiRecoilEditDirty = true;
                AntiRecoilEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.AntiRecoilProfile.Name))
                {
                    AntiRecoilEditName.Text = target.Name ?? "";
                }
            };
            target.PropertyChanged += _antiRecoilDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(AntiRecoilEditPage));
    }

    private void CloseAntiRecoilEditor(bool save)
    {
        var target = _antiRecoilEditTarget;
        var isNew = _antiRecoilEditIsNew;
        var commit = _antiRecoilEditCommit;
        var returnTo = _antiRecoilEditReturnTo ?? nameof(AimMenu);

        if (!save && _antiRecoilEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage, Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return;
            if (res == MessageDialog.DialogResult.Yes) save = true;
        }

        if (target != null && _antiRecoilDirtyHandler != null)
            target.PropertyChanged -= _antiRecoilDirtyHandler;
        _antiRecoilDirtyHandler = null;
        _antiRecoilEditDirty = false;
        AntiRecoilEditDirty.Visibility = Visibility.Collapsed;

        if (target != null && !isNew)
        {
            if (save) target.EndEdit();
            else target.CancelEdit();
        }

        SetSidebarLocked(false);
        _antiRecoilEditTarget = null;
        _antiRecoilEditCommit = null;
        commit?.Invoke(save ? target : null);
        _ = NavigateTo(returnTo);
    }

    private void AntiRecoilEditBack_Click(object sender, RoutedEventArgs e)   => CloseAntiRecoilEditor(false);
    private void AntiRecoilEditCancel_Click(object sender, RoutedEventArgs e) => CloseAntiRecoilEditor(false);
    private void AntiRecoilEditSave_Click(object sender, RoutedEventArgs e)   => CloseAntiRecoilEditor(true);

    // ===== Aim profile editor (in-window page, analog to AntiRecoilEditPage) =====
    private global::UILibrary.AimProfileEdit? _aimEditor;
    private string? _aimEditReturnTo;
    private PowerAim.Config.AimProfile? _aimEditTarget;
    private bool _aimEditIsNew;
    private Action<PowerAim.Config.AimProfile?>? _aimEditCommit;
    private bool _aimEditDirty;
    private System.ComponentModel.PropertyChangedEventHandler? _aimDirtyHandler;

    /// <summary>Open the aim-profile editor in-window. Mirrors <see cref="OpenAntiRecoilEditor"/>.</summary>
    public void OpenAimEditor(PowerAim.Config.AimProfile target, bool isNew, Action<PowerAim.Config.AimProfile?> commit)
    {
        _aimEditReturnTo = CurrentMenu;
        _aimEditTarget = target;
        _aimEditIsNew = isNew;
        _aimEditCommit = commit;
        if (!isNew) target.BeginEdit();
        AimEditTitle.Text = isNew ? Locale.AimAddProfile : Locale.AimProfileEdit;
        AimEditName.Text = target.Name ?? "";
        _aimEditDirty = false;
        AimEditDirty.Visibility = Visibility.Collapsed;
        if (_aimEditor == null)
        {
            _aimEditor = new global::UILibrary.AimProfileEdit();
            AimEditorHost.Content = _aimEditor;
        }
        _aimEditor.Profile = target;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_aimEditTarget, target)) return;
            _aimDirtyHandler = (s, e) =>
            {
                _aimEditDirty = true;
                AimEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.AimProfile.Name))
                    AimEditName.Text = target.Name ?? "";
            };
            target.PropertyChanged += _aimDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(AimEditPage));
    }

    private void CloseAimEditor(bool save)
    {
        var target = _aimEditTarget;
        var isNew = _aimEditIsNew;
        var commit = _aimEditCommit;
        var returnTo = _aimEditReturnTo ?? nameof(AimMenu);

        if (!save && _aimEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage, Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return;
            if (res == MessageDialog.DialogResult.Yes) save = true;
        }

        if (target != null && _aimDirtyHandler != null)
            target.PropertyChanged -= _aimDirtyHandler;
        _aimDirtyHandler = null;
        _aimEditDirty = false;
        AimEditDirty.Visibility = Visibility.Collapsed;

        if (target != null && !isNew)
        {
            if (save) target.EndEdit();
            else target.CancelEdit();
        }

        SetSidebarLocked(false);
        _aimEditTarget = null;
        _aimEditCommit = null;
        commit?.Invoke(save ? target : null);

        // If the edited profile is the active one, re-apply its (possibly changed) values to the
        // live settings the pipeline reads.
        if (save && target != null && AppConfig.Current?.AimSettings?.ActiveProfileId == target.Id)
            target.Apply();

        _ = NavigateTo(returnTo);
    }

    private void AimEditBack_Click(object sender, RoutedEventArgs e)   => CloseAimEditor(false);
    private void AimEditCancel_Click(object sender, RoutedEventArgs e) => CloseAimEditor(false);
    private void AimEditSave_Click(object sender, RoutedEventArgs e)   => CloseAimEditor(true);

    private void SetSidebarLocked(bool locked)
    {
        if (Sidebar is not null)
        {
            Sidebar.IsHitTestVisible = !locked;
            Sidebar.Opacity = locked ? 0.4 : 1.0;
        }
        if (HamburgerButton is not null) HamburgerButton.IsEnabled = !locked;
    }

    private void TriggerEditBack_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(false);
    private void TriggerEditCancel_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(false);
    private void TriggerEditSave_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(true);

    // ============================================================================ MAPPING EDITOR ====

    private string? _mappingEditReturnTo;
    private PowerAim.Config.ControllerMappingProfile? _mappingEditTarget;
    private bool _mappingEditDirty;
    private bool _mappingEditIsNew;
    private Action<PowerAim.Config.ControllerMappingProfile?>? _mappingEditCommit;
    private System.ComponentModel.PropertyChangedEventHandler? _mappingDirtyHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _mappingCollectionDirtyHandler;
    private List<PowerAim.Config.InputMapping>? _mappingEditSnapshot;

    /// <summary>
    ///     True while the mapping editor is open. The <see cref="MappingPage"/>'s
    ///     auto-save handlers check this to skip writing during editing — otherwise every keystroke
    ///     would persist the in-flight state and Discard would have nothing to roll back to.
    /// </summary>
    public bool IsMappingEditorOpen { get; private set; }

    /// <summary>
    ///     Open the mapping editor.
    ///     <para>
    ///     <paramref name="isNew"/> = true: <paramref name="target"/> is a draft NOT in the
    ///     profiles collection yet. <paramref name="commit"/> is invoked with the saved profile
    ///     (or null on discard). This is the trigger-editor pattern — prevents zombie "Profile N"
    ///     entries from clicking + immediately discarding.
    ///     </para>
    ///     <para>
    ///     <paramref name="isNew"/> = false: target is already in the collection. We snapshot its
    ///     mappings list + call BeginEdit; on Discard we restore from the snapshot AND call
    ///     CancelEdit so both property changes and inner mappings-list mutations roll back.
    ///     </para>
    /// </summary>
    public void OpenMappingEditor(
        PowerAim.Config.ControllerMappingProfile target,
        bool isNew = false,
        Action<PowerAim.Config.ControllerMappingProfile?>? commit = null)
    {
        _mappingEditReturnTo = CurrentMenu;
        _mappingEditTarget = target;
        _mappingEditIsNew = isNew;
        _mappingEditCommit = commit;
        IsMappingEditorOpen = true;

        if (!isNew)
        {
            // For existing profiles: take a snapshot of the current Mappings list so Discard can
            // restore it. Nextended's BeginEdit handles primitive properties; the inner collection
            // mutations are NOT covered by IEditableObject contract.
            _mappingEditSnapshot = target.Mappings.Select(ClonedMapping).ToList();
            target.BeginEdit();
        }
        else
        {
            _mappingEditSnapshot = null;
        }

        MappingEditName.Text = target.Name ?? "";
        _mappingEditDirty = isNew; // new drafts start "dirty" so save is the only way to commit
        MappingEditDirty.Visibility = _mappingEditDirty ? Visibility.Visible : Visibility.Collapsed;

        MappingEditor.Profile = target;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(_mappingEditTarget, target)) return;
            _mappingDirtyHandler = (s, e) =>
            {
                _mappingEditDirty = true;
                MappingEditDirty.Visibility = Visibility.Visible;
                if (e.PropertyName == nameof(PowerAim.Config.ControllerMappingProfile.Name))
                    MappingEditName.Text = target.Name ?? "";
            };
            target.PropertyChanged += _mappingDirtyHandler;

            _mappingCollectionDirtyHandler = (s, e) =>
            {
                _mappingEditDirty = true;
                MappingEditDirty.Visibility = Visibility.Visible;
            };
            target.Mappings.CollectionChanged += _mappingCollectionDirtyHandler;
        }), System.Windows.Threading.DispatcherPriority.Background);

        SetSidebarLocked(true);
        _ = NavigateTo(nameof(MappingEditPage));
    }

    private static PowerAim.Config.InputMapping ClonedMapping(PowerAim.Config.InputMapping m) => new()
    {
        SourceKind = m.SourceKind,
        SourceCode = m.SourceCode,
        TargetKind = m.TargetKind,
        TargetCode = m.TargetCode,
        Enabled = m.Enabled,
        Activator = m.Activator,
        LongPressMs = m.LongPressMs,
        ModifierKind = m.ModifierKind,
        ModifierCode = m.ModifierCode,
    };

    private void CloseMappingEditor(bool save)
    {
        var target = _mappingEditTarget;
        var returnTo = _mappingEditReturnTo ?? nameof(MappingMenu);
        var isNew = _mappingEditIsNew;
        var commit = _mappingEditCommit;
        var snapshot = _mappingEditSnapshot;

        if (!save && _mappingEditDirty)
        {
            var res = MessageDialog.Show(
                Locale.UnsavedChangesMessage,
                Locale.UnsavedChanges,
                MessageDialog.DialogButtons.YesNoCancel,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.Yes);
            if (res == MessageDialog.DialogResult.Cancel) return;
            if (res == MessageDialog.DialogResult.Yes) save = true;
        }

        if (target != null)
        {
            if (_mappingDirtyHandler != null)
                target.PropertyChanged -= _mappingDirtyHandler;
            if (_mappingCollectionDirtyHandler != null)
                target.Mappings.CollectionChanged -= _mappingCollectionDirtyHandler;
            if (save)
            {
                if (!isNew) target.EndEdit();
                // Hand draft to the commit callback (which appends to collection for new
                // profiles) BEFORE writing to disk so the JSON contains it.
                commit?.Invoke(target);
                AppConfig.Current?.Save();
            }
            else
            {
                if (!isNew)
                {
                    // Roll back inner mappings list to the snapshot (IEditableObject doesn't cover
                    // ObservableCollection mutations), then roll back primitive properties.
                    if (snapshot != null)
                    {
                        target.Mappings.Clear();
                        foreach (var m in snapshot)
                            target.Mappings.Add(m);
                    }
                    target.CancelEdit();
                }
                // For isNew drafts: do nothing — the profile was never in the collection.
                commit?.Invoke(null);
            }
        }
        _mappingDirtyHandler = null;
        _mappingCollectionDirtyHandler = null;
        _mappingEditDirty = false;
        _mappingEditSnapshot = null;
        _mappingEditCommit = null;
        _mappingEditIsNew = false;
        MappingEditDirty.Visibility = Visibility.Collapsed;
        MappingEditor.Profile = null;
        _mappingEditTarget = null;
        IsMappingEditorOpen = false;

        SetSidebarLocked(false);
        _ = NavigateTo(returnTo);
    }

    private void MappingEditBack_Click(object sender, RoutedEventArgs e) => CloseMappingEditor(false);
    private void MappingEditCancel_Click(object sender, RoutedEventArgs e) => CloseMappingEditor(false);
    private void MappingEditSave_Click(object sender, RoutedEventArgs e) => CloseMappingEditor(true);

    private async Task RunBenchmarkClick()
    {
        var modelFile = AppConfig.Current?.LastLoadedModel;
        if (string.IsNullOrWhiteSpace(modelFile) || modelFile == "N/A")
        {
            MessageDialog.Warn(Locale.BenchmarkNoModel, Locale.RunBenchmark, owner: this);
            return;
        }
        var modelPath = Path.Combine("bin/models", modelFile);
        if (!File.Exists(modelPath))
        {
            MessageDialog.Warn(Locale.BenchmarkNoModel, Locale.RunBenchmark, owner: this);
            return;
        }

        var notice = new NoticeBar(Locale.BenchmarkRunning, 60000);
        notice.Show();
        try
        {
            var result = await PowerAim.AILogic.PerformanceBenchmark.RunAsync(modelPath);
            notice.Close();

            var msg = Locale.BenchmarkRecommendedSize.FormatWith(result.RecommendedImageSize) + "\n\n";
            foreach (var s in result.Samples)
            {
                msg += $"• {s.ImageSize}px → {s.AvgFps:F1} fps ({s.AvgInferenceMs:F1} ms";
                if (s.GpuUtilizationPct > 0) msg += $", GPU {s.GpuUtilizationPct:F0}%";
                msg += ")\n";
            }
            if (!string.IsNullOrWhiteSpace(result.Notes)) msg += "\n" + result.Notes;

            MessageDialog.Show(
                msg, Locale.BenchmarkResult,
                MessageDialog.DialogButtons.OK,
                MessageDialog.DialogIcon.Info,
                owner: this);
        }
        catch (Exception ex)
        {
            notice.Close();
            MessageDialog.Error(ex.Message, Locale.RunBenchmark, owner: this);
        }
    }


    private void BindingOnKeyReleased(string bindingId)
    {
        switch (bindingId)
        {
            case nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind):
                AppConfig.Current.SliderSettings.OnPropertyChanged(nameof(AppConfig.Current.SliderSettings.ActualFovSize));
                if (FOV.Instance is not null)
                {
                    Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.FOVSize);
                    Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.FOVSize);
                }
                break;
        }
    }

    private void BindingOnKeyPressed(string bindingId)
    {
        if (AppConfig.Current?.ToggleState is { RequireGlobalActiveForKeybinds: true, GlobalActive: false })
            return;

        switch (bindingId)
        {
            case nameof(AppConfig.Current.BindingSettings.MagnifierZoomInKeybind):
                AppConfig.Current.SliderSettings.MagnificationFactor += AppConfig.Current.SliderSettings.MagnificationStepFactor;
                ValidateMagnificationFactor();
                break;
            case nameof(AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind):
                AppConfig.Current.SliderSettings.MagnificationFactor -= AppConfig.Current.SliderSettings.MagnificationStepFactor;
                ValidateMagnificationFactor();
                break;
            case nameof(AppConfig.Current.BindingSettings.MagnifierKeybind):
                ToggleMagnifier();
                break;
            case nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind):
                if (AppConfig.Current.BindingSettings.ModelSwitchKeybind.IsValid)
                    if (!FileManager.CurrentlyLoadingModel)
                    {
                        if (ModelListBox.SelectedIndex >= 0 &&
                            ModelListBox.SelectedIndex < ModelListBox.Items.Count - 1)
                            ModelListBox.SelectedIndex += 1;
                        else
                            ModelListBox.SelectedIndex = 0;
                    }

                break;

            case nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind):
                AppConfig.Current.SliderSettings.OnPropertyChanged(nameof(AppConfig.Current.SliderSettings.ActualFovSize));
                if (FOV.Instance is not null)
                {
                    Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.ActualFovSize);
                    Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.ActualFovSize);
                }

                break;


            case nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind):
                if (AppConfig.Current.ToggleState.AntiRecoil)
                {
                    AppConfig.Current.ToggleState.AntiRecoil = false;
                    new NoticeBar(Locale.DisableAntiRecoilKeybindExt, 4000).Show();
                }

                break;

            // Gun1Key / Gun2Key cases removed: the old "load gun-config file" behaviour is
            // superseded by the AntiRecoilProfile keybind activation (see AntiRecoilProfileManager
            // — each profile's KeyBind toggles ActiveProfileId). Binding fields remain on
            // BindingSettings for legacy compat but are unused.
        }
    }

    #endregion Menu Logic

    #region Menu Loading

    private void LoadAimMenu()
    {
        ConfigSettings.RemoveAll();
        ModelSettings.RemoveAll();
        AimAssist.RemoveAll();
        PredictionConfig.RemoveAll();
        AimConfig.RemoveAll();
        TriggerBot.RemoveAll();
        AntiRecoil.RemoveAll();
        FOVConfig.RemoveAll();
        ESPConfig.RemoveAll();

        #region Aim Assist

        var keybind = AppConfig.Current.BindingSettings;
        AimAssist.AddTitle(Locale.AimAssist, true);
        AimAssist.AddToggleWithKeyBind(Locale.AimAssist, nameof(Locale.AimAssist), BindingManager).BindTo(() => AppConfig.Current.ToggleState.AimAssist).BindActiveStateColor(AimAssist);

        AimAssist.AddMultiKeyChanger(Locale.AimKeyBindings, Locale.DescriptionAimKeyBindings).BindTo(() => AppConfig.Current.BindingSettings.AimKeyBindings);

        AimAssist.AddToggleWithKeyBind(Locale.Predictions, nameof(Locale.Predictions), BindingManager).BindTo(() => AppConfig.Current.ToggleState.Predictions);
        AimAssist.AddToggleWithKeyBind(Locale.EMASmoothening, nameof(Locale.EMASmoothening), BindingManager).BindTo(() => AppConfig.Current.ToggleState.EMASmoothening);
        AimAssist.AddSeparator();
        AimAssist.Visibility = GetVisibilityFor(nameof(AimAssist));

        #endregion Aim Assist

        #region Model Settings

        ModelSettings.AddTitle(Locale.ModelSettings);
        ModelSettings.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind), () => keybind.ModelSwitchKeybind, BindingManager);
        ModelSettings.AddCredit("", Locale.ModelKeyBindHelp);

        // Image-size override (only used for dynamic-shape models)
        ModelSettings.AddSlider(Locale.ImageSizeOverride, Locale.Pixels, 32, 32, 192, 1280)
            .InitWith(s => s.ToolTip = Locale.ImageSizeOverrideHelp)
            .BindTo(() => AppConfig.Current.SliderSettings.ImageSize);

        // Run Performance Benchmark
        ModelSettings.AddButton(Locale.RunBenchmark).Reader.Click += async (_, _) => await RunBenchmarkClick();

        ModelSettings.AddSeparator();

        #endregion

        #region Config Settings

        ConfigSettings.AddTitle(Locale.ConfigSettings);
        ConfigSettings.AddButton(Locale.SaveConfig).Reader.Click += (s, e) => new ConfigSaver { Owner = this }.ShowDialog();
        ConfigSettings.AddSeparator();

        #endregion

        #region Config


        PredictionConfig.AddTitle(Locale.PredictionConfig, true);
        PredictionConfig.AddDropdown(Locale.PredictionMethod, AppConfig.Current.DropdownState.PredictionMethod, v => AppConfig.Current.DropdownState.PredictionMethod = v);
        PredictionConfig.AddDropdown(Locale.DetectionAreaType, AppConfig.Current.DropdownState.DetectionAreaType, v => AppConfig.Current.DropdownState.DetectionAreaType = v);
        PredictionConfig.AddSlider(Locale.MaxInferenceFPS, Locale.FPS, 1, 5, 0, 240)
            .InitWith(s => s.ToolTip = Locale.MaxInferenceFPSHelp)
            .BindTo(() => AppConfig.Current.SliderSettings.MaxInferenceFPS);
        PredictionConfig.AddButton(Locale.TargetClassesButton).Reader.Click += (_, _) =>
        {
            var classes = FileManager.AIManager?.PredictionLogic?.ModelClasses
                          ?? new Dictionary<int, string>();
            new TargetClassDialog(classes) { Owner = this }.ShowDialog();
        };
        PredictionConfig.AddButton(Locale.DetectionMasksMenuItem).Reader.Click += (_, _) =>
        {
            new DetectionMasksDialog { Owner = this }.ShowDialog();
        };
        PredictionConfig.AddSeparator();
        PredictionConfig.Visibility = GetVisibilityFor(nameof(AimConfig));


        AimConfig.AddTitle(Locale.AimConfig, true);


        // (Calibrate-sensitivity removed — the new closed-loop damped controller converges without
        // pixel-to-rotation calibration. Aim-disengage moved into the per-profile edit page.)

        // ----- Aim profiles -----
        // All aim tuning (sensitivity, region, smart-tracking, smoothing, …) now lives per-profile
        // in the edit page — exactly like AntiRecoil. The active profile's values are applied to
        // the live settings the pipeline reads. Per-row hotkey toggles a profile active.
        // NB: no AddSubTitle here — a nested ATitle would halt the section's collapse animation
        // (ATitle.ApplyState stops at the next ATitle), leaving the list stuck visible.
        var aimProfileList = new global::UILibrary.AimProfileList { Margin = new Thickness(4) };
        aimProfileList.BindTo(() => AppConfig.Current.AimSettings.Profiles);
        AimConfig.Children.Add(aimProfileList);

        AimConfig.AddSeparator();
        AimConfig.Visibility = GetVisibilityFor(nameof(AimConfig));

        // Spin up the aim profile manager (keybind activation + OCR auto-switch). Idempotent.
        PowerAim.AILogic.AimProfileManager.EnsureInitialized();

        #endregion Config


        #region Trigger Bot

        TriggerBot.AddTitle(Locale.AutoTrigger, true);
        TriggerBot.AddToggleWithKeyBind(Locale.AutoTrigger, nameof(Locale.AutoTrigger), BindingManager).BindTo(() => AppConfig.Current.ToggleState.AutoTrigger).BindActiveStateColor(TriggerBot);
        TriggerBot.Add<TriggerList>().BindTo(() => AppConfig.Current.Triggers);

        TriggerBot.AddSeparator();
        TriggerBot.Visibility = GetVisibilityFor(nameof(TriggerBot));

        #endregion Trigger Bot

        #region Anti Recoil

        AntiRecoil.AddTitle(Locale.AntiRecoil, true);
        AntiRecoil.AddToggleWithKeyBind(Locale.AntiRecoil, nameof(Locale.AntiRecoil), BindingManager)
            .BindTo(() => AppConfig.Current.ToggleState.AntiRecoil)
            .BindActiveStateColor(AntiRecoil);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind), () => keybind.AntiRecoilKeybind, BindingManager);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind), () => keybind.DisableAntiRecoilKeybind, BindingManager);

        // Profile list — replaces the old monolithic Legacy/BETA/Pattern UI. Each profile bundles
        // an engine variant + parameters + optional activation conditions (hotkey, OCR weapon
        // match). The AntiRecoilProfileManager handles radio activation + notifications.
        var profilesHelpLabel = new System.Windows.Controls.Label
        {
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
            FontSize = 11,
            Padding = new Thickness(0),
            Margin = new Thickness(8, 4, 8, 4),
            Content = Locale.AntiRecoilProfilesHelp,
        };
        AntiRecoil.Children.Add(profilesHelpLabel);

        var profileList = new global::UILibrary.AntiRecoilProfileList { Margin = new Thickness(4) };
        profileList.BindTo(() => AppConfig.Current.AntiRecoilSettings.Profiles);
        AntiRecoil.Children.Add(profileList);

        // Pattern library is reachable from here too — the per-profile editor's pattern picker
        // reads from this library.
        AntiRecoil.AddButton(Locale.RecoilPatternsMenuItem).Reader.Click += (_, _) =>
        {
            new RecoilPatternsDialog { Owner = this }.ShowDialog();
        };

        AntiRecoil.AddSeparator();
        AntiRecoil.Visibility = GetVisibilityFor(nameof(AntiRecoil));

        // Spin up the profile manager (keybind subscriptions + OCR poll). Idempotent.
        PowerAim.AILogic.AntiRecoilProfileManager.EnsureInitialized();

        #endregion Anti Recoil

        #region FOV Config

        FOVConfig.AddTitle(Locale.FOVConfig, true);
        FOVConfig.AddToggleWithKeyBind(Locale.FOV, nameof(Locale.FOV), BindingManager).BindTo(() => AppConfig.Current.ToggleState.FOV).BindActiveStateColor(FOVConfig);
        FOVConfig.AddToggleWithKeyBind(Locale.DynamicFOV, nameof(Locale.DynamicFOV), BindingManager).BindTo(() => AppConfig.Current.ToggleState.DynamicFOV);
        FOVConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind), () => keybind.DynamicFOVKeybind, BindingManager);
        FOVConfig.AddColorChanger(Locale.FOVColor).BindTo(() => AppConfig.Current.ColorState.FOVColor);

        // FOV now sizes the captured screen region itself (PredictionLogic downscales it to the
        // model input before inference), so its ceiling is the screen, not the model resolution.
        // Cap at the primary screen's smaller dimension so the centered capture box always fits.
        var primaryBounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                            ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        int fovMax = Math.Max(10, Math.Min(primaryBounds.Width, primaryBounds.Height));
        var fovSizeSlider = FOVConfig.AddSlider(Locale.FOVSize, Locale.Size, 1, 1, 10, fovMax);
        fovSizeSlider.BindTo(() => AppConfig.Current.SliderSettings.FOVSize);
        var dynFovSizeSlider = FOVConfig.AddSlider(Locale.DynamicFOVSize, Locale.Size, 1, 1, 10, fovMax);
        dynFovSizeSlider.BindTo(() => AppConfig.Current.SliderSettings.DynamicFOVSize);
        // FOV max is screen-based and constant now (FOV sizes the capture region, not an inner
        // crop of the model input), so it no longer needs to re-cap when ImageSize changes.

        FOVConfig.AddSeparator();
        FOVConfig.Visibility = GetVisibilityFor(nameof(FOVConfig));

        #endregion FOV Config

        #region ESP Config

        ESPConfig.AddTitle(Locale.ESPConfig, true);
        ESPConfig.AddToggleWithKeyBind(Locale.ShowDetectedPlayer, nameof(Locale.ShowDetectedPlayer), BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowDetectedPlayer).BindActiveStateColor(ESPConfig);
        ESPConfig.AddToggleWithKeyBind(Locale.ShowTriggerHeadArea, nameof(Locale.ShowTriggerHeadArea), BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowTriggerHeadArea);
        ESPConfig.AddToggleWithKeyBind(Locale.ShowAIConfidence, nameof(Locale.ShowAIConfidence), BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowAIConfidence);
        ESPConfig.AddToggleWithKeyBind(Locale.ShowTracers, nameof(Locale.ShowTracers), BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowTracers);
        var sizeToggle = ESPConfig.AddToggleWithKeyBind(Locale.ShowSizes, nameof(Locale.ShowSizes), BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowSizes);

        NoticeBar w1 = null;
        NoticeBar w2 = null;
        ESPConfig.AddDropdown(Locale.DrawingMethod, AppConfig.Current.DropdownState.OverlayDrawingMethod, v =>
        {
            AppConfig.Current.DropdownState.OverlayDrawingMethod = v;
            sizeToggle.SetEnabled(v != OverlayDrawingMethod.WpfWindowCanvas);
            if (v == OverlayDrawingMethod.DesktopDC)
            {
                w1 = new NoticeBar(Locale.DrawingMethodWarning, 8000);
                w1.Closed += (s, e) => w1 = null;
                w1.Show();
                if (AppConfig.Current.ToggleState.GlobalActive && AppConfig.Current.ToggleState.ShowDetectedPlayer)
                {
                    AppConfig.Current.ToggleState.GlobalActive = false;
                    w2 = new NoticeBar(Locale.DisabledActiveStateForSafety, 10000);
                    w2.Closed += (s, e) => w2 = null;
                    w2.Show();
                }
            }
            else
            {
                w1?.Close();
                w2?.Close();
            }
        });

        ESPConfig.AddColorChanger(Locale.DetectedPlayerColor).BindTo(() => AppConfig.Current.ColorState.DetectedPlayerColor);


        ESPConfig.AddSlider(Locale.AIConfidenceFontSize, Locale.Size, 1, 1, 1, 30).BindTo(() => AppConfig.Current.SliderSettings.AIConfidenceFontSize);

        ESPConfig.AddSlider(Locale.CornerRadius, Locale.Radius, 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.CornerRadius);

        ESPConfig.AddSlider(Locale.BorderThickness, Locale.Thickness, 0.1, 1, 0.1, 10).BindTo(() => AppConfig.Current.SliderSettings.BorderThickness);

        ESPConfig.AddSeparator();
        ESPConfig.Visibility = GetVisibilityFor(nameof(ESPConfig));

        #endregion ESP Config
    }

    private void LoadGamepadSettingsMenu()
    {
        void Reload()
        {
            if (_uiCreated)
            {
                _uiCreated = false;
                LoadGamepadSettingsMenu();
                _uiCreated = true;
            }
        }

        string error = "";
        try
        {
            GamepadManager.Init();
        }
        catch (Exception e)
        {
            error = e.Message;
        }
        if (!string.IsNullOrWhiteSpace(error) || !GamepadManager.CanSend)
            ButtonGamepadSettings.Foreground = Brushes.Red;
        else
            ButtonGamepadSettings.ClearValue(ForegroundProperty);
        GamepadSettingsConfig.RemoveAll();
        GamepadSettingsConfig.AddTitle(Locale.GamepadSettings, false);

        GamepadSettingsConfig.AddDropdown(Locale.GamepadSendCommandMode, AppConfig.Current.DropdownState.GamepadSendMode, v =>
        {
            AppConfig.Current.DropdownState.GamepadSendMode = v;
            Reload();
        });

        if (!string.IsNullOrEmpty(error))
        {
            GamepadSettingsConfig.AddCredit(Locale.Status,
                Locale.Error + ": " + error, credit => credit.Description.Foreground = Brushes.Red);
        }

        if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.None)
        {
            GamepadSettingsConfig.AddCredit(Locale.None, Locale.GamepadModeNoneInfo);
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.ViGEm)
        {
            GamepadSettingsConfig.AddCredit("", Locale.ViGEmInfoText.FormatWith(Environment.NewLine));
            if (!GamepadManager.CanSend)
            {
                GamepadSettingsConfig.AddButton(Locale.GotoVigembusdriver, b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://vigembusdriver.com",
                            UseShellExecute = true
                        });
                    };
                });
            }
            else
            {
                GamepadSettingsConfig.AddCredit(Locale.Status, $"{Locale.Great.ToUpper()}, {Locale.GamepadDriverSuccessMessage}");
            }
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.VJoy)
        {
            GamepadSettingsConfig.AddCredit("", Locale.vJoyInfoInfoText.FormatWith(Environment.NewLine));
            if (!GamepadManager.CanSend)
            {
                GamepadSettingsConfig.AddButton(Locale.InstallVJoy, b =>
                {
                    b.Reader.Click += (s, e) => WindowsHelper.RunResourceTool("vJoySetup.exe", null, _ => Reload());
                });
            }
            else
            {
                GamepadSettingsConfig.AddCredit(Locale.Status, $"{Locale.Great.ToUpper()}, {Locale.GamepadDriverSuccessMessage}");
            }
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.XInputHook)
        {
            GamepadSettingsConfig.AddCredit(Locale.Notice, Locale.XInputEmulationInfo);
            GamepadSettingsConfig.Add<AProcessPicker>(picker =>
            {
                picker.SelectedProcessModel = new ProcessModel { Title = AppConfig.Current.DropdownState.GamepadProcess };
                picker.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(picker.SelectedProcessModel))
                    {
                        AppConfig.Current.DropdownState.GamepadProcess = picker.SelectedProcessModel.Title;
                        Reload();
                    }
                };
            });
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.Internal)
        {
            GamepadSettingsConfig.AddCredit(Locale.InternalModeTitle, Locale.InternalModeInfo);
            if (GamepadManager.CanSend)
            {
                GamepadSettingsConfig.AddCredit(Locale.Status, $"{Locale.Great.ToUpper()}, {Locale.GamepadDriverSuccessMessage}");
            }
            else
            {
                GamepadSettingsConfig.AddCredit(Locale.Status, Locale.InternalModeInitialized, credit => credit.Description.Foreground = Brushes.Orange);
            }
        }

        // Inline live diagnostics — replaces the earlier modal dialog. Stays on the Gamepad page
        // so the user can read the slot map while tweaking other settings on the same screen.
        GamepadSettingsConfig.Children.Add(new PowerAim.UILibrary.GamepadDiagnosticsPanel
        {
            Margin = new Thickness(0, 8, 0, 8),
        });

        GamepadSettingsConfig.AddSeparator();

        if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.VJoy ||
            AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.ViGEm ||
            AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.Internal)
        {
            GamepadSettingsConfig.AddTitle(Locale.HidePhysicalController, false);
            GamepadSettingsConfig.AddCredit(Locale.HIDHideHeader, Locale.HIDHideSubHeader);

            GamepadSettingsConfig.AddToggle(Locale.AutoHideControllerText, toggle =>
            {
                toggle.IsEnabled = File.Exists(HidHideHelper.GetHidHidePath());
                toggle.Changed += (s, e) => Reload();
            }).BindTo(() => AppConfig.Current.ToggleState.AutoHideController);

            GamepadSettingsConfig.AddFileLocator(Locale.HIDHidePath, "HidHideCLI.exe (HidHideCLI.exe)|HidHideCLI.exe", HidHideHelper.GetHidHidePath(), cfg:
                locator =>
                {
                    locator.FileSelected += (sender, args) => Reload();
                });

            // Install button stays here because it triggers a heavy installer; the run-time
            // buttons (Launch HidHide UI / Open Gamepad Tester / joy.cpl) moved into the diagnostic
            // panel above so all live actions sit in one place.
            if (!File.Exists(HidHideHelper.GetHidHidePath()))
            {
                GamepadSettingsConfig.AddButton(Locale.InstallHidHide, b =>
                {
                    b.Reader.Click += (s, e) => WindowsHelper.RunResourceTool("HidHide_1.5.230_x64.exe", null, _ => Reload());
                });
            }
        }
    }

    /// <summary>Helper used by <see cref="UILibrary.GamepadDiagnosticsPanel"/> to jump to the gamepad tester page.</summary>
    public void OpenGamepadTester() => _ = NavigateTo(nameof(GamepadTestPage));

    /// <summary>Helper used by <see cref="UILibrary.GamepadDiagnosticsPanel"/> to jump to the hidden-controllers page.</summary>
    public void OpenHiddenControllersPage() => _ = NavigateTo(nameof(HiddenControllersPage));

    private void BackToGamepadSettings_Click(object sender, RoutedEventArgs e) => _ = NavigateTo(nameof(GamepadSettings));

    private void LoadTools()
    {
        ToolsConfig.RemoveAll();
        ToolsConfig.AddTitle(Locale.Magnifier, true);
        ToolsConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.MagnifierKeybind), () => AppConfig.Current.BindingSettings.MagnifierKeybind, BindingManager);

        ToolsConfig.AddButton(Locale.ToggleMagnifier).Reader.Click += (_, _) => ToggleMagnifier();

        ToolsConfig.AddSlider(Locale.MagnificationValue, Locale.ZoomFactor, 0.1, 0.1, ApplicationConstants.MinMagnificationFactor, ApplicationConstants.MaxMagnificationFactor).BindTo(() => AppConfig.Current.SliderSettings.MagnificationFactor);
        ToolsConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.MagnifierZoomInKeybind), () => AppConfig.Current.BindingSettings.MagnifierZoomInKeybind, BindingManager);
        ToolsConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind), () => AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind, BindingManager);

        ToolsConfig.AddSlider(Locale.ZoomStep, Locale.Step, 0.1, 0.1, 0.1, 4).BindTo(() => AppConfig.Current.SliderSettings.MagnificationStepFactor);

        ToolsConfig.AddSlider(Locale.WindowSizeWidth, Locale.Width, 1, 10, 50, 1500).BindTo(() => AppConfig.Current.SliderSettings.MagnifierWindowWidth);
        ToolsConfig.AddSlider(Locale.WindowSizeHeight, Locale.Height, 1, 10, 50, 1500).BindTo(() => AppConfig.Current.SliderSettings.MagnifierWindowHeight);


        ToolsConfig.AddSeparator();

        ToolsConfig.AddTitle(Locale.HwidSpoofer, false);

        ToolsConfig.AddButton(Locale.OpenHwidSpoofer).Reader.Click += (_, _) => OpenSpoofer();
        ToolsConfig.AddCredit("", Locale.HwidSpooferHelp);

        ToolsConfig.AddSeparator();

        BuildSettingsExtras();
    }

    // Tracked across rebuilds: CreateUI() re-runs on every config load / language change, so we
    // tear down the previous stats timer + event subscriptions instead of stacking duplicates.
    private System.Windows.Threading.DispatcherTimer? _statsTimer;
    private System.ComponentModel.PropertyChangedEventHandler? _replayStatusHandler;
    private System.ComponentModel.PropertyChangedEventHandler? _learnStatusHandler;

    /// <summary>
    ///     Builds the new Settings-page cards added by the feature batch:
    ///     <list type="bullet">
    ///       <item>Active Processes — Auto-Pause + per-game profile switching</item>
    ///       <item>Overlays — Debug + Crosshair overlay toggles &amp; crosshair config</item>
    ///       <item>Stats — live session metrics + reset</item>
    ///     </list>
    /// </summary>
    private void BuildSettingsExtras()
    {
        // CreateUI() re-runs on every config load / language change. Clear these cards first so we
        // rebuild instead of appending duplicates, and tear down the previous stats timer + event
        // subscriptions (otherwise each language switch stacks another timer / handler on orphaned
        // controls).
        ActiveProcessesSettings.RemoveAll();
        OverlaySettings.RemoveAll();
        StatsCard.RemoveAll();
        HudOcrCard.RemoveAll();
        ReplayCard.RemoveAll();
        LearningCard.RemoveAll();

        _statsTimer?.Stop();
        if (_replayStatusHandler != null)
            PowerAim.AILogic.ReplayBuffer.Instance.PropertyChanged -= _replayStatusHandler;
        if (_learnStatusHandler != null)
            PowerAim.AILogic.AutoPlayLearningModel.Instance.PropertyChanged -= _learnStatusHandler;

        // ===== Active Processes (Auto-Pause + per-game profile switch) =====
        ActiveProcessesSettings.AddTitle(Locale.ActiveProcesses, true);
        ActiveProcessesSettings.AddToggle(Locale.AutoPauseOnFocusLoss)
            .InitWith(t => t.ToolTip = Locale.AutoPauseOnFocusLossTooltip)
            .BindTo(() => AppConfig.Current.ActiveProcessSettings.AutoPauseOnFocusLoss);
        ActiveProcessesSettings.AddToggle(Locale.AutoSwitchProfile)
            .InitWith(t => t.ToolTip = Locale.AutoSwitchProfileTooltip)
            .BindTo(() => AppConfig.Current.ActiveProcessSettings.AutoSwitchProfile);
        ActiveProcessesSettings.AddCredit("", Locale.MatchPatternHelp);

        // Inline editor for the optional "game whitelist". Comma-separated text-box is the
        // simplest path that round-trips cleanly through the ObservableCollection<string>.
        var whitelistInput = new System.Windows.Controls.TextBox
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            MinHeight = 32,
            Margin = new Thickness(2, 4, 2, 4),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            ToolTip = Locale.GameProcessPatternsTooltip,
        };
        whitelistInput.Text = string.Join(", ", AppConfig.Current.ActiveProcessSettings.GameProcessPatterns);
        whitelistInput.LostFocus += (_, _) =>
        {
            var list = AppConfig.Current.ActiveProcessSettings.GameProcessPatterns;
            list.Clear();
            foreach (var part in whitelistInput.Text.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                list.Add(part);
        };
        ActiveProcessesSettings.Children.Add(whitelistInput);
        ActiveProcessesSettings.AddSeparator();


        // ===== Overlays (Debug + Crosshair) =====
        OverlaySettings.AddTitle(Locale.Overlays, true);
        OverlaySettings.AddToggleWithKeyBind(Locale.ShowDebugOverlay, nameof(Locale.ShowDebugOverlay), BindingManager)
            .InitWith(t => t.ToolTip = Locale.ShowDebugOverlayTooltip)
            .BindTo(() => AppConfig.Current.ToggleState.ShowDebugOverlay);
        OverlaySettings.AddToggleWithKeyBind(Locale.ShowInputVisualizer, nameof(Locale.ShowInputVisualizer), BindingManager) 
            .InitWith(t => t.ToolTip = Locale.ShowInputVisualizerTooltip)
            .BindTo(() => AppConfig.Current.ToggleState.ShowInputVisualizer);


        OverlaySettings.AddToggleWithKeyBind(Locale.ShowCustomCrosshair, nameof(Locale.ShowCustomCrosshair), BindingManager)
            .InitWith(t => t.ToolTip = Locale.ShowCustomCrosshairTooltip)
            .BindTo(() => AppConfig.Current.ToggleState.ShowCrosshairOverlay);

        OverlaySettings.AddDropdown(Locale.CrosshairShape,
            AppConfig.Current.CrosshairSettings.Shape,
            v => AppConfig.Current.CrosshairSettings.Shape = v);
        OverlaySettings.AddSlider(Locale.CrosshairSize, Locale.Pixels, 1, 1, 4, 80)
            .BindTo(() => AppConfig.Current.CrosshairSettings.Size);
        OverlaySettings.AddSlider(Locale.CrosshairThickness, Locale.Pixels, 1, 1, 1, 10)
            .BindTo(() => AppConfig.Current.CrosshairSettings.Thickness);
        OverlaySettings.AddSlider(Locale.CrosshairGap, Locale.Pixels, 1, 1, 0, 30)
            .BindTo(() => AppConfig.Current.CrosshairSettings.Gap);
        OverlaySettings.AddSlider(Locale.CrosshairOutline, Locale.Pixels, 1, 1, 0, 4)
            .BindTo(() => AppConfig.Current.CrosshairSettings.OutlineThickness);
        OverlaySettings.AddColorChanger(Locale.CrosshairColor).BindTo(() => AppConfig.Current.CrosshairSettings.ColorValue);
        OverlaySettings.AddColorChanger(Locale.CrosshairOutlineColor).BindTo(() => AppConfig.Current.CrosshairSettings.OutlineColorValue);

        // Detection-flash cue: tints the crosshair briefly whenever the detector finds a target.
        // The dependent picker + duration slider hide when the toggle is off — matches the
        // conditional-UI pattern used in the Anti-Recoil section.
        var detectionFlashToggle = OverlaySettings.AddToggle(Locale.DetectionFlash)
            .InitWith(t => t.ToolTip = Locale.DetectionFlashHelp)
            .BindTo(() => AppConfig.Current.CrosshairSettings.DetectionFlashEnabled);
        var detectionFlashColor = OverlaySettings.AddColorChanger(Locale.DetectionFlashColor);
        detectionFlashColor.BindTo(() => AppConfig.Current.CrosshairSettings.DetectionFlashColorValue);
        var detectionFlashDuration = OverlaySettings.AddSlider(Locale.DetectionFlashDuration, Locale.Milliseconds, 10, 10, 50, 1000)
            .BindTo(() => AppConfig.Current.CrosshairSettings.DetectionFlashMs);
        void UpdateDetectionFlashVisibility()
        {
            var on = AppConfig.Current.CrosshairSettings.DetectionFlashEnabled;
            detectionFlashColor.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            detectionFlashDuration.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }
        AppConfig.Current.CrosshairSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CrosshairSettings.DetectionFlashEnabled))
                Dispatcher.Invoke(UpdateDetectionFlashVisibility);
        };
        UpdateDetectionFlashVisibility();
        OverlaySettings.AddSeparator();

        // ===== Stats =====
        StatsCard.AddTitle(Locale.SessionStats, true);
        var fpsLabel       = new System.Windows.Controls.Label { Padding = new Thickness(0) };
        var msLabel        = new System.Windows.Controls.Label { Padding = new Thickness(0) };
        var detLabel       = new System.Windows.Controls.Label { Padding = new Thickness(0) };
        var shotsLabel     = new System.Windows.Controls.Label { Padding = new Thickness(0) };
        var framesLabel    = new System.Windows.Controls.Label { Padding = new Thickness(0) };
        var tacticalLabel  = new System.Windows.Controls.Label { Padding = new Thickness(0) };
        var durationLabel  = new System.Windows.Controls.Label { Padding = new Thickness(0) };

        foreach (var l in new[] { fpsLabel, msLabel, detLabel, shotsLabel, framesLabel, tacticalLabel, durationLabel })
        {
            l.Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary");
            l.FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono,Consolas");
            l.FontSize = 12;
            l.Margin = new Thickness(2, 1, 2, 1);
            StatsCard.Children.Add(l);
        }

        _statsTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _statsTimer.Tick += (_, _) =>
        {
            var s = PowerAim.Class.SessionStats.Instance;
            fpsLabel.Content      = $"{Locale.StatFpsLabel,-19} {s.InstantFps:0.0}";
            msLabel.Content       = $"{Locale.StatInferenceTime,-19} {s.LastInferenceMs:0.0} ms";
            detLabel.Content      = $"{Locale.StatDetectionsLast,-19} {s.LastDetectionCount}";
            shotsLabel.Content    = $"{Locale.StatShotsFired,-19} {s.ShotsFired}";
            framesLabel.Content   = $"{Locale.StatFramesProcessed,-19} {s.FramesProcessed}";
            tacticalLabel.Content = $"{Locale.StatTacticalActions,-19} {s.TacticalActionsUsed}";
            durationLabel.Content = $"{Locale.StatSession,-19} {s.Duration:hh\\:mm\\:ss}";
        };
        _statsTimer.Start();

        StatsCard.AddButton(Locale.ResetStats).Reader.Click += (_, _) => PowerAim.Class.SessionStats.Instance.Reset();
        StatsCard.AddToggle(Locale.AdaptiveKalmanLead)
            .InitWith(t => t.ToolTip = Locale.AdaptiveKalmanLeadTooltip)
            .Checked = PowerAim.AILogic.PredictionSettings.AdaptiveKalmanLead;
        StatsCard.AddSeparator();

        // ===== HUD OCR =====
        HudOcrCard.AddTitle(Locale.HudOcr, true);
        HudOcrCard.AddToggleWithKeyBind(Locale.EnableHudOcr, nameof(Locale.EnableHudOcr), BindingManager)
            .InitWith(t => t.ToolTip = Locale.EnableHudOcrTooltip)
            .BindTo(() => AppConfig.Current.OcrSettings.Enabled);
        HudOcrCard.AddSlider(Locale.OcrInterval, Locale.MillisecondsShort, 50, 50, 100, 5000, true)
            .BindTo(() => AppConfig.Current.OcrSettings.IntervalMs);
        HudOcrCard.AddToggleWithKeyBind(Locale.ShowOcrRegions, nameof(Locale.ShowOcrRegions), BindingManager)
            .InitWith(t => t.ToolTip = Locale.ShowOcrRegionsTooltip)
            .BindTo(() => AppConfig.Current.ToggleState.ShowOcrRegionsOverlay);
        HudOcrCard.AddButton(Locale.ConfigureOcrRegions).Reader.Click += (_, _) =>
        {
            new OcrRegionsDialog { Owner = this }.ShowDialog();
        };
        // Aim-disengage button was here historically because it edits OCR-driven rules. It now
        // lives under AimConfig (see below) since semantically it's an aim-side feature — OCR is
        // just the input. The relocation makes it discoverable from the section that already
        // hosts sensitivity / sticky-aim / calibration.
        HudOcrCard.AddSeparator();

        // ===== Replay Buffer =====
        ReplayCard.AddTitle(Locale.ReplayBuffer, true);
        ReplayCard.AddToggle(Locale.RecordRollingBuffer)
            .InitWith(t => t.ToolTip = Locale.RecordRollingBufferTooltip)
            .BindTo(() => AppConfig.Current.ReplaySettings.Enabled);
        ReplayCard.AddSlider(Locale.BufferLength, Locale.SecondsShort, 1, 1, 1, 30, true)
            .BindTo(() => AppConfig.Current.ReplaySettings.BufferSeconds);
        ReplayCard.AddSlider(Locale.JpegQuality, "", 1, 5, 10, 100, true)
            .BindTo(() => AppConfig.Current.ReplaySettings.JpegQuality);

        var replayStatus = new System.Windows.Controls.Label
        {
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono,Consolas"),
            FontSize = 12,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 4, 2, 4),
            Content = Locale.FramesBufferedZero
        };
        ReplayCard.Children.Add(replayStatus);
        _replayStatusHandler = (_, _) =>
            Dispatcher.BeginInvoke(new Action(() =>
                replayStatus.Content = Locale.FramesBufferedFormat.FormatWith(PowerAim.AILogic.ReplayBuffer.Instance.FrameCount)));
        PowerAim.AILogic.ReplayBuffer.Instance.PropertyChanged += _replayStatusHandler;

        ReplayCard.AddButton(Locale.SaveReplayBuffer).Reader.Click += async (_, _) =>
        {
            replayStatus.Content = Locale.Exporting;
            var folder = await PowerAim.AILogic.ReplayBuffer.Instance.ExportAsync();
            replayStatus.Content = folder is null
                ? Locale.NothingToExportEmpty
                : Locale.SavedToFormat.FormatWith(folder);
        };
        ReplayCard.AddButton(Locale.ClearBuffer).Reader.Click += (_, _) =>
            PowerAim.AILogic.ReplayBuffer.Instance.Clear();
        ReplayCard.AddSeparator();

        // ===== AutoPlay learning =====
        LearningCard.AddTitle(Locale.AutoPlayLearning, true);
        LearningCard.AddToggle(Locale.RecordPlaystyle)
            .InitWith(t => t.ToolTip = Locale.RecordPlaystyleTooltip)
            .BindTo(() => AppConfig.Current.AutoPlayLearningSettings.Recording);
        LearningCard.AddToggle(Locale.ApplyLearnedBias)
            .InitWith(t => t.ToolTip = Locale.ApplyLearnedBiasTooltip)
            .BindTo(() => AppConfig.Current.AutoPlayLearningSettings.ApplyModel);
        LearningCard.AddSlider(Locale.BiasStrength, "", 0.01, 0.01, 0, 1)
            .BindTo(() => AppConfig.Current.AutoPlayLearningSettings.BiasStrength);
        LearningCard.AddSlider(Locale.SampleInterval, Locale.MillisecondsShort, 50, 50, 50, 1000, true)
            .BindTo(() => AppConfig.Current.AutoPlayLearningSettings.SampleIntervalMs);

        var learnStatus = new System.Windows.Controls.Label
        {
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono,Consolas"),
            FontSize = 12,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 4, 2, 4)
        };
        UpdateLearnStatus(learnStatus);
        LearningCard.Children.Add(learnStatus);
        _learnStatusHandler = (_, _) =>
            Dispatcher.BeginInvoke(new Action(() => UpdateLearnStatus(learnStatus)));
        PowerAim.AILogic.AutoPlayLearningModel.Instance.PropertyChanged += _learnStatusHandler;

        LearningCard.AddButton(Locale.SaveModel).Reader.Click += (_, _) =>
        {
            try
            {
                PowerAim.AILogic.AutoPlayLearningModel.Instance.Save(AppConfig.Current?.AutoPlayLearningSettings?.ModelPath);
                learnStatus.Content = Locale.SavedSamplesFormat.FormatWith(PowerAim.AILogic.AutoPlayLearningModel.Instance.TotalSamples);
            }
            catch (Exception ex) { learnStatus.Content = Locale.SaveFailedFormat.FormatWith(ex.Message); }
        };
        LearningCard.AddButton(Locale.LoadModel).Reader.Click += (_, _) =>
        {
            bool ok = PowerAim.AILogic.AutoPlayLearningModel.Instance.Load(AppConfig.Current?.AutoPlayLearningSettings?.ModelPath);
            learnStatus.Content = ok ? Locale.ModelLoaded : Locale.NoModelFileAtPath;
            UpdateLearnStatus(learnStatus);
        };
        LearningCard.AddButton(Locale.ClearModel).Reader.Click += (_, _) =>
        {
            PowerAim.AILogic.AutoPlayLearningModel.Instance.Clear();
            UpdateLearnStatus(learnStatus);
        };
        LearningCard.AddSeparator();

        // Apply current state of overlays in case the config was loaded with them already enabled.
        if (AppConfig.Current.ToggleState.ShowDebugOverlay)
            DebugOverlay.ShowOrHide(true);
        if (AppConfig.Current.ToggleState.ShowCrosshairOverlay)
            CrosshairOverlay.ShowOrHide(true);
        if (AppConfig.Current.ToggleState.ShowOcrRegionsOverlay)
            OcrRegionsOverlay.ShowOrHide(true);
    }

    private static void UpdateLearnStatus(System.Windows.Controls.Label label)
    {
        var model = PowerAim.AILogic.AutoPlayLearningModel.Instance;
        label.Content = string.Format(Locale.LearningStatusFormat, model.TotalSamples, model.StateCount);
    }

    private void OpenSpoofer()
    {
        WindowsHelper.RunResourceToolAsAdmin("SecHex-GUI.exe", DefenderExclusionType.Folder, () => Topmost = false, _ => Topmost = AppConfig.Current.ToggleState.UITopMost);
    }


    private void ValidateMagnificationFactor()
    {
        AppConfig.Current.SliderSettings.MagnificationFactor =
            AppConfig.Current.SliderSettings.MagnificationFactor switch
            {
                < ApplicationConstants.MinMagnificationFactor => ApplicationConstants.MinMagnificationFactor,
                > ApplicationConstants.MaxMagnificationFactor => ApplicationConstants.MaxMagnificationFactor,
                _ => AppConfig.Current.SliderSettings.MagnificationFactor
            };
    }


    private void LoadAutoPlayMenu()
    {
        AutoPlayConfig.RemoveAll();
        AutoPlayProfiles.RemoveAll();

        // Ollama Status Section
        AutoPlayConfig.AddTitle(Locale.AutoPlayMenuTitle, true);
        AutoPlayConfig.Add<OllamaStatusIndicator>();

        // AutoPlay Toggle
        AutoPlayConfig.AddToggleWithKeyBind(Locale.AutoPlay, nameof(Locale.AutoPlay), BindingManager, toggle =>
        {
            toggle.ToolTip = Locale.AutoPlayToggleTooltip;
        }).BindTo(() => AppConfig.Current.ToggleState.AutoPlay);

        // Ollama Settings
        AutoPlayConfig.AddTitle(Locale.OllamaSettings);
        AutoPlayConfig.AddSlider(Locale.RequestTimeout, Locale.Seconds, 1, 5, 5, 120).BindTo(() => AppConfig.Current.OllamaSettings.TimeoutSeconds);
        AutoPlayConfig.AddSlider(Locale.Temperature, "", 0.1, 0.1, 0.0, 1.0).BindTo(() => AppConfig.Current.OllamaSettings.Temperature);
        AutoPlayConfig.AddSlider(Locale.ImageMaxSize, Locale.Pixels, 64, 128, 256, 1024).BindTo(() => AppConfig.Current.OllamaSettings.ImageMaxSize);
        AutoPlayConfig.AddSlider(Locale.ImageQuality, Locale.PercentSign, 5, 10, 30, 100).BindTo(() => AppConfig.Current.OllamaSettings.ImageQuality);

        AutoPlayConfig.AddSeparator();

        // Profiles Section
        AutoPlayProfiles.AddTitle(Locale.AutoPlayProfiles, true);
        AutoPlayProfiles.Add<AutoPlayProfileList>().BindTo(() => AppConfig.Current.AutoPlayProfiles);

        AutoPlayProfiles.AddSeparator();

        // Help Section
        AutoPlayProfiles.AddTitle(Locale.QuickStart);
        AutoPlayProfiles.AddCredit(Locale.QuickStartStep1Title, Locale.QuickStartStep1Body);
        AutoPlayProfiles.AddCredit(Locale.QuickStartStep2Title, Locale.QuickStartStep2Body);
        AutoPlayProfiles.AddCredit(Locale.QuickStartStep3Title, Locale.QuickStartStep3Body);
        AutoPlayProfiles.AddCredit(Locale.QuickStartStep4Title, Locale.QuickStartStep4Body);
        AutoPlayProfiles.AddCredit(Locale.QuickStartStep5Title, Locale.QuickStartStep5Body);

        AutoPlayProfiles.AddSeparator();
    }

    private void LoadSettingsMenu()
    {

        UISettings.RemoveAll();
        CaptureSettings.RemoveAll();
        InputSettings.RemoveAll();

        InputSettings.AddTitle(Locale.InputSettings, true);
        UISettings.AddTitle(Locale.UISettings, true);
        CaptureSettings.AddTitle(Locale.CaptureSettings, true);


        var text = ApplicationConstants.IsCudaBuild ? Locale.SwitchToDirectML : Locale.SwitchToCuda;
        UISettings.AddButton(text,
            button =>
            {
                button.Reader.Click += async (s, e) =>
                {
                    var releaseManager = new GithubManager();
                    var task = releaseManager.GetAvailableReleasesAsync(Constants.RepoOwner, Constants.RepoName);
                    var releases = await task;
                    var release = releases.FirstOrDefault(x => Version.Parse(x.TagName) == ApplicationConstants.ApplicationVersion);
                    if (release is not null)
                    {
                        var manager = new UpdateManager();
                        manager.SetRelease(release, !ApplicationConstants.IsCudaBuild);
                        var dialog = new UpdateDialog(manager);
                        dialog.ApplyButton.Content = text;
                        dialog.TitleLabel.Text = text;
                        dialog.HeaderLabel.Visibility = Visibility.Collapsed;
                        dialog.Height = 170;
                        dialog.ReleaseInfo.Visibility = Visibility.Collapsed;
                        dialog.Owner = this;
                        dialog.ShowDialog();
                    }
                };
            });
        UISettings.AddDropdown(Locale.Language, CultureInfo.CurrentUICulture, Cultures.All, culture =>
        {
            if (_uiCreated)
            {
                AppConfig.Current.Language = culture.Name;
                CultureInfo.CurrentUICulture = culture;
                OnPropertyChanged(nameof(Texts));
                CreateUI();
            }
        }, toStringFn: info => info.EnglishName);

        // Accent colour is freely pickable now; the named palettes survive only as quick-fill swatches.
        var accentSwatches = ThemePalette.ByMode(false).Select(p => p.AccentColor).ToArray();

        var accentPicker = UISettings.AddColorChanger(Locale.AccentColor);
        accentPicker.BindTo(() => AppConfig.Current.AccentColorValue);
        accentPicker.SetSwatches(accentSwatches);
        accentPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UILibrary.AColorChanger.Color))
                PowerAim.Theme.ThemeManager.Apply();
        };

        var activeAccentPicker = UISettings.AddColorChanger(Locale.AccentColorWhenActive);
        activeAccentPicker.BindTo(() => AppConfig.Current.ActiveAccentColorValue);
        activeAccentPicker.SetSwatches(accentSwatches);
        activeAccentPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UILibrary.AColorChanger.Color))
                PowerAim.Theme.ThemeManager.Apply();
        };

        UISettings.AddDropdown<AppThemeMode>(Locale.ThemeMode, AppConfig.Current.ThemeMode, mode =>
        {
            if (Config is not null)
                Config.ThemeMode = mode;
            PowerAim.Theme.ThemeManager.Apply();
        });

        UISettings.AddToggle(Locale.UITopMost).BindTo(() => AppConfig.Current.ToggleState.UITopMost);
        UISettings.AddToggle(Locale.ShowHelpTexts).BindTo(() => AppConfig.Current.ToggleState.ShowHelpTexts);
        UISettings.AddToggle(Locale.ShowToggleNotifications)
            .InitWith(t => t.ToolTip = Locale.ShowToggleNotificationsTooltip)
            .BindTo(() => AppConfig.Current.ToggleState.ShowToggleNotifications);
        UISettings.AddToggle(Locale.RequireGlobalActiveForKeybinds)
            .InitWith(t => t.ToolTip = Locale.RequireGlobalActiveForKeybindsTooltip)
            .BindTo(() => AppConfig.Current.ToggleState.RequireGlobalActiveForKeybinds);

        var hideCaptureToggle = UISettings.AddToggle(Locale.HideUIFromCapture);
        hideCaptureToggle.BindTo(() => AppConfig.Current.ToggleState.HideUIFromCapture);
        hideCaptureToggle.Changed += (s, e) =>
        {
            if (!e.Value)
            {
                var result = MessageDialog.Show(
                    Locale.DisableCaptureProtectionWarning,
                    Locale.DisableCaptureProtectionTitle,
                    MessageDialog.DialogButtons.YesNo,
                    MessageDialog.DialogIcon.Warning,
                    owner: this,
                    defaultResult: MessageDialog.DialogResult.No);
                if (result != MessageDialog.DialogResult.Yes)
                {
                    AppConfig.Current.ToggleState.HideUIFromCapture = true;
                    hideCaptureToggle.Checked = true;
                    return;
                }
            }
            PowerAim.Class.Native.NativeAPIMethods.ApplyCaptureExclusionToAllWindows();
            AppConfig.Current.Save();
        };
        UISettings.AddSeparator();

        CaptureSettings.AddToggle(Locale.CollectDataWhilePlaying).BindTo(() => AppConfig.Current.ToggleState.CollectDataWhilePlaying);
        CaptureSettings.AddToggle(Locale.AutoLabelData).BindTo(() => AppConfig.Current.ToggleState.AutoLabelData);


        CaptureSettings.AddSlider(Locale.AIMinimumConfidence, $"{Locale.PercentSign} {Locale.Confidence}", 1, 1, 1, 100).BindTo(() => AppConfig.Current.SliderSettings.AIMinimumConfidence).Slider.PreviewMouseLeftButtonUp += (sender, e) =>
        {
            switch (AppConfig.Current.SliderSettings.AIMinimumConfidence)
            {
                case >= 95:
                    new NoticeBar(Locale.MaxConfidenceWarning, 10000).Show();
                    break;
                case <= 35:
                    new NoticeBar(Locale.MinConfidenceWarning, 10000).Show();
                    break;
            }
        };
        CaptureSettings.AddToggleWithKeyBind(Locale.EnsureCapturedProcessForeground, nameof(Locale.EnsureCapturedProcessForeground), BindingManager).BindTo(() => AppConfig.Current.ToggleState.EnsureCaptureForeground);
        CaptureSettings.AddToggleWithKeyBind(Locale.ShowCapturedArea, nameof(Locale.ShowCapturedArea), BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowCapturedArea);
        CaptureSettings.AddColorChanger(Locale.CapturedAreaBorderColor).BindTo(() => AppConfig.Current.ColorState.CapturedAreaBorderColor);
        CaptureSettings.AddSeparator();

        // Movement Method dropdown — custom-built so we can:
        //   • render a device icon (mouse 🖱 / gamepad 🎮) next to each entry, matching the
        //     AKeyChanger device-icon convention,
        //   • disable the "Gamepad" entry when no working virtual gamepad sender exists, so the
        //     user can't pick a non-functional option.
        // Replaces both the old generic-enum dropdown AND the standalone "Use controller for aim"
        // toggle — those two were redundant.
        var movementDropdown = new global::UILibrary.ADropdown(Locale.MouseMovementMethod);
        InputSettings.Add(movementDropdown);

        foreach (var v in Enum.GetValues<MouseMovementMethod>())
        {
            // Glyphs match AKeyChanger.SetContent: mouse = U+F8AF, gamepad = U+E7FC.
            string glyph = v == MouseMovementMethod.Gamepad ? "" : "";
            string label = v.ToDescriptionString();
            bool isGamepad = v == MouseMovementMethod.Gamepad;

            var item = new System.Windows.Controls.ComboBoxItem();
            var stack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = glyph,
                FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 13,
                Width = 20,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.85,
            });
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
            });
            item.Content = stack;
            if (isGamepad)
                item.ToolTip = Locale.UseControllerForAimTooltip;

            // Capture by closure — we copy `v` into a local so the inner lambda binds correctly.
            var local = v;
            item.Selected += async (_, _) =>
            {
                // Gamepad pick + nothing connected → warn the user and offer to navigate to the
                // Gamepad page so they can wire it up. We DO commit the selection either way so
                // their intent survives the navigation; once a sender comes online,
                // InputSender.GamepadAimActive flips true automatically.
                if (local == MouseMovementMethod.Gamepad
                    && !PowerAim.InputLogic.InputSender.GamepadAimAvailable)
                {
                    var res = MessageDialog.Show(
                        Locale.GamepadNotReadyMessage,
                        Locale.GamepadNotReady,
                        MessageDialog.DialogButtons.YesNo,
                        MessageDialog.DialogIcon.Warning,
                        owner: this,
                        defaultResult: MessageDialog.DialogResult.Yes);
                    if (res == MessageDialog.DialogResult.Yes)
                        _ = NavigateTo(nameof(GamepadSettings));
                }

                AppConfig.Current.DropdownState.MouseMovementMethod = local;
                if ((local == MouseMovementMethod.LGHUB && !new LGHubMain().Load())
                    || (local == MouseMovementMethod.RazerSynapse && !await RZMouse.Load())
                    || (local == MouseMovementMethod.ddxoft && !await DdxoftMain.Load()))
                {
                    AppConfig.Current.DropdownState.MouseMovementMethod = MouseMovementMethod.MouseEvent;
                }
            };

            movementDropdown.DropdownBox.Items.Add(item);
            if (v == AppConfig.Current.DropdownState.MouseMovementMethod)
                movementDropdown.DropdownBox.SelectedItem = item;
        }

        InputSettings.AddSlider(Locale.GamepadMinimumLT, "LT", 0.1, 0.1, 0.1, 1).BindTo(() => AppConfig.Current.SliderSettings.GamepadMinimumLT);
        InputSettings.AddSlider(Locale.GamepadMinimumRT, "RT", 0.1, 0.1, 0.1, 1).BindTo(() => AppConfig.Current.SliderSettings.GamepadMinimumRT);

        InputSettings.AddCredit(Locale.FireMaxDelay, Locale.FireDelayInfo.FormatWith(Environment.NewLine));
        InputSettings.AddSlider(Locale.FireMaxDelay, Locale.Seconds, 0.01, 0.1, 0.00, 2).BindTo(() => AppConfig.Current.SliderSettings.FirePressDelay);
        InputSettings.AddSeparator();

    }


    // ============================================================================ ABOUT ====

    private sealed record ReleaseInfo(string Tag, DateTime Published, string HtmlUrl, int TotalDownloads);

    private List<ReleaseInfo>? _cachedReleases;
    private bool _releasesFetchAttempted;
    private bool _aboutPageWired;

    /// <summary>
    ///     Hooks the About page so the releases list gets fetched lazily on first visit (instead
    ///     of paying the API round-trip at startup for users who never open About). Safe to call
    ///     multiple times — idempotent via <see cref="_aboutPageWired"/>.
    /// </summary>
    private void WireAboutPage()
    {
        if (_aboutPageWired) return;
        _aboutPageWired = true;
        if (AboutMenu == null) return;
        AboutMenu.IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) RefreshReleasesAsync();
        };
    }

    /// <summary>
    ///     Re-renders the releases card. Triggers a one-shot API fetch on first call and re-renders
    ///     the cached data on every subsequent call (so language-change rebuilds re-localize the
    ///     "X downloads" / "Open on GitHub" labels without re-hitting the API).
    /// </summary>
    private void RefreshReleasesAsync()
    {
        if (ReleasesPanel == null) return;
        if (_cachedReleases is not null) { BuildReleaseRows(_cachedReleases); return; }
        if (_releasesFetchAttempted) return;
        _releasesFetchAttempted = true;
        ReleasesStatus.Text = Locale.AboutReleasesLoading;
        ReleasesStatus.Visibility = Visibility.Visible;
        _ = LoadReleasesAsync();
    }

    private async Task LoadReleasesAsync()
    {
        try
        {
            // Route through CachingHttpClient: 1-hour disk cache + stale-cache fallback on a
            // rate-limit/network error, so the About → Releases list survives GitHub's anonymous
            // limit instead of showing "too many requests". (Still session-cached via _cachedReleases.)
            using var http = new Core.CachingHttpClient(TimeSpan.FromHours(1));
            // GitHub's API rejects requests without a User-Agent.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PowerAim");
            var json = await http.GetAsync(ApplicationConstants.ReleasesApiUrl);

            var releases = new List<ReleaseInfo>();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                string tag = el.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
                string url = el.TryGetProperty("html_url", out var u) ? (u.GetString() ?? "") : "";
                DateTime published = el.TryGetProperty("published_at", out var p) && DateTime.TryParse(p.GetString(), out var dt)
                    ? dt : DateTime.MinValue;
                int total = 0;
                if (el.TryGetProperty("assets", out var assets) && assets.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var a in assets.EnumerateArray())
                        if (a.TryGetProperty("download_count", out var dc) && dc.TryGetInt32(out var n))
                            total += n;
                releases.Add(new ReleaseInfo(tag, published, url, total));
            }
            _cachedReleases = releases;
            Dispatcher.Invoke(() => BuildReleaseRows(releases));
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                ReleasesStatus.Text = string.Format(Locale.AboutReleasesErrorFormat, ex.Message);
                ReleasesStatus.Visibility = Visibility.Visible;
            });
        }
    }

    /// <summary>
    ///     Build one row per release: tag (semibold) + published date + total download count +
    ///     "Open on GitHub" button. Total downloads is the sum across every asset (DirectML zip,
    ///     CUDA zip, Installer.exe, …) so users see the real reach of each release.
    /// </summary>
    private void BuildReleaseRows(List<ReleaseInfo> releases)
    {
        ReleasesPanel.Children.Clear();
        if (releases.Count == 0)
        {
            ReleasesStatus.Text = Locale.AboutReleasesEmpty;
            ReleasesStatus.Visibility = Visibility.Visible;
            return;
        }
        ReleasesStatus.Visibility = Visibility.Collapsed;

        foreach (var r in releases)
        {
            var row = new Border
            {
                Padding = new Thickness(10, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 4),
                CornerRadius = new CornerRadius(4),
                Background = (System.Windows.Media.Brush)FindResource("FluentSurface2"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("FluentStroke"),
                BorderThickness = new Thickness(1),
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tag = new TextBlock
            {
                Text = "v" + r.Tag,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("FluentTextPrimary"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(tag, 0);
            grid.Children.Add(tag);

            var date = new TextBlock
            {
                Text = r.Published == DateTime.MinValue ? "" : r.Published.ToString("yyyy-MM-dd"),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Small"),
                FontSize = 11,
                Foreground = (System.Windows.Media.Brush)FindResource("FluentTextTertiary"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(date, 1);
            grid.Children.Add(date);

            var dl = new TextBlock
            {
                Text = string.Format(Locale.AboutDownloadsFormat, r.TotalDownloads.ToString("N0")),
                FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono,Consolas"),
                FontSize = 11,
                Foreground = (System.Windows.Media.Brush)FindResource("FluentAccent"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };
            Grid.SetColumn(dl, 2);
            grid.Children.Add(dl);

            var open = new Button
            {
                Content = new TextBlock
                {
                    Text = "", // pop-out glyph
                    FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                    FontSize = 11,
                },
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = r.HtmlUrl,
                ToolTip = Locale.OpenOnGitHub,
            };
            open.SetResourceReference(StyleProperty, "FluentStandardButton");
            open.Click += (s, _) =>
            {
                if (s is FrameworkElement fe && fe.Tag is string url && !string.IsNullOrWhiteSpace(url))
                    OpenUrl(url);
            };
            Grid.SetColumn(open, 3);
            grid.Children.Add(open);

            row.Child = grid;
            ReleasesPanel.Children.Add(row);
        }
    }

    private void AboutLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        OpenUrl(e.Uri?.ToString() ?? "");
        e.Handled = true;
    }

    private void OpenReleasesOnGitHub_Click(object sender, RoutedEventArgs e)
        => OpenUrl(ApplicationConstants.ReleasesUrl);

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch { /* user has no default browser / blocked — silently ignore */ }
    }

    private void LoadCreditsMenu()
    {
        CreditsPanel.RemoveAll();
    }

    public async Task LoadStoreMenu()
    {
        // Fork must be first — it is the tie-break winner when commit dates collide
        // (see FileManager.RetrieveAndMergeFromRepos).
        (string owner, string repo, string subPath)[] repos =
        [
            (ApplicationConstants.RepoOwner, ApplicationConstants.RepoName, "models"),
            (ApplicationConstants.UpstreamRepoOwner, ApplicationConstants.UpstreamRepoName, "models"),
        ];

        (string owner, string repo, string subPath)[] configRepos =
        [
            (ApplicationConstants.RepoOwner, ApplicationConstants.RepoName, "configs"),
        ];

        try
        {
            Task models = FileManager.RetrieveAndMergeFromRepos(repos, "bin\\models", _availableModels);
            Task configs = FileManager.RetrieveAndMergeFromRepos(configRepos, "bin\\configs", _availableConfigs);

            await Task.WhenAll(models, configs);
            FillMenus();
        }
        catch (Exception e)
        {
            new NoticeBar(e.Message, 10000).Show();
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            DownloadGateway(ModelStoreScroller, _availableModels, "models");
            DownloadGateway(ConfigStoreScroller, _availableConfigs, "configs");
        });
    }

    private void DownloadGateway(StackPanel Scroller, Dictionary<string, GitHubFile> entries, string folder)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Scroller.Children.Clear();

            if (entries.Count > 0)
            {
                foreach (var entry in entries)
                {
                    // Pass the resolved download URL so the click hits whichever repo (fork or
                    // upstream) actually owns the freshest copy of this filename.
                    ADownloadGateway gateway = new(entry.Key, folder, entry.Value.DownloadUrl);
                    Scroller.Children.Add(gateway);
                }
            }
            else
            {
                LackOfConfigsText.Visibility = Visibility.Visible;
                LackOfModelsText.Visibility = Visibility.Visible;
            }
        });
    }

    #endregion Menu Loading


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

    private void LoadAntiRecoilConfig(string path = "bin\\anti_recoil_configs\\Default.cfg",
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
        var path = Path.Combine("bin/models", model);
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