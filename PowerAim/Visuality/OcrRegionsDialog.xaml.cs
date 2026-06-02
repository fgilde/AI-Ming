using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PowerAim.AILogic;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim;

namespace PowerAim.Visuality;

/// <summary>
///     UI for managing <see cref="OcrSettings.Regions"/>. Provides a master/detail editor, a live
///     preview that reads from <see cref="OcrService.Latest"/>, a one-click downloader for the
///     Tesseract training data, and a manual "Test" button that forces a single sample without
///     waiting for the polling tick.
/// </summary>
public partial class OcrRegionsDialog
{
    private OcrRegion? _selected;
    private readonly Dictionary<OcrRegion, Border> _rowByRegion = new();
    private bool _suppressFieldUpdates;
    private readonly DispatcherTimer _previewTimer;

    public OcrRegionsDialog()
    {
        InitializeComponent();
        // Without DataContext the {Binding Path=Texts ...} expressions resolve nothing and every
        // localized TextBlock in the XAML stays empty.
        DataContext = this;

        var settings = AppConfig.Current?.OcrSettings;
        if (settings is not null)
        {
            EngineEnabledBox.IsChecked = settings.Enabled;
            // Engine-options expander state — guard with _suppressFieldUpdates so the
            // Checked/Unchecked handlers don't fire while we're hydrating the UI from config.
            _suppressFieldUpdates = true;
            OptMaxChannelBox.IsChecked = settings.UseMaxChannelGrayscale;
            OptSubstituteBox.IsChecked = settings.SubstituteLettersToDigits;
            OptStrictParseBox.IsChecked = settings.StrictNumberParsing;
            OptDpiBox.IsChecked = settings.UseUserDefinedDpi;
            OptOtsuBox.IsChecked = settings.UseOtsuFallback;
            OptStickyMsBox.Text = settings.StickyLastValidMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _suppressFieldUpdates = false;
        }

        _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
        _previewTimer.Tick += (_, _) => UpdatePreviewFromService();
        _previewTimer.Start();

        OcrService.Instance.PropertyChanged += OnServicePropertyChanged;

        RebuildList();
        UpdateDetailPanel();
        UpdateStatus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }

