using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using PowerAim.AILogic;
using PowerAim.AILogic.Contracts;
using PowerAim.Class;
using PowerAim.Class.Native;

namespace PowerAim.Visuality;

/// <summary>
///     Topmost, click-through, transparent overlay that reads from
///     <see cref="SessionStats.Instance"/> and displays live inference / detection / strategic
///     stats in the corner of the screen. Useful for tuning models, watching for stalls, and
///     verifying that AutoPlay's intent layer is producing values.
///     <para>
///     Singleton via <see cref="ShowOrHide"/>; the toggle is bound to
///     <see cref="Config.ToggleState.ShowDebugOverlay"/>.
///     </para>
/// </summary>
public partial class DebugOverlay : Window
{
    private static DebugOverlay? _instance;
    private readonly DispatcherTimer _timer;
    private PowerAim.UILibrary.InputVisualizerPanel? _inputViz;

    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private System.ComponentModel.PropertyChangedEventHandler? _captureChangedHandler;

    public DebugOverlay()
    {
        InitializeComponent();
        _timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
        _timer.Tick += OnTick;
        // Follow the active capture source: monitor / process changes raise PropertyChanged on
        // ICapture.CaptureArea — reposition to the new top-left so the overlay never strands
        // itself on the wrong screen.
        _captureChangedHandler = (_, e) =>
        {
            if (e.PropertyName is nameof(ICapture.CaptureArea) or nameof(ICapture.Screen))
                Dispatcher.BeginInvoke(new Action(PositionOnActiveCapture));
        };
        Loaded += (_, _) => { SubscribeCapture(); PositionOnActiveCapture(); };
    }

    private void SubscribeCapture()
    {
        var cap = AIManager.Instance?.ImageCapture;
        if (cap != null && _captureChangedHandler != null) cap.PropertyChanged += _captureChangedHandler;
    }

    private void UnsubscribeCapture()
    {
        var cap = AIManager.Instance?.ImageCapture;
        if (cap != null && _captureChangedHandler != null) cap.PropertyChanged -= _captureChangedHandler;
    }

