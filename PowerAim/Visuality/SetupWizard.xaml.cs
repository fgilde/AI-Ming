using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nextended.Core.Helper;
using PowerAim.AILogic;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.UILibrary;

namespace PowerAim.Visuality;

/// <summary>
///     First-run setup wizard. Walks the user through the bare-minimum config a working
///     installation needs: capture source → model → movement method → optional sensitivity
///     calibration → optional anti-recoil pattern. Each step is a sub-panel within this single
///     dialog; <see cref="_currentStep"/> drives visibility.
///     <para>
///     Trigger pattern mirrors <see cref="KnownIssuesDialog"/>: <see cref="ShowIfFirstRun"/> is
///     called from <c>MainWindow.Window_Loaded</c>, checks the persisted <see cref="WindowSettings.ShouldShow"/>
///     flag, and only opens on first run (or when the user explicitly invokes "Open Setup
///     Wizard" from the help / about page).
///     </para>
/// </summary>
public partial class SetupWizard : BaseDialog
{
    // Step count is hardcoded — keep in sync with the StepN_xxx StackPanels in XAML.
    private const int TotalSteps = 7;
    private int _currentStep;
    private readonly StackPanel[] _stepPanels;
    private readonly TextBlock[] _stepLabels;

    // Don't persist the window's restored position; the wizard always opens centred on the owner.
    protected override bool SaveRestorePosition => false;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }

    /// <summary>
    ///     Open the wizard if the user hasn't dismissed it yet (or <paramref name="force"/> is set).
    ///     Reads <see cref="BaseDialog.GetWindowSettings"/> which BaseDialog persists per dialog
    ///     type — a "don't show again" tick on the wizard flips it to false and the next call here
    ///     skips silently.
    /// </summary>
    public static void ShowIfFirstRun(Window? owner = null, bool force = false)
    {
        var wizard = new SetupWizard();
        var settings = wizard.GetWindowSettings();
        if (!force && settings?.ShouldShow == false)
        {
            return;
        }
        if (owner is not null) wizard.Owner = owner;
        wizard.Show();
    }

    public SetupWizard()
    {
        InitializeComponent();

        // Default-show on first run. Once the user un-ticks "Don't show again" (via the footer
        // checkbox) BaseDialog persists ShouldShow = false through WindowSettingsManager.
        Settings.ShouldShow ??= true;

        _stepPanels = new[]
        {
            Step0_Welcome,
            Step1_Capture,
            Step2_Model,
            Step3_Movement,
            Step4_Calibrate,
            Step5_Recoil,
            Step6_Done,
        };

        BuildStepIndicator(out _stepLabels);
        EmbedCaptureSourceSelect();
        BuildMovementMethodList();
        UpdateStatusLines();

        ShowStep(0);
        Loaded += (_, _) => UpdateStatusLines();
    }

    // ====================================================================== STEP INDICATOR ====

    /// <summary>
    ///     Build a row of breadcrumb chips so the user can see where they are in the flow. The
    ///     active step is rendered in the accent colour, completed steps in muted text, future
    ///     steps in tertiary. Updates on each ShowStep call.
    /// </summary>
    private void BuildStepIndicator(out TextBlock[] labels)
    {
        StepIndicator.Children.Clear();
        var titles = new[]
        {
            Locale.SetupStepWelcome,
            Locale.SetupStepCapture,
            Locale.SetupStepModel,
            Locale.SetupStepMovement,
            Locale.SetupStepCalibrate,
            Locale.SetupStepRecoil,
            Locale.SetupStepDone,
        };
        labels = new TextBlock[titles.Length];
        for (int i = 0; i < titles.Length; i++)
        {
            if (i > 0)
            {
                StepIndicator.Children.Add(new TextBlock
                {
                    Text = "›",
                    Margin = new Thickness(8, 0, 8, 0),
                    Foreground = (Brush)FindResource("FluentTextTertiary"),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13,
                });
            }
            var label = new TextBlock
            {
                Text = $"{i + 1}. {titles[i]}",
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("FluentTextTertiary"),
            };
            labels[i] = label;
            StepIndicator.Children.Add(label);
        }
    }

    private void UpdateStepIndicator()
    {
        if (_stepLabels == null) return;
        for (int i = 0; i < _stepLabels.Length; i++)
        {
            if (i < _currentStep)
            {
                _stepLabels[i].Foreground = (Brush)FindResource("FluentTextSecondary");
                _stepLabels[i].FontWeight = FontWeights.Normal;
            }
            else if (i == _currentStep)
            {
                _stepLabels[i].Foreground = (Brush)FindResource("FluentAccent");
                _stepLabels[i].FontWeight = FontWeights.SemiBold;
            }
            else
            {
                _stepLabels[i].Foreground = (Brush)FindResource("FluentTextTertiary");
                _stepLabels[i].FontWeight = FontWeights.Normal;
            }
        }
    }

    // ====================================================================== STEP CONTENT ====

    /// <summary>
    ///     Drop the existing <see cref="CaptureSourceSelect"/> control into the wizard so the
    ///     user can pick monitor / process inline — no need to alt-tab to the main window header.
    /// </summary>
    private void EmbedCaptureSourceSelect()
    {
        var picker = new CaptureSourceSelect { MinWidth = 360, Margin = new Thickness(0) };
        // Reuse the standard CaptureSourceSelect → no extra wiring needed; it updates AppConfig.
        picker.Selected += (_, _) => UpdateStatusLines();
        Step1_CaptureHost.Children.Add(picker);
    }

    /// <summary>
    ///     Build a simple radio-style movement picker for the three most common modes:
    ///     MouseEvent / SendInput / Gamepad. Less overwhelming than the full 6-option dropdown,
    ///     and consistent with what most users actually need first-time.
    /// </summary>
    private void BuildMovementMethodList()
    {
        var current = AppConfig.Current?.DropdownState?.MouseMovementMethod ?? MouseMovementMethod.MouseEvent;
        var choices = new (MouseMovementMethod method, string glyph, string explainKey)[]
        {
            (MouseMovementMethod.MouseEvent, "", "SetupMovementMouseDesc"),
            (MouseMovementMethod.SendInput,  "", "SetupMovementSendInputDesc"),
            (MouseMovementMethod.Gamepad,    "", "SetupMovementGamepadDesc"),
        };
        foreach (var (method, glyph, key) in choices)
        {
            var radio = new RadioButton
            {
                GroupName = "wizardMovementMethod",
                Margin = new Thickness(0, 4, 0, 4),
                IsChecked = method == current,
                Tag = method,
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 14,
                Width = 22,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("FluentTextSecondary"),
            });
            var textStack = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            textStack.Children.Add(new TextBlock
            {
                Text = method.ToDescriptionString(),
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("FluentTextPrimary"),
            });
            textStack.Children.Add(new TextBlock
            {
                Text = Locale.GetString(key),
                Foreground = (Brush)FindResource("FluentTextSecondary"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 600,
            });
            stack.Children.Add(textStack);
            radio.Content = stack;
            radio.Checked += MovementRadio_Checked;
            Step3_MovementHost.Children.Add(radio);
        }
    }

    private void MovementRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: MouseMovementMethod method }) return;
        if (AppConfig.Current?.DropdownState == null) return;

        // Gamepad pick but no virtual controller wired up → tell the user what's missing.
        // Mirrors the warning in MainWindow's full dropdown. We don't navigate from inside the
        // wizard (the wizard is modal-ish over the main window); just surface the info.
        if (method == MouseMovementMethod.Gamepad
            && !PowerAim.InputLogic.InputSender.GamepadAimAvailable)
        {
            MessageDialog.Show(
                Locale.GamepadNotReadyMessage,
                Locale.GamepadNotReady,
                MessageDialog.DialogButtons.OK,
                MessageDialog.DialogIcon.Warning,
                owner: this,
                defaultResult: MessageDialog.DialogResult.OK);
        }

        AppConfig.Current.DropdownState.MouseMovementMethod = method;
    }

    // ====================================================================== STATUS LINES ====

    /// <summary>
    ///     Light-touch "are we good?" hints under each step. Cheap to recompute on every step
    ///     transition; users get instant feedback on whether they can move on.
    /// </summary>
    private void UpdateStatusLines()
    {
        if (Step1_Status != null)
        {
            var src = AIManager.Instance?.ImageCapture;
            Step1_Status.Text = src == null
                ? Locale.SetupCaptureStatusNone
                : string.Format(Locale.SetupCaptureStatusFormat, $"{src.CaptureArea.Width}×{src.CaptureArea.Height}");
        }

        if (Step2_Status != null)
        {
            bool hasModel = !string.IsNullOrEmpty(AppConfig.Current?.LastLoadedModel)
                            && AppConfig.Current!.LastLoadedModel != "N/A";
            Step2_Status.Text = hasModel
                ? string.Format(Locale.SetupModelStatusFormat, Path.GetFileName(AppConfig.Current!.LastLoadedModel))
                : Locale.SetupModelStatusNone;
        }
    }

    // ====================================================================== STEP CONTROLS ====

    private void ShowStep(int index)
    {
        _currentStep = Math.Clamp(index, 0, TotalSteps - 1);
        for (int i = 0; i < _stepPanels.Length; i++)
            _stepPanels[i].Visibility = i == _currentStep ? Visibility.Visible : Visibility.Collapsed;

        BackButton.IsEnabled = _currentStep > 0;
        NextButton.Content = _currentStep == TotalSteps - 1 ? Locale.Finish : Locale.Next;

        UpdateStepIndicator();
        UpdateStatusLines();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowStep(_currentStep - 1);

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep >= TotalSteps - 1)
        {
            Close();
            return;
        }
        ShowStep(_currentStep + 1);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); } catch { /* DragMove during a Released event throws; ignore */ }
    }

    /// <summary>
    ///     Honour the footer "don't show again" tick: persist via the same
    ///     <see cref="WindowSettingsManager"/> KnownIssuesDialog uses, scoped by dialog type.
    /// </summary>
    private void DontShowAgain_Click(object sender, RoutedEventArgs e)
    {
        var isChecked = DontShowAgainCheckbox.IsChecked ?? true;
        // ShouldShow is the inverse of "don't show again": ticking the box (true) means
        // ShouldShow = false on the next launch.
        var settingsManager = new WindowSettingsManager(GetSettingsFilePath());
        settingsManager.SaveWindowSettings(this, !isChecked);
    }

    // ====================================================================== STEP HANDLERS ====

    private async void Step2_LoadDefault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // The bundled default ONNX is extracted/loaded by MainWindow's LoadDefaultModelAsync
            // — reuse it here so the wizard mirrors the no-model card's behaviour exactly.
            if (PowerAim.MainWindow.Instance is { } main)
            {
                await main.LoadDefaultModelAsync();
            }
        }
        catch (Exception ex)
        {
            MessageDialog.Show(ex.Message, Locale.Error,
                MessageDialog.DialogButtons.OK, MessageDialog.DialogIcon.Error, owner: this);
        }
        UpdateStatusLines();
    }

    private void Step2_OpenModels_Click(object sender, RoutedEventArgs e)
    {
        // ModelMenu lists the user's local models + the GitHub library. Jump there so the user
        // can pick something other than the default. We close the wizard so they can interact
        // with the main window freely — they can re-open the wizard from the help page later.
        if (PowerAim.MainWindow.Instance is { } main)
        {
            _ = main.NavigateTo("ModelMenu");
        }
        Close();
    }

    private void Step5_OpenPatterns_Click(object sender, RoutedEventArgs e)
    {
        new RecoilPatternsDialog { Owner = this }.ShowDialog();
    }
}
