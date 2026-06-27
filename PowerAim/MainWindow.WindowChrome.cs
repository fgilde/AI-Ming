using Nextended.Core.Extensions;
using PowerAim.Localizations;
using PowerAim.UILibrary;
using PowerAim.Visuality;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;

namespace PowerAim;

public partial class MainWindow
{
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
        if (GpuPickerButton is not null)
        {
            _gpuPicker ??= new GpuPickerController(GpuPickerButton, GpuPickerLabel, GpuPickerPopup, GpuPickerList, () => LoadModel());
            _gpuPicker.Initialize();
        }
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
                new global::PowerAim.Visuality.NoticeBar(Locale.ResolveExePathFailed, 4000).Show();
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
            new global::PowerAim.Visuality.NoticeBar(Locale.UacDeclined, 3000).Show();
        }
        catch (Exception ex)
        {
            new global::PowerAim.Visuality.NoticeBar(Locale.RestartAsAdminFailedFormat.FormatWith(ex.Message), 5000).Show();
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
}
