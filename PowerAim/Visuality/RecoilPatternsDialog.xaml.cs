using PowerAim.AILogic;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PowerAim.Visuality;

/// <summary>
///     Master/detail UI for the recoil-pattern library. Lets the user record new patterns from a
///     live game, edit metadata, preview the recorded drift curve, and arm a pattern for playback.
///     <para>
///     Recording suspends <c>GlobalActive</c> so the aim pipeline doesn't fight the user while
///     they spray the wall; it is restored after the recording window closes. Cancellation is
///     cooperative — clicking the record button again during a recording aborts it.
///     </para>
/// </summary>
public partial class RecoilPatternsDialog
{
    /// <summary>Seconds of "get ready" countdown before recording actually begins.</summary>
    private const int StartCountdownSeconds = 5;

    private RecoilPattern? _selected;
    private readonly Dictionary<RecoilPattern, Border> _rowByPattern = new();
    private CancellationTokenSource? _recordCts;
    private bool _suppressFieldUpdates;
    private bool _restoredGlobalActive;

    public RecoilPatternsDialog()
    {
        InitializeComponent();
        DataContext = this;
        var settings = AppConfig.Current?.AntiRecoilSettings;
        if (settings is not null)
        {
            EnablePlaybackBox.IsChecked = settings.UsePatternRecoil;
            StrengthSlider.Value = settings.PatternStrength;
            StrengthText.Text = settings.PatternStrength.ToString("0.00");
        }

        RebuildList();
        UpdateDetailPanel();
        UpdatePreview();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }

    // ---------- list ----------

    private void RebuildList()
    {
        PatternItems.Items.Clear();
        _rowByPattern.Clear();
        var patterns = AppConfig.Current?.AntiRecoilSettings?.Patterns;
        if (patterns is null) return;

        foreach (var p in patterns)
        {
            var row = BuildRow(p);
            _rowByPattern[p] = row;
            PatternItems.Items.Add(row);
        }
        UpdateRowHighlights();
    }

    private Border BuildRow(RecoilPattern p)
    {
        var border = new Border
        {
            BorderBrush = TryFindResource("FluentStroke") as Brush ?? Brushes.DimGray,
            BorderThickness = new(1),
            CornerRadius = new(4),
            Background = TryFindResource("FluentSurface3") as Brush ?? Brushes.Black,
            Padding = new(10, 8, 10, 8),
            Margin = new(0, 0, 0, 4),
            Cursor = Cursors.Hand,
            Tag = p
        };
        border.MouseLeftButtonDown += (_, _) => Select(p);

        var sp = new StackPanel();
        var nameLine = new TextBlock
        {
            FontFamily = new("Segoe UI Variable Text"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = TryFindResource("FluentTextPrimary") as Brush ?? Brushes.White,
            Text = p.Name
        };
        var subLine = new TextBlock
        {
            FontFamily = new("Segoe UI Variable Small"),
            FontSize = 11,
            Foreground = TryFindResource("FluentTextSecondary") as Brush ?? Brushes.Gray,
            Text = string.IsNullOrEmpty(p.Weapon)
                ? $"{p.Samples.Count} samples · {p.DurationMs} ms"
                : $"{p.Weapon} · {p.Samples.Count} samples · {p.DurationMs} ms"
        };
        sp.Children.Add(nameLine);
        sp.Children.Add(subLine);
        border.Child = sp;
        return border;
    }

    private void UpdateRowHighlights()
    {
        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
        var stroke = TryFindResource("FluentStroke") as Brush ?? Brushes.DimGray;
        var activeName = AppConfig.Current?.AntiRecoilSettings?.ActivePatternName ?? "";
        foreach (var kv in _rowByPattern)
        {
            bool isSelected = ReferenceEquals(kv.Key, _selected);
            bool isActive = kv.Key.Name == activeName && !string.IsNullOrEmpty(activeName);
            kv.Value.BorderBrush = isSelected ? accent : (isActive ? accent : stroke);
            kv.Value.BorderThickness = new(isSelected || isActive ? 2 : 1);
        }
    }

    private void Select(RecoilPattern p)
    {
        _selected = p;
        // Selection also arms the pattern for playback — single click = use this one.
        var settings = AppConfig.Current?.AntiRecoilSettings;
        if (settings is not null) settings.ActivePatternName = p.Name;

        UpdateRowHighlights();
        UpdateDetailPanel();
        UpdatePreview();
    }

    // ---------- detail ----------

    private void UpdateDetailPanel()
    {
        _suppressFieldUpdates = true;
        try
        {
            if (_selected is null)
            {
                NameBox.Text = "";
                WeaponBox.Text = "";
                NameBox.IsEnabled = WeaponBox.IsEnabled = DeleteButton.IsEnabled = false;
                DurationText.Text = Locale.NoPatternSelected;
            }
            else
            {
                NameBox.Text = _selected.Name;
                WeaponBox.Text = _selected.Weapon;
                NameBox.IsEnabled = WeaponBox.IsEnabled = DeleteButton.IsEnabled = true;
                DurationText.Text = string.Format(Locale.PatternSamplesDurationFormat, _selected.Samples.Count, _selected.DurationMs);
            }
        }
        finally
        {
            _suppressFieldUpdates = false;
        }
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFieldUpdates || _selected is null) return;
        var settings = AppConfig.Current?.AntiRecoilSettings;
        string oldName = _selected.Name;
        _selected.Name = NameBox.Text;
        if (settings is not null && settings.ActivePatternName == oldName)
            settings.ActivePatternName = NameBox.Text;
        // Refresh row label without rebuilding the whole list.
        if (_rowByPattern.TryGetValue(_selected, out var row) && row.Child is StackPanel sp
            && sp.Children.Count > 0 && sp.Children[0] is TextBlock nameLabel)
        {
            nameLabel.Text = _selected.Name;
        }
    }