    /// <summary>
    ///     Park the overlay at the top-left of the active capture rectangle (the monitor or
    ///     process window <see cref="AIManager.ImageCapture"/> is reading from). 16-px margin.
    ///     Falls back to the primary screen when AIManager isn't ready yet.
    /// </summary>
    private void PositionOnActiveCapture()
    {
        var src = System.Windows.PresentationSource.FromVisual(this);
        double dpi = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        System.Drawing.Rectangle b;
        var cap = AIManager.Instance?.ImageCapture;
        if (cap != null && cap.CaptureArea.Width > 0 && cap.CaptureArea.Height > 0)
        {
            b = cap.CaptureArea;
        }
        else
        {
            var screen = Screen.PrimaryScreen;
            if (screen is null) { Left = 16; Top = 16; return; }
            b = screen.Bounds;
        }

        Left = (b.X / dpi) + 16;
        Top  = (b.Y / dpi) + 16;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Click-through + no taskbar entry + no activation focus stealing.
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        _timer.Start();
        this.HideForCaptureIfEnabled();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        UnsubscribeCapture();
        base.OnClosed(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var s = SessionStats.Instance;
        FpsText.Text     = s.InstantFps.ToString("0.0");
        MsText.Text      = s.LastInferenceMs.ToString("0.0") + " ms";
        DetText.Text     = s.LastDetectionCount.ToString();
        ShotsText.Text   = s.ShotsFired.ToString();
        IntentText.Text  = string.IsNullOrEmpty(s.LastIntent) ? "—" : s.LastIntent;
        ProfileText.Text = string.IsNullOrEmpty(s.ActiveProfileName) ? "—" : s.ActiveProfileName;
        ProcessText.Text = string.IsNullOrEmpty(WindowFocusWatcher.Instance.CurrentProcessName)
            ? "—"
            : WindowFocusWatcher.Instance.CurrentProcessName;

        UpdateOcrSection();
        UpdateAutoPlaySection();

        // Lazily add / remove the input visualizer. Adding the panel flips InputEventBus.Enabled on
        // (via the panel's Loaded handler); removing it turns the senders' reporting back off.
        bool showViz = PowerAim.Config.AppConfig.Current?.ToggleState?.ShowInputVisualizer == true;
        if (showViz && _inputViz == null)
        {
            InputHeader.Text = PowerAim.Locale.SentInput;
            _inputViz = new PowerAim.UILibrary.InputVisualizerPanel();
            InputHost.Content = _inputViz;
            InputSection.Visibility = Visibility.Visible;
        }
        else if (!showViz && _inputViz != null)
        {
            InputHost.Content = null;
            _inputViz = null;
            InputSection.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    ///     Rebuilds the OCR readout column next to the stats: one row per enabled OCR region,
    ///     showing the recognized text/value. Hidden entirely when OCR is off or there are no
    ///     enabled regions.
    /// </summary>
    private void UpdateOcrSection()
    {
        var settings = PowerAim.Config.AppConfig.Current?.OcrSettings;
        bool show = settings != null && settings.Enabled && settings.Regions.Any(r => r.Enabled);
        if (!show)
        {
            if (OcrSection.Visibility != Visibility.Collapsed) OcrSection.Visibility = Visibility.Collapsed;
            return;
        }
        OcrSection.Visibility = Visibility.Visible;

        var latest = PowerAim.AILogic.OcrService.Instance.Latest;
        var regions = settings!.Regions.Where(r => r.Enabled).ToList();

        // Resize the row set to match the region count, reusing existing TextBlocks so the timer
        // doesn't keep allocating + churning the visual tree every 120 ms.
        while (OcrGrid.RowDefinitions.Count < regions.Count)
        {
            OcrGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            int row = OcrGrid.RowDefinitions.Count - 1;
            var nameTb = new System.Windows.Controls.TextBlock { Style = (Style)FindResource("DebugLabel") };
            var valueTb = new System.Windows.Controls.TextBlock { Style = (Style)FindResource("DebugValue") };
            Grid.SetRow(nameTb, row); Grid.SetColumn(nameTb, 0);
            Grid.SetRow(valueTb, row); Grid.SetColumn(valueTb, 1);
            OcrGrid.Children.Add(nameTb);
            OcrGrid.Children.Add(valueTb);
        }
        while (OcrGrid.RowDefinitions.Count > regions.Count)
        {
            int last = OcrGrid.RowDefinitions.Count - 1;
            OcrGrid.RowDefinitions.RemoveAt(last);
            // Remove the two TextBlocks for that row (name + value, last two children).
            for (int k = 0; k < 2 && OcrGrid.Children.Count > 0; k++)
                OcrGrid.Children.RemoveAt(OcrGrid.Children.Count - 1);
        }

        for (int i = 0; i < regions.Count; i++)
        {
            var r = regions[i];
            // Children layout: row i has [name at 2i, value at 2i+1].
            var nameTb = (System.Windows.Controls.TextBlock)OcrGrid.Children[2 * i];
            var valueTb = (System.Windows.Controls.TextBlock)OcrGrid.Children[2 * i + 1];
            nameTb.Text = string.IsNullOrEmpty(r.Name) ? "—" : r.Name;
            valueTb.Text = latest.TryGetValue(r.Name ?? "", out var res) && !string.IsNullOrEmpty(res.Text)
                ? res.Text
                : "—";
        }
    }

    private const int AutoPlayLogRows = 8;

    /// <summary>
    ///     Mirrors the AutoPlay activity ring buffer
    ///     (<see cref="PowerAim.AILogic.Actions.AutoPlayGameAction.RecentEntries"/>) into the
    ///     right-hand column of the overlay. Hidden when AutoPlay is off; shows the last
    ///     <see cref="AutoPlayLogRows"/> entries (oldest at the top, newest at the bottom).
    /// </summary>
    private void UpdateAutoPlaySection()
    {
        bool show = PowerAim.Config.AppConfig.Current?.ToggleState?.AutoPlay == true;
        if (!show)
        {
            if (AutoPlaySection.Visibility != Visibility.Collapsed) AutoPlaySection.Visibility = Visibility.Collapsed;
            return;
        }
        AutoPlaySection.Visibility = Visibility.Visible;
        AutoPlayHeader.Text = PowerAim.Locale.DebugOverlayAutoPlayHeader;

        var entries = PowerAim.AILogic.Actions.AutoPlayGameAction.RecentEntries;
        // Take the trailing window so "oldest at top, newest at bottom" within the visible slice
        // matches the user's reading order.
        int take = Math.Min(entries.Count, AutoPlayLogRows);
        int skip = entries.Count - take;

        // Reuse TextBlocks across ticks — at 120 ms cadence churning the visual tree would be
        // wasteful. Grow/shrink the child collection to match the visible row count.
        while (AutoPlayLogStack.Children.Count < take)
        {
            AutoPlayLogStack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Style = (Style)FindResource("DebugValue"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 1, 0, 1),
            });
        }
        while (AutoPlayLogStack.Children.Count > take)
            AutoPlayLogStack.Children.RemoveAt(AutoPlayLogStack.Children.Count - 1);

        for (int i = 0; i < take; i++)
        {
            var e = entries[skip + i];
            var tb = (System.Windows.Controls.TextBlock)AutoPlayLogStack.Children[i];
            tb.Text = $"{e.At:HH:mm:ss} · {e.Category} · {e.Detail}";
        }
    }

    /// <summary>
    ///     Idempotent show/hide. Pass the visibility flag from the global toggle — when true the
    ///     overlay window is created (or focused) and ticks; when false it's closed and the
    ///     instance dropped so the next show creates a fresh one.
    /// </summary>
    public static void ShowOrHide(bool visible)
    {
        if (visible)
        {
            if (_instance is null)
            {
                _instance = new DebugOverlay();
                _instance.Closed += (_, _) => _instance = null;
            }
            if (!_instance.IsVisible) _instance.Show();
        }
        else
        {
            if (_instance is not null)
            {
                _instance.Close();
                _instance = null;
            }
        }
    }
}
