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
            Task models = FileManager.RetrieveAndMergeFromRepos(repos, Constants.ModelsBasePath, _availableModels);
            Task configs = FileManager.RetrieveAndMergeFromRepos(configRepos, Constants.ConfigBasePath, _availableConfigs);

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
}
