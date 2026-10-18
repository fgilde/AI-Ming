using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.Other;

namespace PowerAim.UI.Visuality;

public partial class DebugLogPanel : Window
{
    private static DebugLogPanel? _instance;
    private readonly List<AppDebugLog.Entry> _all = new(4096);
    private readonly StringBuilder _buf = new(128 * 1024);
    private const int UiCapChars = 500_000;
    private System.Windows.Threading.DispatcherTimer? _healthTimer;

    public DebugLogPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        RefreshEnabledUi();
    }

    public static void ShowSingleton(Window? owner = null)
    {
        if (_instance != null)
        {
            if (_instance.WindowState == WindowState.Minimized)
                _instance.WindowState = WindowState.Normal;
            _instance.Activate();
            return;
        }
        _instance = new DebugLogPanel();
        if (owner != null) _instance.Owner = owner;
        _instance.Show();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        foreach (var entry in AppDebugLog.Snapshot())
            _all.Add(entry);
        RebuildLogView();
        AppDebugLog.EntryAdded += OnEntry;
        TxtStatus.Text = AppDebugLog.CurrentLogFile ?? AppDebugLog.LogDirectory;
        RefreshHealth();
        _healthTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _healthTimer.Tick += (_, _) =>
        {
            if (Tabs.SelectedIndex == 1) RefreshHealth();
        };
        _healthTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        AppDebugLog.EntryAdded -= OnEntry;
        _healthTimer?.Stop();
        if (ReferenceEquals(_instance, this))
            _instance = null;
    }

    private void OnEntry(AppDebugLog.Entry entry)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _all.Add(entry);
            if (_all.Count > 6000)
                _all.RemoveRange(0, _all.Count - 4000);
            if (PassesFilter(entry))
                AppendLine(entry, scroll: ChkAutoScroll.IsChecked == true);
            UpdateCounts();
        }));
    }

    private bool PassesFilter(AppDebugLog.Entry e)
    {
        if (ChkErrorsOnly.IsChecked == true && e.Level < AppDebugLog.Level.Warn)
            return false;
        var f = FilterBox.Text?.Trim();
        if (string.IsNullOrEmpty(f)) return true;
        return e.Category.Contains(f, StringComparison.OrdinalIgnoreCase)
               || e.Message.Contains(f, StringComparison.OrdinalIgnoreCase)
               || (e.Detail?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)
               || e.Level.ToString().Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildLogView()
    {
        _buf.Clear();
        LogView.Text = "";
        foreach (var e in _all)
        {
            if (!PassesFilter(e)) continue;
            AppendLine(e, scroll: false);
        }
        if (ChkAutoScroll.IsChecked == true)
        {
            LogView.CaretIndex = LogView.Text.Length;
            LogView.ScrollToEnd();
        }
        UpdateCounts();
    }

    private void AppendLine(AppDebugLog.Entry e, bool scroll)
    {
        var line = $"{e.Utc:HH:mm:ss.fff} [{e.Level.ToString().ToUpperInvariant(),-5}] [{e.Category}] {e.Message}\n";
        if (e.Detail != null)
            line += "  " + e.Detail.Replace("\n", "\n  ") + "\n";
        _buf.Append(line);
        if (_buf.Length > UiCapChars)
            _buf.Remove(0, _buf.Length - UiCapChars / 2);
        LogView.Text = _buf.ToString();
        if (scroll)
        {
            LogView.CaretIndex = LogView.Text.Length;
            LogView.ScrollToEnd();
        }
    }

    private void UpdateCounts()
    {
        var err = _all.Count(x => x.Level >= AppDebugLog.Level.Error);
        var warn = _all.Count(x => x.Level == AppDebugLog.Level.Warn);
        TxtCounts.Text = $"{_all.Count} events  |  {err} errors  |  {warn} warnings";
    }

    private void RefreshHealth()
    {
        var items = AppDebugLog.CaptureHealth();
        HealthGrid.ItemsSource = items;
        var errors = items.Count(x => x.Status is "ERROR" or "NULL");
        var warns = items.Count(x => x.Status == "WARN");
        HealthSummary.Text = errors > 0
            ? $"⚠ {errors} problem(s), {warns} warning(s) — check red/error rows"
            : warns > 0
                ? $"✓ Core OK — {warns} warning(s)"
                : "✓ All checks OK";
        HealthSummary.Foreground = errors > 0
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
            : warns > 0
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07))
                : new SolidColorBrush(Color.FromRgb(0x69, 0xF0, 0xAE));

        // Features tab: toggles only
        var sb = new StringBuilder();
        sb.AppendLine("FEATURE MAP (ToggleState) — ON means active / wired if subsystem is loaded");
        sb.AppendLine(new string('-', 72));
        foreach (var i in items.Where(x => x.Area == "Toggle"))
            sb.AppendLine($"{(i.Value == "ON" ? "●" : "○")}  {i.Name,-32} {i.Value}");
        sb.AppendLine();
        sb.AppendLine("AI / MODEL");
        sb.AppendLine(new string('-', 72));
        foreach (var i in items.Where(x => x.Area is "AI" or "Model"))
            sb.AppendLine($"[{i.Status,-5}] {i.Name,-28} {i.Value}" +
                          (string.IsNullOrEmpty(i.Detail) ? "" : "  — " + i.Detail));
        FeaturesView.Text = sb.ToString();
    }

    private void RefreshEnabledUi()
    {
        BtnEnabled.IsChecked = AppDebugLog.Enabled;
        BtnEnabled.Content = AppDebugLog.Enabled ? "Logging ON" : "Logging OFF";
    }

    private void BtnEnabled_Click(object sender, RoutedEventArgs e)
    {
        var on = BtnEnabled.IsChecked == true;
        if (AppConfig.Current?.ToggleState != null)
            AppConfig.Current.ToggleState.EnableDebugLogging = on;
        else
            AppDebugLog.Enable(on, "DebugLogPanel");
        RefreshEnabledUi();
        if (on) RefreshHealth();
    }

    private void BtnHealth_Click(object sender, RoutedEventArgs e)
    {
        RefreshHealth();
        Tabs.SelectedIndex = 1;
        AppDebugLog.Info("Health", "Manual health refresh");
        foreach (var line in AppDebugLog.CaptureHealth()
                     .Select(i => $"{i.Status,-5} | {i.Area}/{i.Name} = {i.Value}"))
            AppDebugLog.Write(AppDebugLog.Level.Info, "Health", line, null);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        AppDebugLog.ClearMemory();
        _all.Clear();
        _buf.Clear();
        LogView.Text = "";
        UpdateCounts();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = Tabs.SelectedIndex switch
            {
                1 => AppDebugLog.FormatHealthReport(AppDebugLog.CaptureHealth()),
                2 => FeaturesView.Text,
                _ => LogView.Text
            };
            Clipboard.SetText(text ?? "");
        }
        catch { }
    }

    private void BtnExportHealth_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = AppDebugLog.FormatHealthReport(AppDebugLog.CaptureHealth());
            var path = System.IO.Path.Combine(AppDebugLog.LogDirectory,
                $"health-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            System.IO.File.WriteAllText(path, report);
            AppDebugLog.Info("Health", "Exported " + path);
            TxtStatus.Text = path;
            MessageBox.Show("Saved:\n" + path, "Export Health", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export Health", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppDebugLog.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open log folder", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnFlush_Click(object sender, RoutedEventArgs e)
    {
        AppDebugLog.Flush();
        TxtStatus.Text = AppDebugLog.CurrentLogFile ?? AppDebugLog.LogDirectory;
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => RebuildLogView();
    private void FilterChanged(object sender, RoutedEventArgs e) => RebuildLogView();
}