    private void WeaponBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFieldUpdates || _selected is null) return;
        _selected.Weapon = WeaponBox.Text;
        if (_rowByPattern.TryGetValue(_selected, out var row) && row.Child is StackPanel sp
            && sp.Children.Count > 1 && sp.Children[1] is TextBlock subLabel)
        {
            subLabel.Text = string.IsNullOrEmpty(_selected.Weapon)
                ? $"{_selected.Samples.Count} samples · {_selected.DurationMs} ms"
                : $"{_selected.Weapon} · {_selected.Samples.Count} samples · {_selected.DurationMs} ms";
        }
    }

    // ---------- preview ----------

    private void UpdatePreview()
    {
        PreviewCanvas.Children.Clear();
        if (_selected is null || _selected.Samples.Count < 2) return;

        double w = PreviewCanvas.ActualWidth > 0 ? PreviewCanvas.ActualWidth : 480;
        double h = PreviewCanvas.ActualHeight > 0 ? PreviewCanvas.ActualHeight : 220;
        if (w <= 0 || h <= 0) { Dispatcher.BeginInvoke(new Action(UpdatePreview)); return; }

        double maxAbs = 1;
        foreach (var s in _selected.Samples)
        {
            maxAbs = Math.Max(maxAbs, Math.Abs(s.DeltaX));
            maxAbs = Math.Max(maxAbs, Math.Abs(s.DeltaY));
        }
        // 10% padding around the extreme.
        double scaleY = (h / 2.0) * 0.92 / maxAbs;
        double scaleX = w / Math.Max(1, _selected.DurationMs);
        double cy = h / 2.0;

        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
        var secondary = new SolidColorBrush(Color.FromArgb(200, 130, 130, 200));

        // Zero line.
        var zero = new Line
        {
            X1 = 0, X2 = w, Y1 = cy, Y2 = cy,
            Stroke = new SolidColorBrush(Color.FromArgb(60, 200, 200, 200)),
            StrokeThickness = 1,
            StrokeDashArray = [4, 4]
        };
        PreviewCanvas.Children.Add(zero);

        // Y curve (vertical recoil)
        for (int i = 1; i < _selected.Samples.Count; i++)
        {
            var a = _selected.Samples[i - 1];
            var b = _selected.Samples[i];
            PreviewCanvas.Children.Add(new Line
            {
                X1 = a.TimeMs * scaleX, X2 = b.TimeMs * scaleX,
                Y1 = cy - a.DeltaY * scaleY, Y2 = cy - b.DeltaY * scaleY,
                Stroke = accent, StrokeThickness = 2
            });
            PreviewCanvas.Children.Add(new Line
            {
                X1 = a.TimeMs * scaleX, X2 = b.TimeMs * scaleX,
                Y1 = cy - a.DeltaX * scaleY, Y2 = cy - b.DeltaX * scaleY,
                Stroke = secondary, StrokeThickness = 1
            });
        }

        // Legend
        var legend = new TextBlock
        {
            Foreground = TryFindResource("FluentTextTertiary") as Brush ?? Brushes.Gray,
            FontFamily = new("Segoe UI Variable Small"),
            FontSize = 10,
            Text = Locale.RecoilPreviewLegend
        };
        Canvas.SetLeft(legend, 6);
        Canvas.SetTop(legend, 4);
        PreviewCanvas.Children.Add(legend);
    }

    // ---------- buttons ----------

    private async void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_recordCts is not null)
        {
            _recordCts.Cancel();
            return;
        }

        var capture = AIManager.Instance?.ImageCapture;
        if (capture is null)
        {
            StatusText.Text = Locale.NoCaptureSourceLoadModelFirst;
            return;
        }

        var toggle = AppConfig.Current?.ToggleState;
        bool wasActive = toggle?.GlobalActive == true;
        if (wasActive && toggle is not null)
        {
            toggle.GlobalActive = false;
            _restoredGlobalActive = true;
        }

        _recordCts = new();
        RecordButton.Content = Locale.RecordingClickToCancel;

        var progress = new Progress<double>(v =>
            StatusText.Text = string.Format(Locale.RecordingProgressFormat, (int)(v * 100)));

        // Build an in-flight RecoilPattern so we can plot it live as samples roll in. The
        // recorder appends each new sample via sampleProgress; we re-render the preview canvas
        // on every report so the user actually *sees* the curve being drawn during the 2s.
        var inFlight = new RecoilPattern { Name = Locale.Recording };
        _selected = inFlight;
        UpdatePreview();
        var sampleProgress = new Progress<RecoilSample>(sample =>
        {
            inFlight.Samples.Add(sample);
            UpdatePreview();
        });

        try
        {
            // Give the user time to switch to the game, aim at a wall and start spraying before we
            // begin sampling. The countdown is shown live in the status line.
            if (!await CountdownAsync(StartCountdownSeconds,
                    s => StatusText.Text = string.Format(Locale.RecordingCountdownFormat, s), _recordCts.Token))
            {
                StatusText.Text = Locale.Cancelled;
                return;
            }
            StatusText.Text = Locale.RecordingFireWeapon;

            var name = string.Format(Locale.PatternDefaultNameFormat, (AppConfig.Current?.AntiRecoilSettings?.Patterns?.Count ?? 0) + 1);
            var pattern = await RecoilPatternRecorder.RecordAsync(
                capture,
                durationMs: 2000,
                name: name,
                weapon: "",
                progress: progress,
                sampleProgress: sampleProgress,
                cancellation: _recordCts.Token);

            if (pattern.Samples.Count == 0)
            {
                StatusText.Text = Locale.RecordingNoMotion;
            }
            else
            {
                AppConfig.Current!.AntiRecoilSettings.Patterns.Add(pattern);
                RebuildList();
                Select(pattern);
                StatusText.Text = string.Format(Locale.RecordingDoneFormat, pattern.Samples.Count, pattern.DurationMs);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Format(Locale.RecordingFailedFormat, ex.Message);
        }
        finally
        {
            if (_restoredGlobalActive && toggle is not null)
            {
                toggle.GlobalActive = true;
                _restoredGlobalActive = false;
            }
            _recordCts?.Dispose();
            _recordCts = null;
            RecordButton.Content = Locale.RecordPattern2s;
        }
    }

    /// <summary>
    ///     Counts down <paramref name="seconds"/> seconds, calling <paramref name="show"/> once per
    ///     second with the remaining count. Returns <c>false</c> if cancelled, <c>true</c> if it ran
    ///     to completion.
    /// </summary>
    private static async Task<bool> CountdownAsync(int seconds, Action<int> show, CancellationToken ct)
    {
        for (int s = seconds; s > 0; s--)
        {
            show(s);
            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) { return false; }
        }
        return !ct.IsCancellationRequested;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var settings = AppConfig.Current?.AntiRecoilSettings;
        if (settings is null) return;

        bool wasActive = settings.ActivePatternName == _selected.Name;
        settings.Patterns.Remove(_selected);
        if (wasActive) settings.ActivePatternName = "";
        _selected = null;
        RebuildList();
        UpdateDetailPanel();
        UpdatePreview();
    }

    private void EnablePlayback_Toggle(object sender, RoutedEventArgs e)
    {
        var settings = AppConfig.Current?.AntiRecoilSettings;
        if (settings is null) return;
        settings.UsePatternRecoil = EnablePlaybackBox.IsChecked == true;
    }

    private void Strength_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var settings = AppConfig.Current?.AntiRecoilSettings;
        if (settings is null) return;
        settings.PatternStrength = e.NewValue;
        StrengthText.Text = e.NewValue.ToString("0.00");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _recordCts?.Cancel();
        Close();
    }

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _recordCts?.Cancel();
        if (_restoredGlobalActive && AppConfig.Current?.ToggleState is not null)
        {
            AppConfig.Current.ToggleState.GlobalActive = true;
            _restoredGlobalActive = false;
        }
        base.OnClosed(e);
    }
}
