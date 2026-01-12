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
using Aimmy2.Class;
using Aimmy2.Class.Native;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.HidHide;
using Aimmy2.Localizations;
using Aimmy2.Models;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.Other;
using Aimmy2.Types;
using Aimmy2.UILibrary;
using Aimmy2.Visuality;
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

namespace Aimmy2;

public partial class MainWindow
{
    #region Main Variables

    public static MainWindow? Instance => Application.Current.Dispatcher.Invoke(() => Application.Current.MainWindow as MainWindow);


    private bool _uiCreated;
    private FileManager _fileManager;
    public AntiRecoilManager ArManager = new();


    private bool _currentlySwitching;
    private ScrollViewer? _currentScrollViewer;

    private readonly HashSet<string> _availableModels = new();
    private readonly HashSet<string> _availableConfigs = new();

    #endregion Main Variables

    #region Loading Window

    public AppConfig? Config { get; internal set; }

    public InputBindingManager? BindingManager { get; private set; }


    public MainWindow()
    {
        InitializeComponent();

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

        MainBorder.BindMouseGradientAngle(ShouldBindGradientMouse);
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

        var theme = ThemePalette.All.FirstOrDefault(x => x.Name == AppConfig.Current.ThemeName) ?? ThemePalette.PurplePalette;
        ApplicationConstants.Theme = theme;

        _currentScrollViewer = FindName(nameof(AimMenu)) as ScrollViewer;
        if (_currentScrollViewer == null) throw new NullReferenceException("CurrentScrollViewer is null");

        _fileManager?.Dispose();
        _fileManager = new FileManager(ModelListBox, SelectedModelNotifier, ConfigsListBox, SelectedConfigNotifier);

        // Needed to import annotations into MakeSense
        if (!File.Exists("bin\\labels\\labels.txt")) File.WriteAllText("bin\\labels\\labels.txt", "Enemy");

        ArManager.HoldDownLoad();

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
        var downloadableMenu = new MenuItem()
        {
            Header = Locale.DownloadableModelsHeader,
            Foreground = Brushes.Black
        };
        ModelContextMenu.Items.Add(downloadableMenu);
        downloadableMenu.Items.AddRange(_availableModels.Select(s => new MenuItem()
        {
            Header = s,
            Foreground = Brushes.Black,
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
        var theme = ThemePalette.All.FirstOrDefault(x => x.Name == AppConfig.Current.ThemeName) ?? ThemePalette.PurplePalette;
        var themeActive = ThemePalette.ThemeForActive;
        ApplicationConstants.Theme = active ? themeActive : theme;
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
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
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

    private async Task NavigateTo(string name, bool animate = true, Button? clickedButton = null)
    {
        clickedButton ??= MenuButtons.Children.OfType<Button>().FirstOrDefault(b => b.Tag?.ToString() == name);
        _currentlySwitching = true;
        var buttonIndx = MenuButtons.Children.IndexOf(clickedButton);
        var margin = buttonIndx * Menu1B.Height;
        Animator.ObjectShift(TimeSpan.FromMilliseconds(animate ? 350 : 0), MenuHighlighter, MenuHighlighter.Margin, new Thickness(0, margin, 0, 0));
        await SwitchScrollPanels(FindName(name) as ScrollViewer ?? throw new NullReferenceException("Scrollpanel is null"), animate);
        CurrentMenu = name;
    }

    private async Task SwitchScrollPanels(ScrollViewer movingScrollViewer, bool animate = true)
    {
        var duration = animate ? 350 : 0;
        movingScrollViewer.Visibility = Visibility.Visible;
        Animator.Fade(movingScrollViewer);

        Animator.ObjectShift(TimeSpan.FromMilliseconds(duration), movingScrollViewer, movingScrollViewer.Margin,
            new Thickness(50, 50, 0, 0));

        Animator.FadeOut(_currentScrollViewer!);
        Animator.ObjectShift(TimeSpan.FromMilliseconds(duration), _currentScrollViewer!, _currentScrollViewer!.Margin,
            new Thickness(50, 450, 0, -400));
        await Task.Delay(350);

        _currentScrollViewer.Visibility = Visibility.Collapsed;
        _currentScrollViewer = movingScrollViewer;
        _currentlySwitching = false;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateVisibilityBasedOnSearchText((TextBox)sender, ModelStoreScroller);
    }

    private void CSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateVisibilityBasedOnSearchText((TextBox)sender, ConfigStoreScroller);
    }

    private void UpdateVisibilityBasedOnSearchText(TextBox textBox, Panel panel)
    {
        var searchText = textBox.Text.ToLower();

        foreach (var item in panel.Children.OfType<ADownloadGateway>())
            item.Visibility = item.Title.Content.ToString()?.ToLower().Contains(searchText) == true
                ? Visibility.Visible
                : Visibility.Collapsed;
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
            // Anti Recoil
            case nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind):
                if (AppConfig.Current.ToggleState.AntiRecoil)
                {
                    ArManager.HoldDownTimer.Stop();
                    ArManager.IndependentMousePress = 0;
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


            case nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind):
                if (AppConfig.Current.ToggleState.AntiRecoil)
                {
                    ArManager.IndependentMousePress = 0;
                    ArManager.HoldDownTimer.Start();
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
        PredictionConfig.AddSeparator();
        PredictionConfig.Visibility = GetVisibilityFor(nameof(AimConfig));


        AimConfig.AddTitle(Locale.AimConfig, true);


        AimConfig.AddDropdown(Locale.AimingBoundariesAlignment, AppConfig.Current.DropdownState.AimingBoundariesAlignment, v => AppConfig.Current.DropdownState.AimingBoundariesAlignment = v);
        AimConfig.AddSlider(Locale.MouseSensitivity, Locale.Sensitivity, 0.01, 0.01, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.MouseSensitivity);

        AimConfig.AddSlider(Locale.MouseJitter, Locale.Jitter, 1, 1, 0, 15).BindTo(() => AppConfig.Current.SliderSettings.MouseJitter);

        AimConfig.AddSlider(Locale.YOffset, Locale.Offset, 1, 1, -150, 150).BindTo(() => AppConfig.Current.SliderSettings.YOffset);
        AimConfig.AddSlider(Locale.YOffsetPercentage, Locale.Percent, 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.YOffsetPercentage);

        AimConfig.AddSlider(Locale.XOffset, Locale.Offset, 1, 1, -150, 150).BindTo(() => AppConfig.Current.SliderSettings.XOffset);
        AimConfig.AddSlider(Locale.XOffsetPercentage, Locale.Percent, 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.XOffsetPercentage);

        AimConfig.AddSlider(Locale.EMASmoothening, Locale.Amount, 0.01, 0.01, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.EMASmoothening);

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
        AntiRecoil.AddSlider(Locale.HoldTime, Locale.Milliseconds, 1, 1, 1, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.HoldTime);
        AntiRecoil.AddButton(Locale.RecordFireRate).Reader.Click += (s, e) => new SetAntiRecoil(this).Show();
        AntiRecoil.AddSlider(Locale.FireRate, Locale.Milliseconds, 1, 1, 1, 5000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.FireRate);
        AntiRecoil.AddSlider(Locale.YRecoilUpDown, Locale.Move, 1, 1, -1000, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.YRecoil);
        AntiRecoil.AddSlider(Locale.XRecoilLeftRight, Locale.Move, 1, 1, -1000, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.XRecoil);
        AntiRecoil.AddSeparator();
        AntiRecoil.Visibility = GetVisibilityFor(nameof(AntiRecoil));

        #endregion Anti Recoil

        #region Anti Recoil Config

        // Anti-Recoil Config
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
        ButtonGamepadSettings.Foreground = !string.IsNullOrWhiteSpace(error) || !GamepadManager.CanSend ? Brushes.Red : Brushes.White;
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
            
            GamepadSettingsConfig.AddButton("Open Gamepad Test Window", b =>
            {
                b.Reader.Click += (s, e) =>
                {
                    var testWindow = new GamepadTestWindow();
                    testWindow.Show();
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
                        dialog.TitleLabel.Content = text;
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

        UISettings.AddDropdown(Locale.Theme, ApplicationConstants.Theme, ThemePalette.All, palette =>
        {
            ApplicationConstants.Theme = palette;
            if (Config != null)
                Config.ThemeName = palette.Name;
        });
        var themeOnActive = ThemePalette.ThemeForActive;
        UISettings.AddDropdown(Locale.ThemeWhenActive, themeOnActive, ThemePalette.All, palette =>
        {
            if (Config != null)
                Config.ActiveThemeName = palette.Name;
        });

        UISettings.AddToggle(Locale.MouseBackgroundEffect).BindTo(() => AppConfig.Current.ToggleState.MouseBackgroundEffect);
        UISettings.AddToggle(Locale.UITopMost).BindTo(() => AppConfig.Current.ToggleState.UITopMost);
        UISettings.AddToggle("Locale.ShowHelpTexts").BindTo(() => AppConfig.Current.ToggleState.ShowHelpTexts);
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
        try
        {
            Task models = FileManager.RetrieveAndAddFiles(
                $"https://api.github.com/repos/{ApplicationConstants.RepoOwner}/{ApplicationConstants.RepoName}/contents/models",
                "bin\\models", _availableModels);
            Task configs = FileManager.RetrieveAndAddFiles(
                $"https://api.github.com/repos/{ApplicationConstants.RepoOwner}/{ApplicationConstants.RepoName}/contents/configs",
                "bin\\configs", _availableConfigs);

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

    private void DownloadGateway(StackPanel Scroller, HashSet<string> entries, string folder)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Scroller.Children.Clear();

            if (entries.Count > 0)
            {
                foreach (var entry in entries)
                {
                    ADownloadGateway gateway = new(entry, folder);
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
            MessageBox.Show($"{Locale.ModelSuggestionText}:\n" + AppConfig.Current.SuggestedModelName, Locale.SuggestedModel);
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
                var res = MessageBox.Show(Locale.ConfirmModelDelete.FormatWith(model), Locale.DeleteModel,
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (res == MessageBoxResult.No)
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
                var res = MessageBox.Show(Locale.ConfirmConfigDelete.FormatWith(cfg), Locale.DeleteConfig,
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (res == MessageBoxResult.No)
                    return;
            }
            File.Delete(path);
        }
    }
}