    private void OnServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(UpdateStatus));
    }

    // ---------- list ----------

    private void RebuildList()
    {
        RegionItems.Items.Clear();
        _rowByRegion.Clear();
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        foreach (var r in settings.Regions)
        {
            var row = BuildRow(r);
            _rowByRegion[r] = row;
            RegionItems.Items.Add(row);
        }
        UpdateRowHighlights();
    }

    private Border BuildRow(OcrRegion r)
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
            Tag = r
        };
        border.MouseLeftButtonDown += (_, _) => Select(r);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var enabledBox = new CheckBox
        {
            IsChecked = r.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 0, 8, 0)
        };
        enabledBox.Checked   += (_, _) => r.Enabled = true;
        enabledBox.Unchecked += (_, _) => r.Enabled = false;
        Grid.SetColumn(enabledBox, 0);
        grid.Children.Add(enabledBox);

        var sp = new StackPanel();
        var nameLabel = new TextBlock
        {
            Text = r.Name,
            FontFamily = new("Segoe UI Variable Text"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = TryFindResource("FluentTextPrimary") as Brush ?? Brushes.White
        };
        var subLabel = new TextBlock
        {
            Text = $"{r.Kind}  ·  {r.X},{r.Y}  {r.Width}×{r.Height}",
            FontFamily = new("Segoe UI Variable Small"),
            FontSize = 11,
            Foreground = TryFindResource("FluentTextSecondary") as Brush ?? Brushes.Gray
        };
        sp.Children.Add(nameLabel);
        sp.Children.Add(subLabel);
        Grid.SetColumn(sp, 1);
        grid.Children.Add(sp);
        border.Child = grid;
        return border;
    }

    private void UpdateRowHighlights()
    {
        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
        var stroke = TryFindResource("FluentStroke") as Brush ?? Brushes.DimGray;
        foreach (var kv in _rowByRegion)
        {
            kv.Value.BorderBrush = ReferenceEquals(kv.Key, _selected) ? accent : stroke;
            kv.Value.BorderThickness = new(ReferenceEquals(kv.Key, _selected) ? 2 : 1);
        }
    }

    private void Select(OcrRegion r)
    {
        _selected = r;
        UpdateRowHighlights();
        UpdateDetailPanel();
        UpdatePreviewFromService();
    }

    // ---------- detail ----------

    private void UpdateDetailPanel()
    {
        _suppressFieldUpdates = true;
        try
        {
            if (_selected is null)
            {
                DetailHeader.Text = Locale.NoRegionSelected;
                NameBox.IsEnabled = KindCombo.IsEnabled = XBox.IsEnabled = YBox.IsEnabled =
                    WBox.IsEnabled = HBox.IsEnabled = ThresholdSlider.IsEnabled = InvertBox.IsEnabled =
                    DeleteButton.IsEnabled = false;
                NameBox.Text = ""; XBox.Text = YBox.Text = WBox.Text = HBox.Text = "";
                KindCombo.SelectedIndex = -1;
                ThresholdSlider.Value = 140;
                InvertBox.IsChecked = false;
                PreviewText.Text = "—";
                ConfidenceText.Text = "";
                return;
            }
            DetailHeader.Text = string.Format(Locale.EditingItemFormat, _selected.Name);
            NameBox.IsEnabled = KindCombo.IsEnabled = XBox.IsEnabled = YBox.IsEnabled =
                WBox.IsEnabled = HBox.IsEnabled = ThresholdSlider.IsEnabled = InvertBox.IsEnabled =
                DeleteButton.IsEnabled = true;
            NameBox.Text = _selected.Name;
            XBox.Text = _selected.X.ToString();
            YBox.Text = _selected.Y.ToString();
            WBox.Text = _selected.Width.ToString();
            HBox.Text = _selected.Height.ToString();
            KindCombo.SelectedIndex = (int)_selected.Kind;
            ThresholdSlider.Value = _selected.Threshold;
            ThresholdText.Text = _selected.Threshold.ToString();
            InvertBox.IsChecked = _selected.Invert;
        }
        finally
        {
            _suppressFieldUpdates = false;
        }
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFieldUpdates || _selected is null) return;
        _selected.Name = NameBox.Text;
        if (_rowByRegion.TryGetValue(_selected, out var row) && row.Child is Grid g
            && g.Children.Count >= 2 && g.Children[1] is StackPanel sp
            && sp.Children.Count > 0 && sp.Children[0] is TextBlock t)
        {
            t.Text = _selected.Name;
        }
        DetailHeader.Text = string.Format(Locale.EditingItemFormat, _selected.Name);
    }

    private void KindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFieldUpdates || _selected is null) return;
        int idx = KindCombo.SelectedIndex;
        if (idx >= 0 && idx <= 2) _selected.Kind = (OcrRegionKind)idx;
    }

    private void CoordBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFieldUpdates || _selected is null) return;
        if (sender is not TextBox tb) return;
        if (!int.TryParse(tb.Text, out int v)) return;
        switch (tb.Tag)
        {
            case "X": _selected.X = v; break;
            case "Y": _selected.Y = v; break;
            case "W": _selected.Width = v; break;
            case "H": _selected.Height = v; break;
        }
        RefreshRowSubtitle();
    }

    private void ThresholdSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressFieldUpdates || _selected is null) return;
        _selected.Threshold = (int)e.NewValue;
        ThresholdText.Text = _selected.Threshold.ToString();
    }

    private void InvertBox_Toggle(object sender, RoutedEventArgs e)
    {
        if (_suppressFieldUpdates || _selected is null) return;
        _selected.Invert = InvertBox.IsChecked == true;
    }

    private void RefreshRowSubtitle()
    {
        if (_selected is null) return;
        if (_rowByRegion.TryGetValue(_selected, out var row) && row.Child is Grid g
            && g.Children.Count >= 2 && g.Children[1] is StackPanel sp
            && sp.Children.Count > 1 && sp.Children[1] is TextBlock t)
        {
            t.Text = $"{_selected.Kind}  ·  {_selected.X},{_selected.Y}  {_selected.Width}×{_selected.Height}";
        }
    }

    // ---------- preview ----------

    private void UpdatePreviewFromService()
    {
        if (_selected is null) return;
        if (OcrService.Instance.Latest.TryGetValue(_selected.Name, out var result))
        {
            PreviewText.Text = string.IsNullOrEmpty(result.Text) ? Locale.OcrEmpty : result.Text;
            ConfidenceText.Text = string.Format(Locale.OcrConfidenceFormat, result.Confidence, result.Timestamp);
        }
        else
        {
            PreviewText.Text = "—";
            ConfidenceText.Text = Locale.OcrNoReadingYet;
        }
    }

    // ---------- buttons ----------

    private void AddRegion_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        var fresh = new OcrRegion { Name = string.Format(Locale.OcrRegionDefaultNameFormat, settings.Regions.Count + 1) };
        settings.Regions.Add(fresh);
        RebuildList();
        Select(fresh);
    }

    private void DeleteRegion_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null || _selected is null) return;
        settings.Regions.Remove(_selected);
        _selected = null;
        RebuildList();
        UpdateDetailPanel();
    }
    

    private void PickOnScreen_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        // Hide ourselves so the user can see the HUD they want to crop. Restore on return.
        var prevVis = Visibility;
        Visibility = Visibility.Hidden;
        try
        {
            var rect = ScreenRegionPicker.Pick(Owner ?? this);
            if (rect.HasValue)
            {
                _selected.X = rect.Value.X;
                _selected.Y = rect.Value.Y;
                _selected.Width = rect.Value.Width;
                _selected.Height = rect.Value.Height;
                UpdateDetailPanel();
                RefreshRowSubtitle();
            }
        }
        finally
        {
            Visibility = prevVis;
            Activate();
        }
    }

    private void EngineEnabled_Toggle(object sender, RoutedEventArgs e)
    {
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        settings.Enabled = EngineEnabledBox.IsChecked == true;
        UpdateStatus();
    }

    // ---------- engine options (all opt-in, default off) ----------

    private void OptMaxChannel_Toggle(object sender, RoutedEventArgs e)
    {
        if (_suppressFieldUpdates) return;
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        settings.UseMaxChannelGrayscale = OptMaxChannelBox.IsChecked == true;
    }

    private void OptSubstitute_Toggle(object sender, RoutedEventArgs e)
    {
        if (_suppressFieldUpdates) return;
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        settings.SubstituteLettersToDigits = OptSubstituteBox.IsChecked == true;
    }

    private void OptStrictParse_Toggle(object sender, RoutedEventArgs e)
    {
        if (_suppressFieldUpdates) return;
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        settings.StrictNumberParsing = OptStrictParseBox.IsChecked == true;
    }

    private void OptDpi_Toggle(object sender, RoutedEventArgs e)
    {
        if (_suppressFieldUpdates) return;
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        // OcrService.EnsureEngine compares this against its cached _engineDpiHinted and recreates
        // the Tesseract engine on the next sample — no manual restart needed.
        settings.UseUserDefinedDpi = OptDpiBox.IsChecked == true;
    }

    private void OptOtsu_Toggle(object sender, RoutedEventArgs e)
    {
        if (_suppressFieldUpdates) return;
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        settings.UseOtsuFallback = OptOtsuBox.IsChecked == true;
    }

    private void OptStickyMs_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressFieldUpdates) return;
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is null) return;
        // Invalid input (non-int, empty) leaves the previous value alone — same pattern as the
        // coord boxes above. The setter on OcrSettings clamps to [0, 10000].
        if (int.TryParse(OptStickyMsBox.Text, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int ms))
        {
            settings.StickyLastValidMs = ms;
        }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        StatusLine.Text = Locale.OcrDownloadingTraineddata;
        bool ok = await OcrService.EnsureTraineddataAsync(new Progress<double>(v =>
            StatusLine.Text = string.Format(Locale.OcrDownloadingTraineddataProgressFormat, (int)(v * 100))));
        StatusLine.Text = ok
            ? string.Format(Locale.OcrTraineddataInstalledFormat, OcrService.ResolveTessdataPath())
            : Locale.OcrDownloadFailed;
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        // Force a synchronous engine instantiation and surface any DLL / load failure. Lets the
        // user know whether Tesseract is actually wired up before they start adding regions.
        StatusLine.Text = OcrService.Instance.TestEngine();

        // Also flip the engine on so the polling loop starts feeding the live preview.
        var settings = AppConfig.Current?.OcrSettings;
        if (settings is not null && !settings.Enabled)
        {
            settings.Enabled = true;
            EngineEnabledBox.IsChecked = true;
        }
    }

    private void UpdateStatus()
    {
        bool hasData = OcrService.HasTraineddata(AppConfig.Current?.OcrSettings?.TessdataPath);
        string err = OcrService.Instance.LastError;
        if (!string.IsNullOrEmpty(err)) StatusLine.Text = err;
        else if (!hasData) StatusLine.Text = Locale.OcrTraineddataMissing;
        else if (AppConfig.Current?.OcrSettings?.Enabled != true) StatusLine.Text = Locale.OcrEnginePaused;
        else StatusLine.Text = Locale.OcrEngineRunningStatus;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    ///     Closes this configuration dialog and opens the on-screen overlay in edit mode so the
    ///     user can drag/resize regions directly on the live HUD. The overlay writes back to the
    ///     same <see cref="OcrRegion"/> instances, so re-opening this dialog after editing surfaces
    ///     the up-to-date coordinates.
    /// </summary>
    private void VisualEdit_Click(object sender, RoutedEventArgs e)
    {
        Close();
        OcrRegionsOverlay.OpenInEditMode();
    }

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _previewTimer.Stop();
        OcrService.Instance.PropertyChanged -= OnServicePropertyChanged;
        base.OnClosed(e);
    }
}
