using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using PowerAim.Class;
using PowerAim.Class.Native;
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
using AimmyWPF.Class;
using Core;
using InputLogic;
using Microsoft.Xaml.Behaviors.Core;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Nextended.Core;
using Nextended.Core.Extensions;
using Nextended.UI.Helper;
using Other;
using UILibrary;
using Visuality;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
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
        if (_currentScrollViewer == null) throw new NullReferenceException("CurrentScrollViewer is null");

        _fileManager?.Dispose();
        _fileManager = new FileManager(ModelListBox, SelectedModelNotifier, ConfigsListBox, SelectedConfigNotifier);

        // Needed to import annotations into MakeSense
        if (!File.Exists("bin\\labels\\labels.txt")) File.WriteAllText("bin\\labels\\labels.txt", "Enemy");

        if (BindingManager != null)
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

        if (isRecreating && menu != null)
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
        if (Config != null)
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
            _ = _fileManager.LoadModel(Path.GetFileName(modelPath), modelPath);
        }
        UpdateModelText();
    }


    public bool IsModelLoaded => FileManager.AIManager?.IsModelLoaded ?? false;
    public bool IsNotModelLoaded => !IsModelLoaded;

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
        }, border => border.Background = Brushes.Transparent, changer => changer.CanEditMinTime = false);
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
        AboutSpecs.Content =
            $"{GetProcessorName()} • {GetVideoControllerName()} • {GetFormattedMemorySize()}GB RAM";

        if (GamepadTester != null)
            GamepadTester.BackRequested += (_, _) => _ = NavigateTo(nameof(GamepadSettings));
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
        for (int i = 0; i < 32 && node != null; i++)
        {
            switch (node)
            {
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

    // ===================================================================== LAYOUT MANAGER ====

    private readonly Dictionary<string, PowerAim.Visuality.PageLayoutManager> _pageLayouts = new();
    private PowerAim.Visuality.HiddenBoxesPill? _hiddenBoxesPill;

    private static readonly string[] _layoutManagedPages =
    {
        "AimMenu", "ModelMenu", "SettingsMenu", "AutoPlayMenu",
        "Tools", "Logs", "AboutMenu", "GamepadSettings"
    };

    /// <summary>
    ///     Attach a <see cref="PowerAim.Visuality.PageLayoutManager"/> to whichever pages already
    ///     have a fully-realised visual tree. Collapsed pages still lazy-attach on first nav via
    ///     <see cref="EnsurePageAttached"/>. Called after <see cref="CreateUI"/> finishes.
    /// </summary>
    private void AttachLayoutManagers()
    {
        _pageLayouts.Clear();
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
        _pageLayouts[name] = PowerAim.Visuality.PageLayoutManager.Attach(name, page);
    }

    private void EnsureHiddenBoxesPill()
    {
        if (_hiddenBoxesPill != null) return;
        // Inject the pill into the outermost Grid that hosts the page area. The first child of
        // MainWindow is a Grid (the row/column layout); we put the pill there with high Z-index.
        if (Content is Grid root)
            _hiddenBoxesPill = new PowerAim.Visuality.HiddenBoxesPill(root);
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
        if (_searchIndex == null) return;
        var matches = PowerAim.Class.GlobalSearch.Filter(_searchIndex, query);
        if (matches.Count == 0)
        {
            GlobalSearchHint.Text = "No matches.";
            return;
        }
        GlobalSearchHint.Text = $"{matches.Count} match{(matches.Count == 1 ? "" : "es")} · Enter = open first";
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
            if (name != null && CurrentMenu != name)
            {
                await NavigateTo(name, true, clickedButton);
            }
        }
    }

    private Button? FindNavButton(string name)
    {
        return MenuButtons?.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == name)
            ?? MenuButtonsBottom?.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == name);
    }

    private async Task NavigateTo(string name, bool animate = true, Button? clickedButton = null)
    {
        if (SectionLabel != null)
        {
            var section = string.Join(" ", name.Replace("Menu", "").SplitByUpperCase()).ToUpper();
            SectionLabel.Content = section == "AIM" ? "MAIN" : section;
        }

        clickedButton ??= FindNavButton(name);
        _currentlySwitching = true;
        if (clickedButton != null && MenuHighlighter?.Parent is UIElement highlighterParent)
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
        if (_currentScrollViewer != null && _currentScrollViewer != movingScrollViewer)
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
        if (list == null) return;
        foreach (var item in list.Items)
        {
            var container = list.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
            if (container == null) continue;
            var match = string.IsNullOrEmpty(searchText) || (item?.ToString()?.ToLower().Contains(searchText) ?? false);
            container.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void FilterDownloadPanel(Panel? panel, string searchText)
    {
        if (panel == null) return;
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
            var res = PowerAim.Visuality.MessageDialog.Show(
                Locale.UnsavedChangesMessage,
                Locale.UnsavedChanges,
                PowerAim.Visuality.MessageDialog.DialogButtons.YesNoCancel,
                PowerAim.Visuality.MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.Yes);
            if (res == PowerAim.Visuality.MessageDialog.DialogResult.Cancel) return; // stay on editor
            if (res == PowerAim.Visuality.MessageDialog.DialogResult.Yes) save = true; // proceed as if Save was clicked
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

    private void SetSidebarLocked(bool locked)
    {
        if (Sidebar != null)
        {
            Sidebar.IsHitTestVisible = !locked;
            Sidebar.Opacity = locked ? 0.4 : 1.0;
        }
        if (HamburgerButton != null) HamburgerButton.IsEnabled = !locked;
    }

    private void TriggerEditBack_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(false);
    private void TriggerEditCancel_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(false);
    private void TriggerEditSave_Click(object sender, RoutedEventArgs e) => CloseTriggerEditor(true);

    private async Task RunBenchmarkClick()
    {
        var modelFile = AppConfig.Current?.LastLoadedModel;
        if (string.IsNullOrWhiteSpace(modelFile) || modelFile == "N/A")
        {
            PowerAim.Visuality.MessageDialog.Warn(Locale.BenchmarkNoModel, Locale.RunBenchmark, owner: this);
            return;
        }
        var modelPath = Path.Combine("bin/models", modelFile);
        if (!File.Exists(modelPath))
        {
            PowerAim.Visuality.MessageDialog.Warn(Locale.BenchmarkNoModel, Locale.RunBenchmark, owner: this);
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

            PowerAim.Visuality.MessageDialog.Show(
                msg, Locale.BenchmarkResult,
                PowerAim.Visuality.MessageDialog.DialogButtons.OK,
                PowerAim.Visuality.MessageDialog.DialogIcon.Info,
                owner: this);
        }
        catch (Exception ex)
        {
            notice.Close();
            PowerAim.Visuality.MessageDialog.Error(ex.Message, Locale.RunBenchmark, owner: this);
        }
    }


    private void BindingOnKeyReleased(string bindingId)
    {
        switch (bindingId)
        {
            case nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind):
                AppConfig.Current.SliderSettings.OnPropertyChanged(nameof(AppConfig.Current.SliderSettings.ActualFovSize));
                if (FOV.Instance != null)
                {
                    Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.FOVSize);
                    Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.FOVSize);
                }
                break;
        }
    }

    private void BindingOnKeyPressed(string bindingId)
    {
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
                if (FOV.Instance != null)
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

            case nameof(AppConfig.Current.BindingSettings.Gun1Key):
                if (AppConfig.Current.ToggleState.EnableGunSwitchingKeybind)
                    LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun1Config, true);
                break;

            case nameof(AppConfig.Current.BindingSettings.Gun2Key):
                if (AppConfig.Current.ToggleState.EnableGunSwitchingKeybind)
                    LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun2Config, true);
                break;
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
        ARConfig.RemoveAll();
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
        ConfigSettings.AddButton(Locale.SaveConfig).Reader.Click += (s, e) => new ConfigSaver().ShowDialog();
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
            new PowerAim.Visuality.TargetClassDialog(classes) { Owner = this }.ShowDialog();
        };
        PredictionConfig.AddButton("Detection masks…").Reader.Click += (_, _) =>
        {
            new PowerAim.Visuality.DetectionMasksDialog { Owner = this }.ShowDialog();
        };
        PredictionConfig.AddSeparator();
        PredictionConfig.Visibility = GetVisibilityFor(nameof(AimConfig));


        AimConfig.AddTitle(Locale.AimConfig, true);


        AimConfig.AddDropdown(Locale.AimingBoundariesAlignment, AppConfig.Current.DropdownState.AimingBoundariesAlignment, v => AppConfig.Current.DropdownState.AimingBoundariesAlignment = v);
        AimConfig.AddDropdown(Locale.MovementPath, AppConfig.Current.DropdownState.MovementPathType, v => AppConfig.Current.DropdownState.MovementPathType = v);
        AimConfig.AddSlider(Locale.MouseSensitivity, Locale.Sensitivity, 0.01, 0.01, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.MouseSensitivity);
        AimConfig.AddButton("Calibrate sensitivity…").Reader.Click += (_, _) =>
        {
            new PowerAim.Visuality.CalibrationWizardDialog { Owner = this }.ShowDialog();
        };

        AimConfig.AddSlider(Locale.MouseJitter, Locale.Jitter, 1, 1, 0, 15).BindTo(() => AppConfig.Current.SliderSettings.MouseJitter);

        AimConfig.AddSlider(Locale.YOffset, Locale.Offset, 1, 1, -150, 150).BindTo(() => AppConfig.Current.SliderSettings.YOffset);
        AimConfig.AddSlider(Locale.YOffsetPercentage, Locale.Percent, 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.YOffsetPercentage);

        AimConfig.AddSlider(Locale.XOffset, Locale.Offset, 1, 1, -150, 150).BindTo(() => AppConfig.Current.SliderSettings.XOffset);
        AimConfig.AddSlider(Locale.XOffsetPercentage, Locale.Percent, 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.XOffsetPercentage);

        AimConfig.AddSlider(Locale.EMASmoothening, Locale.Amount, 0.01, 0.01, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.EMASmoothening);

        // ----- Sticky Aim -----
        AimConfig.AddToggle(Locale.StickyAimEnabled).BindTo(() => AppConfig.Current.AISettings.StickyAimEnabled);
        AimConfig.AddSlider(Locale.StickyAimThreshold, Locale.Pixels, 1, 5, 10, 300)
            .BindTo(() => AppConfig.Current.AISettings.StickyAimThreshold);
        AimConfig.AddSlider(Locale.StickyAimMaxLockScore, Locale.Amount, 1, 5, 10, 300)
            .BindTo(() => AppConfig.Current.AISettings.StickyAimMaxLockScore);

        AimConfig.AddSeparator();
        AimConfig.Visibility = GetVisibilityFor(nameof(AimConfig));

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
        AntiRecoil.AddToggleWithKeyBind(Locale.AntiRecoil, nameof(Locale.AntiRecoil), BindingManager).BindTo(() => AppConfig.Current.ToggleState.AntiRecoil).BindActiveStateColor(AntiRecoil);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind), () => keybind.AntiRecoilKeybind, BindingManager);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind), () => keybind.DisableAntiRecoilKeybind, BindingManager);

        // BETA toggle for the experimental image-based mode. When on, the manual sliders below
        // are disabled and only the Image-Based Strength slider applies.
        var betaToggle = AntiRecoil.AddToggle(Locale.UseImageBasedAntiRecoil);
        betaToggle.ToolTip = Locale.UseImageBasedAntiRecoilHelp;
        betaToggle.BindTo(() => AppConfig.Current.AntiRecoilSettings.UseImageBasedAntiRecoil);

        // Pattern recorder + playback. Mutually exclusive with both the legacy and the BETA paths
        // — when this is on and a pattern is selected, the other two skip themselves in their
        // .Active getters.
        var patternToggle = AntiRecoil.AddToggle("Use recoil pattern playback");
        patternToggle.ToolTip = "When armed, plays back a previously recorded recoil pattern while you fire. Overrides the legacy / BETA paths.";
        patternToggle.BindTo(() => AppConfig.Current.AntiRecoilSettings.UsePatternRecoil);

        var patternStatus = new System.Windows.Controls.Label
        {
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
            FontSize = 12,
            Padding = new Thickness(0),
            Margin = new Thickness(8, 0, 8, 6),
        };
        AntiRecoil.Children.Add(patternStatus);

        AntiRecoil.AddButton("Recoil patterns…").Reader.Click += (_, _) =>
        {
            new PowerAim.Visuality.RecoilPatternsDialog { Owner = this }.ShowDialog();
        };

        // Image-based strength (only relevant when BETA is on).
        var strengthSlider = AntiRecoil.AddSlider(Locale.AntiRecoilStrength, Locale.Amount, 0.05, 0.05, 0, 1.5)
            .InitWith(s => s.ToolTip = Locale.AntiRecoilStrengthHelp)
            .BindTo(() => AppConfig.Current.AntiRecoilSettings.AutoStrength);

        // Legacy manual sliders (disabled when BETA is on).
        var holdTimeSlider = AntiRecoil.AddSlider(Locale.HoldTime, Locale.Milliseconds, 1, 1, 1, 1000, true)
            .BindTo(() => AppConfig.Current.AntiRecoilSettings.HoldTime);
        var recordFireRateBtn = AntiRecoil.AddButton(Locale.RecordFireRate);
        recordFireRateBtn.Reader.Click += (s, e) => new SetAntiRecoil(this).Show();
        var fireRateSlider = AntiRecoil.AddSlider(Locale.FireRate, Locale.Milliseconds, 1, 1, 1, 5000, true)
            .BindTo(() => AppConfig.Current.AntiRecoilSettings.FireRate);
        var yRecoilSlider = AntiRecoil.AddSlider(Locale.YRecoilUpDown, Locale.Move, 1, 1, -1000, 1000, true)
            .BindTo(() => AppConfig.Current.AntiRecoilSettings.YRecoil);
        var xRecoilSlider = AntiRecoil.AddSlider(Locale.XRecoilLeftRight, Locale.Move, 1, 1, -1000, 1000, true)
            .BindTo(() => AppConfig.Current.AntiRecoilSettings.XRecoil);

        // Show whichever block of controls is relevant for the engine the user selected.
        // Precedence: pattern playback > BETA image-based > legacy fixed X/Y.
        //   pattern armed → hide everything else (legacy sliders, BETA strength) plus status hint
        //   BETA on       → only the strength slider visible; legacy sliders + ARConfig collapsed
        //   default       → strength slider hidden, legacy sliders + ARConfig visible
        void UpdateAntiRecoilVisibility()
        {
            var s = AppConfig.Current.AntiRecoilSettings;
            bool pattern = s.UsePatternRecoil && !string.IsNullOrEmpty(s.ActivePatternName);
            bool beta = s.UseImageBasedAntiRecoil && !pattern;
            bool legacy = !pattern && !beta;

            strengthSlider.Visibility    = beta ? Visibility.Visible : Visibility.Collapsed;
            holdTimeSlider.Visibility    = legacy ? Visibility.Visible : Visibility.Collapsed;
            recordFireRateBtn.Visibility = legacy ? Visibility.Visible : Visibility.Collapsed;
            fireRateSlider.Visibility    = legacy ? Visibility.Visible : Visibility.Collapsed;
            yRecoilSlider.Visibility     = legacy ? Visibility.Visible : Visibility.Collapsed;
            xRecoilSlider.Visibility     = legacy ? Visibility.Visible : Visibility.Collapsed;
            // Collapse the entire "Anti Recoil Config" card too (the FluentCard Border that
            // wraps the ARConfig StackPanel) — it's only meaningful for the legacy path.
            if (ARConfig.Parent is FrameworkElement arCard)
                arCard.Visibility = legacy ? Visibility.Visible : Visibility.Collapsed;

            // Status line right under the pattern toggle.
            if (pattern)
                patternStatus.Content = $"Active pattern: {s.ActivePatternName}  ·  strength {s.PatternStrength:0.00}";
            else if (s.UsePatternRecoil)
                patternStatus.Content = "Pattern playback armed but no pattern selected. Open 'Recoil patterns…' and click one in the list.";
            else if (beta)
                patternStatus.Content = "Mode: image-based (BETA, phase correlation).";
            else
                patternStatus.Content = "Mode: legacy (fixed X/Y per fire rate). Record Fire Rate measures the cadence for this mode only.";
        }
        AppConfig.Current.AntiRecoilSettings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AntiRecoilSettings.UseImageBasedAntiRecoil)
                || e.PropertyName == nameof(AntiRecoilSettings.UsePatternRecoil)
                || e.PropertyName == nameof(AntiRecoilSettings.ActivePatternName)
                || e.PropertyName == nameof(AntiRecoilSettings.PatternStrength))
                Dispatcher.Invoke(UpdateAntiRecoilVisibility);
        };
        UpdateAntiRecoilVisibility();

        AntiRecoil.AddSeparator();
        AntiRecoil.Visibility = GetVisibilityFor(nameof(AntiRecoil));

        #endregion Anti Recoil

        #region Anti Recoil Config

        // Anti-Recoil Config — per-gun manual recoil patterns. Only meaningful when the legacy
        // (non-BETA) pattern-based path is active.
        ARConfig.AddTitle(Locale.AntiRecoilConfig, true);
        ARConfig.AddToggleWithKeyBind(Locale.EnableGunSwitchingKeybind, nameof(Locale.EnableGunSwitchingKeybind), BindingManager).BindTo(() => AppConfig.Current.ToggleState.EnableGunSwitchingKeybind).BindActiveStateColor(ARConfig);
        ARConfig.AddButton(Locale.SaveAntiRecoilConfig).Reader.Click += (s, e) =>
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = $"{Directory.GetCurrentDirectory}\\bin\\anti_recoil_configs",
                Filter = Locale.FilterAntiRecoilConfig
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                AppConfig.Current.AntiRecoilSettings.Save<AntiRecoilSettings>(saveFileDialog.FileName);
                new NoticeBar(Locale.AntiRecoilSaved.FormatWith(saveFileDialog.FileName), 2000).Show();
            }
        };
        ARConfig.AddKeyChanger(Locale.GunKey.FormatWith(1), () => keybind.Gun1Key, BindingManager);
        ARConfig.AddFileLocator(Locale.GunConfig.FormatWith(1), Locale.FilterAntiRecoilConfig, "\\bin\\anti_recoil_configs");
        ARConfig.AddKeyChanger(Locale.GunKey.FormatWith(2), () => keybind.Gun2Key, BindingManager);
        ARConfig.AddFileLocator(Locale.GunConfig.FormatWith(2), Locale.FilterAntiRecoilConfig, "\\bin\\anti_recoil_configs");

        ARConfig.AddButton(Locale.LoadGunConfig.FormatWith(1)).Reader.Click +=
            (s, e) => LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun1Config, true);
        ARConfig.AddButton(Locale.LoadGunConfig.FormatWith(2)).Reader.Click +=
            (s, e) => LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun2Config, true);
        ARConfig.AddSeparator();
        ARConfig.Visibility = GetVisibilityFor(nameof(ARConfig));

        #endregion Anti Recoil Config

        #region FOV Config

        FOVConfig.AddTitle(Locale.FOVConfig, true);
        FOVConfig.AddToggleWithKeyBind(Locale.FOV, nameof(Locale.FOV), BindingManager).BindTo(() => AppConfig.Current.ToggleState.FOV).BindActiveStateColor(FOVConfig);
        FOVConfig.AddToggleWithKeyBind(Locale.DynamicFOV, nameof(Locale.DynamicFOV), BindingManager).BindTo(() => AppConfig.Current.ToggleState.DynamicFOV);
        FOVConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind), () => keybind.DynamicFOVKeybind, BindingManager);
        FOVConfig.AddColorChanger(Locale.FOVColor).BindTo(() => AppConfig.Current.ColorState.FOVColor);

        FOVConfig.AddSlider(Locale.FOVSize, Locale.Size, 1, 1, 10, 640).BindTo(() => AppConfig.Current.SliderSettings.FOVSize);
        FOVConfig.AddSlider(Locale.DynamicFOVSize, Locale.Size, 1, 1, 10, 640).BindTo(() => AppConfig.Current.SliderSettings.DynamicFOVSize);
        FOVConfig.AddSlider(Locale.FOVOpacity, Locale.FOVOpacity, 0.1, 0.1, 0, 1).BindTo(() => AppConfig.Current.SliderSettings.FOVOpacity);

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
                w1 = new NoticeBar("WARNING: This method is the fastest, but not hidden for captures!!", 8000);
                w1.Closed += (s, e) => w1 = null;
                w1.Show();
                if (AppConfig.Current.ToggleState.GlobalActive && AppConfig.Current.ToggleState.ShowDetectedPlayer)
                {
                    AppConfig.Current.ToggleState.GlobalActive = false;
                    w2 = new NoticeBar("For more security we disabled the active state to prevent auto bans by capture analysis. Use this mode carefully! ", 10000);
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

        ESPConfig.AddSlider(Locale.Opacity, Locale.Opacity, 0.1, 0.1, 0, 1).BindTo(() => AppConfig.Current.SliderSettings.Opacity);

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
            GamepadSettingsConfig.AddCredit("Internal Mode", "Internal mode virtualizes the controller without requiring external drivers. The virtual controller can be manipulated via the existing interfaces and classes.");
            if (GamepadManager.CanSend)
            {
                GamepadSettingsConfig.AddCredit(Locale.Status, $"{Locale.Great.ToUpper()}, {Locale.GamepadDriverSuccessMessage}");
            }
            else
            {
                GamepadSettingsConfig.AddCredit(Locale.Status, "Internal mode initialized", credit => credit.Description.Foreground = Brushes.Orange);
            }
        }

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


            if (!File.Exists(HidHideHelper.GetHidHidePath()))
            {
                GamepadSettingsConfig.AddButton(Locale.InstallHidHide, b =>
                {
                    b.Reader.Click += (s, e) => WindowsHelper.RunResourceTool("HidHide_1.5.230_x64.exe", null, _ => Reload());
                });
            }
            else
            {
                GamepadSettingsConfig.AddButton(Locale.LaunchHidHideUI, b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        var fileName = Path.Combine(Path.GetDirectoryName(HidHideHelper.GetHidHidePath()), "HidHideClient.exe");
                        if (File.Exists(fileName))
                            Process.Start(fileName);
                    };
                });
            }

            GamepadSettingsConfig.AddSeparator();

            GamepadSettingsConfig.AddButton(Locale.ShowAndTestController, b =>
            {
                b.Reader.Click += (s, e) =>
                {

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c joy.cpl",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(startInfo);
                };
            });
        }

        // Add test window button for all modes (except None)
        if (AppConfig.Current.DropdownState.GamepadSendMode != GamepadSendMode.None)
        {
            if (AppConfig.Current.DropdownState.GamepadSendMode != GamepadSendMode.VJoy &&
                AppConfig.Current.DropdownState.GamepadSendMode != GamepadSendMode.ViGEm &&
                AppConfig.Current.DropdownState.GamepadSendMode != GamepadSendMode.Internal)
            {
                GamepadSettingsConfig.AddSeparator();
            }
            
            GamepadSettingsConfig.AddButton("Open Gamepad Tester", b =>
            {
                b.Reader.Click += (s, e) =>
                {
                    _ = NavigateTo(nameof(GamepadTestPage));
                };
            });
        }

    }

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

        ToolsConfig.AddTitle("HWID Spoofer", false);

        ToolsConfig.AddButton("Open HWID Spoofer").Reader.Click += (_, _) => OpenSpoofer();
        ToolsConfig.AddCredit("", "This external tool helps to change your Hardware Id, that can help if your PC is banned");

        ToolsConfig.AddSeparator();

        BuildSettingsExtras();
    }

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
        // ===== Active Processes (Auto-Pause + per-game profile switch) =====
        ActiveProcessesSettings.AddTitle("Active Processes", true);
        ActiveProcessesSettings.AddToggle("Auto-pause when game loses focus")
            .InitWith(t => t.ToolTip = "Pauses AI actions when the foreground window is a known non-game (browser, terminal, PowerAim itself, …).")
            .BindTo(() => AppConfig.Current.ActiveProcessSettings.AutoPauseOnFocusLoss);
        ActiveProcessesSettings.AddToggle("Auto-switch profile by game")
            .InitWith(t => t.ToolTip = "When a Trigger / AutoPlay profile has a Match-Process pattern, it's only active while the matching game is in the foreground.")
            .BindTo(() => AppConfig.Current.ActiveProcessSettings.AutoSwitchProfile);
        ActiveProcessesSettings.AddCredit("", "Match patterns support wildcards (cs2*) and pipes (cs2|valorant). Configure them on each Trigger / AutoPlay profile.");

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
            ToolTip = "Comma- or pipe-separated process patterns considered \"games\". Empty = use the built-in non-game blacklist.",
        };
        whitelistInput.Text = string.Join(", ", AppConfig.Current.ActiveProcessSettings.GameProcessPatterns);
        whitelistInput.LostFocus += (_, _) =>
        {
            var list = AppConfig.Current.ActiveProcessSettings.GameProcessPatterns;
            list.Clear();
            foreach (var part in whitelistInput.Text.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                list.Add(part);
        };
        ActiveProcessesSettings.Children.Add(whitelistInput);
        ActiveProcessesSettings.AddSeparator();

        // ===== Overlays (Debug + Crosshair) =====
        OverlaySettings.AddTitle("Overlays", true);
        OverlaySettings.AddToggle("Show debug overlay")
            .InitWith(t => t.ToolTip = "Tiny topmost panel showing FPS, inference time, detection count, current intent and active profile.")
            .BindTo(() => AppConfig.Current.ToggleState.ShowDebugOverlay);

        OverlaySettings.AddToggle("Show custom crosshair")
            .InitWith(t => t.ToolTip = "Render a configurable crosshair at the centre of the primary screen. Useful for games without a built-in crosshair.")
            .BindTo(() => AppConfig.Current.ToggleState.ShowCrosshairOverlay);

        OverlaySettings.AddDropdown("Crosshair shape",
            AppConfig.Current.CrosshairSettings.Shape,
            v => AppConfig.Current.CrosshairSettings.Shape = v);
        OverlaySettings.AddSlider("Crosshair size", "px", 1, 1, 4, 80)
            .BindTo(() => AppConfig.Current.CrosshairSettings.Size);
        OverlaySettings.AddSlider("Crosshair thickness", "px", 1, 1, 1, 10)
            .BindTo(() => AppConfig.Current.CrosshairSettings.Thickness);
        OverlaySettings.AddSlider("Crosshair gap", "px", 1, 1, 0, 30)
            .BindTo(() => AppConfig.Current.CrosshairSettings.Gap);
        OverlaySettings.AddSlider("Crosshair outline", "px", 1, 1, 0, 4)
            .BindTo(() => AppConfig.Current.CrosshairSettings.OutlineThickness);
        OverlaySettings.AddSeparator();

        // ===== Stats =====
        StatsCard.AddTitle("Session Stats", true);
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

        var statsTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        statsTimer.Tick += (_, _) =>
        {
            var s = PowerAim.Class.SessionStats.Instance;
            fpsLabel.Content      = $"FPS                 {s.InstantFps:0.0}";
            msLabel.Content       = $"Inference time      {s.LastInferenceMs:0.0} ms";
            detLabel.Content      = $"Detections (last)   {s.LastDetectionCount}";
            shotsLabel.Content    = $"Shots fired         {s.ShotsFired}";
            framesLabel.Content   = $"Frames processed    {s.FramesProcessed}";
            tacticalLabel.Content = $"Tactical actions    {s.TacticalActionsUsed}";
            durationLabel.Content = $"Session             {s.Duration:hh\\:mm\\:ss}";
        };
        statsTimer.Start();

        StatsCard.AddButton("Reset stats").Reader.Click += (_, _) => PowerAim.Class.SessionStats.Instance.Reset();
        StatsCard.AddToggle("Adaptive Kalman lead-time")
            .InitWith(t => t.ToolTip = "Scales the predictor's lead-time based on target velocity — less lead on stationary targets, more on fast strafing ones.")
            .Checked = PowerAim.AILogic.PredictionSettings.AdaptiveKalmanLead;
        StatsCard.AddSeparator();

        // ===== HUD OCR =====
        HudOcrCard.AddTitle("HUD OCR", true);
        HudOcrCard.AddToggle("Enable HUD OCR engine")
            .InitWith(t => t.ToolTip = "Periodically samples screen regions (ammo, health, score) with Tesseract. Results are exposed to AutoPlay via OcrService.Instance.Latest.")
            .BindTo(() => AppConfig.Current.OcrSettings.Enabled);
        HudOcrCard.AddSlider("OCR interval", "ms", 50, 50, 100, 5000, true)
            .BindTo(() => AppConfig.Current.OcrSettings.IntervalMs);
        HudOcrCard.AddButton("Configure OCR regions…").Reader.Click += (_, _) =>
        {
            new PowerAim.Visuality.OcrRegionsDialog { Owner = this }.ShowDialog();
        };
        HudOcrCard.AddSeparator();

        // ===== Replay Buffer =====
        ReplayCard.AddTitle("Replay Buffer", true);
        ReplayCard.AddToggle("Record rolling buffer")
            .InitWith(t => t.ToolTip = "Keeps the last N seconds of frames + detections in RAM. Click 'Save replay' to flush to disk as a PNG sequence + annotations.json.")
            .BindTo(() => AppConfig.Current.ReplaySettings.Enabled);
        ReplayCard.AddSlider("Buffer length", "s", 1, 1, 1, 30, true)
            .BindTo(() => AppConfig.Current.ReplaySettings.BufferSeconds);
        ReplayCard.AddSlider("JPEG quality", "", 1, 5, 10, 100, true)
            .BindTo(() => AppConfig.Current.ReplaySettings.JpegQuality);

        var replayStatus = new System.Windows.Controls.Label
        {
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono,Consolas"),
            FontSize = 12,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 4, 2, 4),
            Content = "0 frames buffered"
        };
        ReplayCard.Children.Add(replayStatus);
        PowerAim.AILogic.ReplayBuffer.Instance.PropertyChanged += (_, _) =>
            Dispatcher.BeginInvoke(new Action(() =>
                replayStatus.Content = $"{PowerAim.AILogic.ReplayBuffer.Instance.FrameCount} frames buffered"));

        ReplayCard.AddButton("Save replay buffer").Reader.Click += async (_, _) =>
        {
            replayStatus.Content = "Exporting…";
            var folder = await PowerAim.AILogic.ReplayBuffer.Instance.ExportAsync();
            replayStatus.Content = folder == null
                ? "Nothing to export — buffer is empty."
                : $"Saved to {folder}";
        };
        ReplayCard.AddButton("Clear buffer").Reader.Click += (_, _) =>
            PowerAim.AILogic.ReplayBuffer.Instance.Clear();
        ReplayCard.AddSeparator();

        // ===== AutoPlay learning =====
        LearningCard.AddTitle("AutoPlay Learning", true);
        LearningCard.AddToggle("Record my playstyle")
            .InitWith(t => t.ToolTip = "While on, samples (state, action) tuples from your input and current detections. Stored in an in-memory model that can be saved to disk.")
            .BindTo(() => AppConfig.Current.AutoPlayLearningSettings.Recording);
        LearningCard.AddToggle("Apply learned bias in AutoPlay")
            .InitWith(t => t.ToolTip = "When AutoPlay picks a tactical action, the learned model can nudge the choice toward what you typically did in similar situations.")
            .BindTo(() => AppConfig.Current.AutoPlayLearningSettings.ApplyModel);
        LearningCard.AddSlider("Bias strength", "", 0.01, 0.01, 0, 1)
            .BindTo(() => AppConfig.Current.AutoPlayLearningSettings.BiasStrength);
        LearningCard.AddSlider("Sample interval", "ms", 50, 50, 50, 1000, true)
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
        PowerAim.AILogic.AutoPlayLearningModel.Instance.PropertyChanged += (_, _) =>
            Dispatcher.BeginInvoke(new Action(() => UpdateLearnStatus(learnStatus)));

        LearningCard.AddButton("Save model").Reader.Click += (_, _) =>
        {
            try
            {
                PowerAim.AILogic.AutoPlayLearningModel.Instance.Save(AppConfig.Current?.AutoPlayLearningSettings?.ModelPath);
                learnStatus.Content = $"Saved · {PowerAim.AILogic.AutoPlayLearningModel.Instance.TotalSamples} samples";
            }
            catch (Exception ex) { learnStatus.Content = $"Save failed: {ex.Message}"; }
        };
        LearningCard.AddButton("Load model").Reader.Click += (_, _) =>
        {
            bool ok = PowerAim.AILogic.AutoPlayLearningModel.Instance.Load(AppConfig.Current?.AutoPlayLearningSettings?.ModelPath);
            learnStatus.Content = ok ? "Model loaded." : "No model file found at the configured path.";
            UpdateLearnStatus(learnStatus);
        };
        LearningCard.AddButton("Clear model").Reader.Click += (_, _) =>
        {
            PowerAim.AILogic.AutoPlayLearningModel.Instance.Clear();
            UpdateLearnStatus(learnStatus);
        };
        LearningCard.AddSeparator();

        // Apply current state of overlays in case the config was loaded with them already enabled.
        if (AppConfig.Current.ToggleState.ShowDebugOverlay)
            PowerAim.Visuality.DebugOverlay.ShowOrHide(true);
        if (AppConfig.Current.ToggleState.ShowCrosshairOverlay)
            PowerAim.Visuality.CrosshairOverlay.ShowOrHide(true);
    }

    private static void UpdateLearnStatus(System.Windows.Controls.Label label)
    {
        var model = PowerAim.AILogic.AutoPlayLearningModel.Instance;
        label.Content = $"{model.TotalSamples} samples · {model.StateCount} states";
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
        AutoPlayConfig.AddTitle("AutoPlay (AI Game Control)", true);
        AutoPlayConfig.Add<OllamaStatusIndicator>();

        // AutoPlay Toggle
        AutoPlayConfig.AddToggleWithKeyBind("AutoPlay", "AutoPlay", BindingManager, toggle =>
        {
            toggle.ToolTip = "Enable AI-controlled gameplay using Ollama vision models";
        }).BindTo(() => AppConfig.Current.ToggleState.AutoPlay);

        // Ollama Settings
        AutoPlayConfig.AddTitle("Ollama Settings");
        AutoPlayConfig.AddSlider("Request Timeout", "seconds", 1, 5, 5, 120).BindTo(() => AppConfig.Current.OllamaSettings.TimeoutSeconds);
        AutoPlayConfig.AddSlider("Temperature", "", 0.1, 0.1, 0.0, 1.0).BindTo(() => AppConfig.Current.OllamaSettings.Temperature);
        AutoPlayConfig.AddSlider("Image Max Size", "px", 64, 128, 256, 1024).BindTo(() => AppConfig.Current.OllamaSettings.ImageMaxSize);
        AutoPlayConfig.AddSlider("Image Quality", "%", 5, 10, 30, 100).BindTo(() => AppConfig.Current.OllamaSettings.ImageQuality);

        AutoPlayConfig.AddSeparator();

        // Profiles Section
        AutoPlayProfiles.AddTitle("AutoPlay Profiles", true);
        AutoPlayProfiles.Add<AutoPlayProfileList>().BindTo(() => AppConfig.Current.AutoPlayProfiles);

        AutoPlayProfiles.AddSeparator();

        // Help Section
        AutoPlayProfiles.AddTitle("Quick Start");
        AutoPlayProfiles.AddCredit("1. Install Ollama", "Visit ollama.com and install");
        AutoPlayProfiles.AddCredit("2. Pull a Vision Model", "Run: ollama pull moondream");
        AutoPlayProfiles.AddCredit("3. Start Ollama", "Run: ollama serve");
        AutoPlayProfiles.AddCredit("4. Create Profile", "Add actions like 'move_left', 'jump'");
        AutoPlayProfiles.AddCredit("5. Enable AutoPlay", "Toggle AutoPlay on");

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


        var text = ApplicationConstants.IsCudaBuild ? "Switch to DirectML Version" : "Switch to CUDA version";
        UISettings.AddButton(text,
            button =>
            {
                button.Reader.Click += async (s, e) =>
                {
                    var releaseManager = new GithubManager();
                    var task = releaseManager.GetAvailableReleasesAsync(Constants.RepoOwner, Constants.RepoName);
                    var releases = await task;
                    var release = releases.FirstOrDefault(x => Version.Parse(x.TagName) == ApplicationConstants.ApplicationVersion);
                    if (release != null)
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

        var darkPaletteOptions = ThemePalette.ByMode(false);
        var currentPalette = darkPaletteOptions.FirstOrDefault(p => p.Name == AppConfig.Current.ThemeName) ?? darkPaletteOptions[0];
        UISettings.AddDropdown(Locale.Theme, currentPalette, darkPaletteOptions, palette =>
        {
            if (Config != null)
                Config.ThemeName = palette.Name;
            PowerAim.Theme.ThemeManager.Apply();
        });
        var currentActive = darkPaletteOptions.FirstOrDefault(p => p.Name == AppConfig.Current.ActiveThemeName) ?? darkPaletteOptions[0];
        UISettings.AddDropdown(Locale.ThemeWhenActive, currentActive, darkPaletteOptions, palette =>
        {
            if (Config != null)
                Config.ActiveThemeName = palette.Name;
            PowerAim.Theme.ThemeManager.Apply();
        });
        UISettings.AddDropdown<AppThemeMode>("Theme Mode", AppConfig.Current.ThemeMode, mode =>
        {
            if (Config != null)
                Config.ThemeMode = mode;
            PowerAim.Theme.ThemeManager.Apply();
        });

        UISettings.AddToggle(Locale.UITopMost).BindTo(() => AppConfig.Current.ToggleState.UITopMost);
        UISettings.AddToggle("Locale.ShowHelpTexts").BindTo(() => AppConfig.Current.ToggleState.ShowHelpTexts);

        var hideCaptureToggle = UISettings.AddToggle("Hide UI from screen capture");
        hideCaptureToggle.BindTo(() => AppConfig.Current.ToggleState.HideUIFromCapture);
        hideCaptureToggle.Changed += (s, e) =>
        {
            if (!e.Value)
            {
                var result = PowerAim.Visuality.MessageDialog.Show(
                    "Warning: if you disable this, the Aimmy window and all its overlays may become visible in screen recordings, streams (OBS, Discord, etc.) and other capture tools.\n\nAre you sure you want to disable capture protection?",
                    "Disable capture protection?",
                    PowerAim.Visuality.MessageDialog.DialogButtons.YesNo,
                    PowerAim.Visuality.MessageDialog.DialogIcon.Warning,
                    owner: this,
                    defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No);
                if (result != PowerAim.Visuality.MessageDialog.DialogResult.Yes)
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

        InputSettings.AddDropdown(Locale.MouseMovementMethod,
            AppConfig.Current.DropdownState.MouseMovementMethod, async v =>
            {
                AppConfig.Current.DropdownState.MouseMovementMethod = v;
                if ((v == MouseMovementMethod.LGHUB && !new LGHubMain().Load())
                    || (v == MouseMovementMethod.RazerSynapse && !await RZMouse.Load())
                    || (v == MouseMovementMethod.ddxoft && !await DdxoftMain.Load())
                   )
                {
                    AppConfig.Current.DropdownState.MouseMovementMethod = MouseMovementMethod.MouseEvent;
                }
            });

        InputSettings.AddSlider(Locale.GamepadMinimumLT, "LT", 0.1, 0.1, 0.1, 1).BindTo(() => AppConfig.Current.SliderSettings.GamepadMinimumLT);
        InputSettings.AddSlider(Locale.GamepadMinimumRT, "RT", 0.1, 0.1, 0.1, 1).BindTo(() => AppConfig.Current.SliderSettings.GamepadMinimumRT);

        InputSettings.AddCredit(Locale.FireMaxDelay, Locale.FireDelayInfo.FormatWith(Environment.NewLine));
        InputSettings.AddSlider(Locale.FireMaxDelay, Locale.Seconds, 0.01, 0.1, 0.00, 2).BindTo(() => AppConfig.Current.SliderSettings.FirePressDelay);




        // X/Y Percentage Adjustment Enabler
        InputSettings.AddSubTitle(Locale.XYPercentageAdjustment);
        InputSettings.AddToggle(Locale.XAxisPercentageAdjustment).BindTo(() => AppConfig.Current.ToggleState.XAxisPercentageAdjustment);
        InputSettings.AddToggle(Locale.YAxisPercentageAdjustment).BindTo(() => AppConfig.Current.ToggleState.YAxisPercentageAdjustment);
        InputSettings.AddSeparator();

    }


    private void LoadCreditsMenu()
    {
        CreditsPanel.RemoveAll();
        return; // TODO: Fix size problems
        CreditsPanel.AddTitle("Developers");
        CreditsPanel.AddCredit("Babyhamsta", "AI Logic");
        CreditsPanel.AddCredit("MarsQQ", "Design");
        CreditsPanel.AddCredit("Taylor", "Optimization, Cleanup");
        CreditsPanel.AddCredit("Florian Gilde", "Optimization, Cleanup, Trigger Bot improvements, Gamepad support");
        CreditsPanel.AddSeparator();

        CreditsPanel.AddTitle("Contributors");
        CreditsPanel.AddCredit("Shall0e", "Prediction Method");
        CreditsPanel.AddCredit("wisethef0x", "EMA Prediction Method");
        CreditsPanel.AddCredit("whoswhip", "Bug fixes & EMA");
        CreditsPanel.AddCredit("HakaCat", "Idea for Auto Labelling Data");
        CreditsPanel.AddCredit("Themida", "LGHub check");
        CreditsPanel.AddCredit("Ninja", "MarsQQ's emotional support");
        CreditsPanel.AddSeparator();

        CreditsPanel.AddTitle("Model Creators");
        CreditsPanel.AddCredit("Babyhamsta", "UniversalV4, Phantom Forces");
        CreditsPanel.AddCredit("Natdog400", "AIO V2, V7");
        CreditsPanel.AddCredit("Themida", "Arsenal, Strucid, Bad Business, Blade Ball, etc.");
        CreditsPanel.AddCredit("Hogthewog", "Da Hood, FN, etc.");
        CreditsPanel.AddSeparator();
    }

    public async Task LoadStoreMenu()
    {
        // Fork must be first — it is the tie-break winner when commit dates collide
        // (see FileManager.RetrieveAndMergeFromRepos).
        var repos = new (string owner, string repo, string subPath)[]
        {
            (ApplicationConstants.RepoOwner, ApplicationConstants.RepoName, "models"),
            (ApplicationConstants.UpstreamRepoOwner, ApplicationConstants.UpstreamRepoName, "models"),
        };
        var configRepos = new (string owner, string repo, string subPath)[]
        {
            (ApplicationConstants.RepoOwner, ApplicationConstants.RepoName, "configs"),
            (ApplicationConstants.UpstreamRepoOwner, ApplicationConstants.UpstreamRepoName, "configs"),
        };

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
        Console.WriteLine("Loading Config: " + path);
        Config = AppConfig.Load(path);
        OnPropertyChanged(nameof(Config));

        if (!string.IsNullOrEmpty(AppConfig.Current.SuggestedModelName) && AppConfig.Current.SuggestedModelName != "N/A")
            PowerAim.Visuality.MessageDialog.Show(
                $"{Locale.ModelSuggestionText}:\n" + AppConfig.Current.SuggestedModelName,
                Locale.SuggestedModel,
                PowerAim.Visuality.MessageDialog.DialogButtons.OK,
                PowerAim.Visuality.MessageDialog.DialogIcon.Info,
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
            OnPropertyChanged(nameof(IsNotModelLoaded));
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
        new ConfigSaver().ShowDialog();
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
        if (modelToLoad != null)
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
        if (configToLoad != null)
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
        if (_magnifier == null && show is null or true)
        {
            _magnifier = new MagnifierDialog();
            _magnifier.Show();
        }
        else if (_magnifier != null && show is null or false)
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
                var res = PowerAim.Visuality.MessageDialog.Show(
                    Locale.ConfirmModelDelete.FormatWith(model), Locale.DeleteModel,
                    PowerAim.Visuality.MessageDialog.DialogButtons.YesNo,
                    PowerAim.Visuality.MessageDialog.DialogIcon.Question,
                    owner: this,
                    defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No);
                if (res == PowerAim.Visuality.MessageDialog.DialogResult.No)
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
                var res = PowerAim.Visuality.MessageDialog.Show(
                    Locale.ConfirmConfigDelete.FormatWith(cfg), Locale.DeleteConfig,
                    PowerAim.Visuality.MessageDialog.DialogButtons.YesNo,
                    PowerAim.Visuality.MessageDialog.DialogIcon.Question,
                    owner: this,
                    defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No);
                if (res == PowerAim.Visuality.MessageDialog.DialogResult.No)
                    return;
            }
            File.Delete(path);
        }
    }
}