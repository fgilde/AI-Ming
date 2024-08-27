using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using Aimmy2.Class;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.HidHide;
using Aimmy2.Models;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.Types;
using Aimmy2.UILibrary;
using AimmyWPF.Class;
using AntWpf.Controls;
using Core;
using InputLogic;
using MdXaml;
using Microsoft.Xaml.Behaviors.Core;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Nextended.Core;
using Nextended.UI.Helper;
using Other;
using Visuality;
using WindowsInput;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using Panel = System.Windows.Controls.Panel;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace Aimmy2;

public partial class MainWindow
{
    #region Main Variables

    public static MainWindow? Instance => Application.Current.MainWindow as MainWindow;

    private bool _uiCreated;
    private FileManager fileManager;
    private static GithubManager githubManager = new();
    public AntiRecoilManager arManager = new();


    private bool CurrentlySwitching;
    private ScrollViewer? CurrentScrollViewer;

    private readonly HashSet<string> AvailableModels = new();
    private readonly HashSet<string> AvailableConfigs = new();

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
        Console.WriteLine("Init UI");
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
        Console.WriteLine("Init UI Complete");
    }
    
    private void CreateUI()
    {
        string? menu = null;
        var isRecreating = IsInitialized && _uiCreated;
        if (isRecreating && CurrentMenu != nameof(AimMenu))
        {
            menu = CurrentMenu;
            _= NavigateTo(nameof(AimMenu), false);
        }

        _uiCreated = false;
        Check.TryCatch<Exception>(GamepadManager.Init);
        
        var theme = ThemePalette.All.FirstOrDefault(x => x.Name == AppConfig.Current.ThemeName) ?? ThemePalette.PurplePalette;
        ApplicationConstants.Theme = theme;

        CurrentScrollViewer = FindName("AimMenu") as ScrollViewer;
        if (CurrentScrollViewer == null) throw new NullReferenceException("CurrentScrollViewer is null");

        FOV.Create();
        fileManager?.Dispose();
        fileManager = new FileManager(ModelListBox, SelectedModelNotifier, ConfigsListBox, SelectedConfigNotifier);

        // Needed to import annotations into MakeSense
        if (!File.Exists("bin\\labels\\labels.txt")) File.WriteAllText("bin\\labels\\labels.txt", "Enemy");

        arManager.HoldDownLoad();

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
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.AimKeybind), AppConfig.Current.BindingSettings.AimKeybind);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.TriggerKey), AppConfig.Current.BindingSettings.TriggerKey);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.TriggerAdditionalCommandKey), AppConfig.Current.BindingSettings.TriggerAdditionalCommandKey);
        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.RapidFireKey), AppConfig.Current.BindingSettings.RapidFireKey);

        BindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind), AppConfig.Current.BindingSettings.SecondAimKeybind);
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

    internal void FillMenus()
    {
        ModelContextMenu.Items.Clear();
        ModelContextMenu.Items.AddRange(ModelListBox.ToMenuItems(item =>
        {
            LoadModel(item.Header.ToString());
        },
        (i, item) => i <= 9 ? KeyGestureConvertHelper.CreateFromString($"Ctrl + Shift + {i}") : null));
        ModelContextMenu.Items.Add(new Separator());
        var downloadableMenu = new MenuItem()
        {
            Header = "Downloadable Models",
            Foreground =Brushes.Black
        };
        ModelContextMenu.Items.Add(downloadableMenu);
        downloadableMenu.Items.AddRange(AvailableModels.Select(s => new MenuItem()
        {
            Header = s,
            Foreground =Brushes.Black,
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
            _ = fileManager.LoadModel(Path.GetFileName(modelPath), modelPath);
        }
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
        TopCenterGrid.AddToggleWithKeyBind("Global Active", BindingManager, toggle =>
        {
            toggle.BindTo(() => AppConfig.Current.ToggleState.GlobalActive);
            toggle.Changed += (s, e) => SetActive(e.Value);
        }, border => border.Background = Brushes.Transparent);
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
        fileManager.Dispose();
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

    private string CurrentMenu = "AimMenu";

    private async void MenuSwitch(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton && !CurrentlySwitching)
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
        CurrentlySwitching = true;
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

        Animator.FadeOut(CurrentScrollViewer!);
        Animator.ObjectShift(TimeSpan.FromMilliseconds(duration), CurrentScrollViewer!, CurrentScrollViewer!.Margin,
            new Thickness(50, 450, 0, -400));
        await Task.Delay(350);

        CurrentScrollViewer.Visibility = Visibility.Collapsed;
        CurrentScrollViewer = movingScrollViewer;
        CurrentlySwitching = false;
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
                Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.FOVSize);
                Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.FOVSize);
                break;
            // Anti Recoil
            case nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind):
                if (AppConfig.Current.ToggleState.AntiRecoil)
                {
                    arManager.HoldDownTimer.Stop();
                    arManager.IndependentMousePress = 0;
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
                Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.ActualFovSize);
                Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.ActualFovSize);
                break;


            case nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind):
                if (AppConfig.Current.ToggleState.AntiRecoil)
                {
                    arManager.IndependentMousePress = 0;
                    arManager.HoldDownTimer.Start();
                }

                break;

            case nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind):
                if (AppConfig.Current.ToggleState.AntiRecoil)
                {
                    AppConfig.Current.ToggleState.AntiRecoil = false;
                    new NoticeBar("[Disable Anti Recoil Keybind] Disabled Anti-Recoil.", 4000).Show();
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
        RapidFire.RemoveAll();
        PredictionConfig.RemoveAll();
        AimConfig.RemoveAll();
        TriggerBot.RemoveAll();
        AntiRecoil.RemoveAll();
        ARConfig.RemoveAll();
        FOVConfig.RemoveAll();
        ESPConfig.RemoveAll();

        #region Aim Assist

        var keybind = AppConfig.Current.BindingSettings;
        AimAssist.AddTitle("Aim Assist", true);
        AimAssist.AddToggleWithKeyBind("Aim Assist", BindingManager).BindTo(() => AppConfig.Current.ToggleState.AimAssist).BindActiveStateColor(AimAssist);
        

        AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AimKeybind), () => keybind.AimKeybind, BindingManager);
        AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind), () => keybind.SecondAimKeybind, BindingManager);
        AimAssist.AddToggle("Constant AI Tracking").BindTo(() => AppConfig.Current.ToggleState.ConstantAITracking);

        AimAssist.AddToggle("Predictions").BindTo(() => AppConfig.Current.ToggleState.Predictions);
        AimAssist.AddToggle("EMA Smoothening").BindTo(() => AppConfig.Current.ToggleState.EMASmoothening);
        AimAssist.AddSeparator();
        AimAssist.Visibility = GetVisibilityFor(nameof(AimAssist));

        #endregion Aim Assist

        #region Model Settings

        ModelSettings.AddTitle("Model Settings");
        ModelSettings.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind), () => keybind.ModelSwitchKeybind, BindingManager);
        ModelSettings.AddCredit("",
            "With this Keybind you can toggle through each model\r\n" +
            "Additionally you can set a KeyBind for each model to load a specific one");
        ModelSettings.AddSeparator();

        #endregion

        #region Config Settings

        ConfigSettings.AddTitle("Config Settings");
        ConfigSettings.AddButton("Save Config").Reader.Click += (s, e) => new ConfigSaver().ShowDialog();
        ConfigSettings.AddSeparator();

        #endregion

        #region Config


        PredictionConfig.AddTitle("Prediction Config", true);
        PredictionConfig.AddDropdown("Prediction Method", AppConfig.Current.DropdownState.PredictionMethod, v => AppConfig.Current.DropdownState.PredictionMethod = v);
        PredictionConfig.AddDropdown("Detection Area Type",
            AppConfig.Current.DropdownState.DetectionAreaType, async v =>
            {
                AppConfig.Current.DropdownState.DetectionAreaType = v;
                if (v == DetectionAreaType.ClosestToCenter)
                {
                    await Task.Delay(100);
                    await FOV.Instance.UpdateStrictEnclosure();
                }
            });
        PredictionConfig.AddSeparator();
        PredictionConfig.Visibility = GetVisibilityFor(nameof(AimConfig));


        AimConfig.AddTitle("Aim Config", true);
 

        AimConfig.AddDropdown("Aiming Boundaries Alignment", AppConfig.Current.DropdownState.AimingBoundariesAlignment, v => AppConfig.Current.DropdownState.AimingBoundariesAlignment = v);
        AimConfig.AddSlider("Mouse Sensitivity (+/-)", "Sensitivity", 0.01, 0.01, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.MouseSensitivity);

        AimConfig.AddSlider("Mouse Jitter", "Jitter", 1, 1, 0, 15).BindTo(() => AppConfig.Current.SliderSettings.MouseJitter);

        AimConfig.AddSlider("Y Offset (Up/Down)", "Offset", 1, 1, -150, 150).BindTo(() => AppConfig.Current.SliderSettings.YOffset);
        AimConfig.AddSlider("Y Offset (%)", "Percent", 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.YOffsetPercentage);

        AimConfig.AddSlider("X Offset (Left/Right)", "Offset", 1, 1, -150, 150).BindTo(() => AppConfig.Current.SliderSettings.XOffset);
        AimConfig.AddSlider("X Offset (%)", "Percent", 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.XOffsetPercentage);

        AimConfig.AddSlider("EMA Smoothening", "Amount", 0.01, 0.01, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.EMASmoothening);

        AimConfig.AddSeparator();
        AimConfig.Visibility = GetVisibilityFor(nameof(AimConfig));

        #endregion Config

        #region Rapid

        RapidFire.AddTitle("Rapid Fire", true);
        RapidFire.AddToggleWithKeyBind("Rapid Fire", BindingManager).BindTo(() => AppConfig.Current.ToggleState.RapidFire).BindActiveStateColor(RapidFire);
        RapidFire.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.RapidFireKey), () => keybind.RapidFireKey, BindingManager);
        RapidFire.AddSeparator();
        RapidFire.Visibility = GetVisibilityFor(nameof(RapidFire));

        #endregion

        #region Trigger Bot

        TriggerBot.AddTitle("Auto Trigger", true);

        TriggerBot.AddToggleWithKeyBind("Auto Trigger", BindingManager).BindTo(() => AppConfig.Current.ToggleState.AutoTrigger).BindActiveStateColor(TriggerBot);
        
        TriggerBot.AddToggleWithKeyBind("Charge Mode", BindingManager, null, b => b.ToolTip = "If this is on, mouse will be clicked down when enemy is detected and trigger key is hold and then released when configured area is reached")
            .BindTo(() => AppConfig.Current.ToggleState.AutoTriggerCharged);
        

        TriggerBot.AddDropdown("Trigger Check", AppConfig.Current.DropdownState.TriggerCheck,
            check => AppConfig.Current.DropdownState.TriggerCheck = check);

        TriggerBot.AddButton("Configure Head Area", b =>
        {
            Config.DropdownState.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(Config.DropdownState.TriggerCheck))
                {
                    b.IsEnabled = Config.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter;
                }
            };
            b.IsEnabled = Config.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter;
            b.ToolTip = "Specify the area of the Head when this interaction center the trigger will be executed";
        }).Reader.Click += (s, e) =>
            new EditHeadArea(AppConfig.Current.DropdownState.HeadArea).Show();


        TriggerBot.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.TriggerKey), () => keybind.TriggerKey, BindingManager);
        TriggerBot.AddSlider("Min Time Trigger Key", "Seconds", 0.01, 0.1, 0.0, 5).InitWith(slider =>
        {
            Config.BindingSettings.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(Config.BindingSettings.TriggerKey))
                {
                    slider.IsEnabled = Config.BindingSettings.TriggerKey.IsValid;
                }
            };
            slider.IsEnabled = Config.BindingSettings.TriggerKey.IsValid;
            slider.ToolTip = "The minimum time the trigger key must be held down before the trigger is executed";
        }).BindTo(() => AppConfig.Current.SliderSettings.TriggerKeyMin);
        TriggerBot.AddSlider("Auto Trigger Delay", "Seconds", 0.01, 0.1, 0.00, 5).BindTo(() => AppConfig.Current.SliderSettings.AutoTriggerDelay);


        TriggerBot.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.TriggerAdditionalSend), () => keybind.TriggerAdditionalSend, BindingManager);

        TriggerBot.AddDropdown("Trigger Additional Command Check", AppConfig.Current.DropdownState.TriggerAdditionalCommandCheck,
            check => AppConfig.Current.DropdownState.TriggerAdditionalCommandCheck = check);
        TriggerBot.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.TriggerAdditionalCommandKey), () => keybind.TriggerAdditionalCommandKey, BindingManager);


        TriggerBot.AddSeparator();
        TriggerBot.Visibility = GetVisibilityFor(nameof(TriggerBot));

        #endregion Trigger Bot

        #region Anti Recoil

        AntiRecoil.AddTitle("Anti Recoil", true);
        AntiRecoil.AddToggleWithKeyBind("Anti Recoil", BindingManager).BindTo(() => AppConfig.Current.ToggleState.AntiRecoil).BindActiveStateColor(AntiRecoil);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind), () => keybind.AntiRecoilKeybind, BindingManager);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind), () => keybind.DisableAntiRecoilKeybind, BindingManager);
        AntiRecoil.AddSlider("Hold Time", "Milliseconds", 1, 1, 1, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.HoldTime);
        AntiRecoil.AddButton("Record Fire Rate").Reader.Click += (s, e) => new SetAntiRecoil(this).Show();
        AntiRecoil.AddSlider("Fire Rate", "Milliseconds", 1, 1, 1, 5000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.FireRate);
        AntiRecoil.AddSlider("Y Recoil (Up/Down)", "Move", 1, 1, -1000, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.YRecoil);
        AntiRecoil.AddSlider("X Recoil (Left/Right)", "Move", 1, 1, -1000, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.XRecoil);
        AntiRecoil.AddSeparator();
        AntiRecoil.Visibility = GetVisibilityFor(nameof(AntiRecoil));

        #endregion Anti Recoil

        #region Anti Recoil Config

        // Anti-Recoil Config
        ARConfig.AddTitle("Anti Recoil Config", true);
        ARConfig.AddToggleWithKeyBind("Enable Gun Switching Keybind", BindingManager).BindTo(() => AppConfig.Current.ToggleState.EnableGunSwitchingKeybind).BindActiveStateColor(ARConfig);
        ARConfig.AddButton("Save Anti Recoil Config").Reader.Click += (s, e) =>
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = $"{Directory.GetCurrentDirectory}\\bin\\anti_recoil_configs",
                Filter = "Aimmy Style Recoil Config (*.cfg)|*.cfg"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                AppConfig.Current.AntiRecoilSettings.Save<AntiRecoilSettings>(saveFileDialog.FileName);
                new NoticeBar($"[Anti Recoil] Config has been saved to \"{saveFileDialog.FileName}\"", 2000).Show();
            }
        };
        ARConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.Gun1Key), () => keybind.Gun1Key, BindingManager);
        ARConfig.AddFileLocator("Gun 1 Config", "Aimmy Style Recoil Config (*.cfg)|*.cfg", "\\bin\\anti_recoil_configs");
        ARConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.Gun2Key), () => keybind.Gun2Key, BindingManager);
        ARConfig.AddFileLocator("Gun 2 Config", "Aimmy Style Recoil Config (*.cfg)|*.cfg", "\\bin\\anti_recoil_configs");

        ARConfig.AddButton("Load Gun 1 Config").Reader.Click +=
            (s, e) => LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun1Config, true);
        ARConfig.AddButton("Load Gun 2 Config").Reader.Click +=
            (s, e) => LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun2Config, true);
        ARConfig.AddSeparator();
        ARConfig.Visibility = GetVisibilityFor(nameof(ARConfig));

        #endregion Anti Recoil Config

        #region FOV Config

        FOVConfig.AddTitle("FOV Config", true);
        FOVConfig.AddToggleWithKeyBind("FOV", BindingManager).BindTo(() => AppConfig.Current.ToggleState.FOV).BindActiveStateColor(FOVConfig);
        FOVConfig.AddToggleWithKeyBind("Dynamic FOV", BindingManager).BindTo(() => AppConfig.Current.ToggleState.DynamicFOV);
        FOVConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind), () => keybind.DynamicFOVKeybind, BindingManager);
        FOVConfig.AddColorChanger("FOV Color").BindTo(() => AppConfig.Current.ColorState.FOVColor);

        FOVConfig.AddSlider("FOV Size", "Size", 1, 1, 10, 640).BindTo(() => AppConfig.Current.SliderSettings.FOVSize);
        FOVConfig.AddSlider("Dynamic FOV Size", "Size", 1, 1, 10, 640).BindTo(() => AppConfig.Current.SliderSettings.DynamicFOVSize);
        FOVConfig.AddSlider("FOV Opacity", "FOV Opacity", 0.1, 0.1, 0, 1).BindTo(() => AppConfig.Current.SliderSettings.FOVOpacity);

        FOVConfig.AddSeparator();
        FOVConfig.Visibility = GetVisibilityFor(nameof(FOVConfig));

        #endregion FOV Config

        #region ESP Config

        ESPConfig.AddTitle("ESP Config", true);
        ESPConfig.AddToggleWithKeyBind("Show Detected Player", BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowDetectedPlayer).BindActiveStateColor(ESPConfig);
        ESPConfig.AddToggleWithKeyBind("Show Trigger Head Area", BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowTriggerHeadArea);
        ESPConfig.AddToggleWithKeyBind("Show AI Confidence", BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowAIConfidence);
        ESPConfig.AddToggleWithKeyBind("Show Tracers", BindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowTracers);

        ESPConfig.AddDropdown("Drawing Method", AppConfig.Current.DropdownState.OverlayDrawingMethod, v => AppConfig.Current.DropdownState.OverlayDrawingMethod = v);

        ESPConfig.AddColorChanger("Detected Player Color").BindTo(() => AppConfig.Current.ColorState.DetectedPlayerColor);


        ESPConfig.AddSlider("AI Confidence Font Size", "Size", 1, 1, 1, 30).BindTo(() => AppConfig.Current.SliderSettings.AIConfidenceFontSize);

        ESPConfig.AddSlider("Corner Radius", "Radius", 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.CornerRadius);

        ESPConfig.AddSlider("Border Thickness", "Thickness", 0.1, 1, 0.1, 10).BindTo(() => AppConfig.Current.SliderSettings.BorderThickness);

        ESPConfig.AddSlider("Opacity", "Opacity", 0.1, 0.1, 0, 1).BindTo(() => AppConfig.Current.SliderSettings.Opacity);

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
        GamepadSettingsConfig.AddTitle($"Gamepad Settings", false);

        GamepadSettingsConfig.AddDropdown("Gamepad Send Command mode", AppConfig.Current.DropdownState.GamepadSendMode, v =>
        {
            AppConfig.Current.DropdownState.GamepadSendMode = v;
            Reload();
        });

        if (!string.IsNullOrEmpty(error))
        {
            GamepadSettingsConfig.AddCredit("Status",
                "Error: " + error, credit => credit.Description.Foreground = Brushes.Red);
        }

        if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.None)
        {
            GamepadSettingsConfig.AddCredit("None",
                "If you don't need sending Gamepad Commands it make sense to disable it to decrease CPU usage");
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.ViGEm)
        {
            GamepadSettingsConfig.AddCredit("",
                "Virtual Gamepad with ViGEm\r\n" +
                "In order to use the Gamepad to send actions or AIM you need to ensure you have installed the ViGEm Bus Driver fom https://vigembusdriver.com/\r\n" +
                "Then a Virtual Xbox360 Controller will be created and your current controller will be synced with the virtual one. Please then ensure you use the virtual device in your game");
            if (!GamepadManager.CanSend)
            {
                GamepadSettingsConfig.AddButton("Goto vigembusdriver.com", b =>
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
                GamepadSettingsConfig.AddCredit("Status",
                    "GREAT, It looks like you have installed all necessary software for it.");
            }
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.VJoy)
        {
            GamepadSettingsConfig.AddCredit("",
                "Virtual Gamepad with vJoy\r\n" +
                "In order to use the Gamepad with vJoy you need to install vJoy from https://sourceforge.net/projects/vjoystick/\r\n" +
                "Then a Virtual Xbox360 Controller will be created and your current controller will be synced with the virtual one. Please then ensure you use the virtual device in your game");
            if (!GamepadManager.CanSend)
            {
                GamepadSettingsConfig.AddButton("Install vJoy", b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        var fileName = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "Resources", "vJoySetup.exe");
                        if (File.Exists(fileName))
                        {
                            var p = Process.Start(new ProcessStartInfo
                            {
                                FileName = fileName,
                            });
                            p.Exited += (sender, args) => Reload();
                        }
                    };
                });
            }
            else
            {
                GamepadSettingsConfig.AddCredit("Status",
                    "GREAT, It looks like you have installed all necessary software for it.");
            }
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.XInputHook)
        {
            GamepadSettingsConfig.AddCredit("Notice",
                "In order to use XInputEmulation you need to ensure this tool is started with Admin Privileges");
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

        GamepadSettingsConfig.AddSeparator();

        if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.VJoy ||
            AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.ViGEm)
        {
            GamepadSettingsConfig.AddTitle($"Hide Physical Controller", false);
            GamepadSettingsConfig.AddCredit("If you are not able to select the controller in your game you can hide it's HID",
                "With HidHide you can Hide your Physical Controller and we can do this automatically for you");

            GamepadSettingsConfig.AddToggle("Automatically hide and reactivate physical Controller", toggle =>
            {
                toggle.IsEnabled = File.Exists(HidHideHelper.GetHidHidePath());
                toggle.Changed += (s, e) => Reload();
            }).BindTo(() => AppConfig.Current.ToggleState.AutoHideController);

            GamepadSettingsConfig.AddFileLocator("HidHide Path", "HidHideCLI.exe (HidHideCLI.exe)|HidHideCLI.exe", HidHideHelper.GetHidHidePath(), cfg:
                locator =>
                {
                    locator.FileSelected += (sender, args) => Reload();
                });


            if (!File.Exists(HidHideHelper.GetHidHidePath()))
            {
                GamepadSettingsConfig.AddButton("Install HidHide", b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        var fileName =
                            Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                                "Resources", "HidHide_1.5.230_x64.exe");
                        if (File.Exists(fileName))
                        {
                            var p = Process.Start(fileName);
                            p.Exited += (sender, args) => Reload();
                        }
                    };
                });
            }
            else
            {
                GamepadSettingsConfig.AddButton("Launch HidHide UI", b =>
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

            GamepadSettingsConfig.AddButton("Show and Test Controller", b =>
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

    }

    private void LoadTools()
    {
        ToolsConfig.RemoveAll();
        ToolsConfig.AddTitle("Magnifier", true);
        ToolsConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.MagnifierKeybind), () => AppConfig.Current.BindingSettings.MagnifierKeybind, BindingManager);

        ToolsConfig.AddButton("Show Hide Magnifier").Reader.Click += (s, e) => ToggleMagnifier();
        
        ToolsConfig.AddSlider("Magnification value", "Zoom Factor", 0.1, 0.1, ApplicationConstants.MinMagnificationFactor, ApplicationConstants.MaxMagnificationFactor).BindTo(() => AppConfig.Current.SliderSettings.MagnificationFactor);
        ToolsConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.MagnifierZoomInKeybind), () => AppConfig.Current.BindingSettings.MagnifierZoomInKeybind, BindingManager);
        ToolsConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind), () => AppConfig.Current.BindingSettings.MagnifierZoomOutKeybind, BindingManager);

        ToolsConfig.AddSlider("Step ZoomIn/ZoomOut", "Step", 0.1, 0.1, 0.1, 4).BindTo(() => AppConfig.Current.SliderSettings.MagnificationStepFactor);

        ToolsConfig.AddSlider("Window Size (Width)", "Width", 1, 10, 50, 1500).BindTo(() => AppConfig.Current.SliderSettings.MagnifierWindowWidth);
        ToolsConfig.AddSlider("Window Size (Height)", "Height", 1, 10, 50, 1500).BindTo(() => AppConfig.Current.SliderSettings.MagnifierWindowHeight);


        ToolsConfig.AddSeparator();
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
        XYPercentageEnablerMenu.RemoveAll();
        SettingsConfig.RemoveAll();
        SettingsConfig.AddTitle("Settings Menu", true);

        SettingsConfig.AddDropdown("Theme", ApplicationConstants.Theme, ThemePalette.All, palette =>
        {
            ApplicationConstants.Theme = palette;
            if (Config != null)
                Config.ThemeName = palette.Name;
        });
        var themeOnActive = ThemePalette.ThemeForActive;
        SettingsConfig.AddDropdown("Theme when active", themeOnActive, ThemePalette.All, palette =>
        {
            if (Config != null)
                Config.ActiveThemeName = palette.Name;
        });

        SettingsConfig.AddToggle("Collect Data While Playing").BindTo(() => AppConfig.Current.ToggleState.CollectDataWhilePlaying);
        SettingsConfig.AddToggle("Auto Label Data").BindTo(() => AppConfig.Current.ToggleState.AutoLabelData);
        SettingsConfig.AddDropdown("Mouse Movement Method",
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

        SettingsConfig.AddSlider("AI Minimum Confidence", "% Confidence", 1, 1, 1, 100).BindTo(() => AppConfig.Current.SliderSettings.AIMinimumConfidence).Slider.PreviewMouseLeftButtonUp += (sender, e) =>
        {
            switch (AppConfig.Current.SliderSettings.AIMinimumConfidence)
            {
                case >= 95:
                    new NoticeBar(
                        "The minimum confidence you have set for Aimmy to be too high and may be unable to detect players.",
                        10000).Show();
                    break;
                case <= 35:
                    new NoticeBar("The minimum confidence you have set for Aimmy may be too low can cause false positives.",
                        10000).Show();
                    break;
            }
        };

        SettingsConfig.AddSlider("Gamepad Minimum LT", "LT", 0.1, 0.1, 0.1, 1).BindTo(() => AppConfig.Current.SliderSettings.GamepadMinimumLT);
        SettingsConfig.AddSlider("Gamepad Minimum RT", "RT", 0.1, 0.1, 0.1, 1).BindTo(() => AppConfig.Current.SliderSettings.GamepadMinimumRT);

        SettingsConfig.AddCredit(string.Empty, "When fire (click action) is executed a random value for waiting from press to release is used.\r\nHere you can control the maximum time for this random value");
        SettingsConfig.AddSlider("Fire max delay", "Seconds", 0.01, 0.1, 0.00, 2).BindTo(() => AppConfig.Current.SliderSettings.FirePressDelay);

        SettingsConfig.AddToggle("Mouse Background Effect").BindTo(() => AppConfig.Current.ToggleState.MouseBackgroundEffect);
        SettingsConfig.AddToggle("UI TopMost").BindTo(() => AppConfig.Current.ToggleState.UITopMost);
        
        SettingsConfig.AddSeparator();

        // X/Y Percentage Adjustment Enabler
        XYPercentageEnablerMenu.AddTitle("X/Y Percentage Adjustment", true);
        XYPercentageEnablerMenu.AddToggle("X Axis Percentage Adjustment").BindTo(() => AppConfig.Current.ToggleState.XAxisPercentageAdjustment);
        XYPercentageEnablerMenu.AddToggle("Y Axis Percentage Adjustment").BindTo(() => AppConfig.Current.ToggleState.YAxisPercentageAdjustment);
        XYPercentageEnablerMenu.AddSeparator();

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
                "bin\\models", AvailableModels);
            Task configs = FileManager.RetrieveAndAddFiles(
                $"https://api.github.com/repos/{ApplicationConstants.RepoOwner}/{ApplicationConstants.RepoName}/contents/configs",
                "bin\\configs", AvailableConfigs);

            await Task.WhenAll(models, configs);
        }
        catch (Exception e)
        {
            new NoticeBar(e.Message, 10000).Show();
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            DownloadGateway(ModelStoreScroller, AvailableModels, "models");
            DownloadGateway(ConfigStoreScroller, AvailableConfigs, "configs");
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
        if(path == AppConfig.Current.Path)
            return;
        AppConfig.Current.Save();
        Console.WriteLine("Loading Config: " + path);
        Config = AppConfig.Load(path);
        OnPropertyChanged(nameof(Config));

        if (!string.IsNullOrEmpty(AppConfig.Current.SuggestedModelName) && AppConfig.Current.SuggestedModelName != "N/A")
            MessageBox.Show("The creator of this model suggests you use this model:\n" + AppConfig.Current.SuggestedModelName, "Suggested Model - Aimmy");
        LoadModel();
    }

    #endregion Config Loader

    #region Anti Recoil Config Loader

    private void LoadAntiRecoilConfig(string path = "bin\\anti_recoil_configs\\Default.cfg",
        bool loading_outside_startup = false)
    {
        if(!string.IsNullOrEmpty(path)) {
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
            UpdateCheckStatusLabel.Content = "Checking for updates...";
            await Task.Delay(500);
            var updateManager = new UpdateManager();
            var hasUpdate = await updateManager.CheckForUpdate(ApplicationConstants.ApplicationVersion, ApplicationConstants.RepoOwner, ApplicationConstants.RepoName);
            UpdateCheckStatusLabel.Content = hasUpdate ? "Update available!" : "No update available";
            if (hasUpdate)
            {
                CheckForUpdates.Content = "Install Update";
            }
            if (!hasUpdate)
            {
                if(showNotice)
                    new NoticeBar("You are already on the latest version.", 5000).Show();
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
        var dlg = new SaveFileDialog() { Filter = "Configuration File (*.cfg) | *.cfg" };
        if (dlg.ShowDialog() == true)
        {
            AppConfig.Current.Save(dlg.FileName);
        }
    }

    private void OpenConfig_CLick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog() { Filter = "Configuration File (*.cfg) | *.cfg" };
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

    private void AKeyChanger_ModelOnGlobalKeyPressed(object? sender, EventArgs<(AKeyChanger Sender, string Key)> e)
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

    private void AKeyChanger_ConfigOnGlobalKeyPressed(object? sender, EventArgs<(AKeyChanger Sender, string Key)> e)
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
        Console.WriteLine("toggle " + show);
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
                var res = MessageBox.Show($"Are you sure you want to delete the model {model}?", "Delete Model",
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
                var res = MessageBox.Show($"Are you sure you want to delete the config {cfg}?", "Delete Config",
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (res == MessageBoxResult.No)
                    return;
            }
            File.Delete(path);
        }
    }
}