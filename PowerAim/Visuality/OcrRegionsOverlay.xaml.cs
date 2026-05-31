using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.AILogic;
using PowerAim.Class.Native;
using PowerAim.Config;
using Brush = System.Windows.Media.Brush;

namespace PowerAim.Visuality;

/// <summary>
///     Topmost transparent overlay that paints a labelled rectangle for every enabled
///     <see cref="OcrRegion"/> with its live OCR value, and — in edit mode — lets the user drag
///     the box to move it and resize it from the bottom-right corner. Edits write back to the
///     <see cref="OcrRegion"/> properties so the change is persisted with the config.
///     <para>
///     Hit-testing relies on WPF's transparent-pixel pass-through: the window itself has no
///     <c>WS_EX_TRANSPARENT</c> style, so the toolbar and per-region drag/resize thumbs receive
///     clicks normally while the empty space between them passes clicks down to the game.
///     </para>
///     Singleton via <see cref="ShowOrHide"/>; toggle bound to
///     <see cref="ToggleState.ShowOcrRegionsOverlay"/>.
/// </summary>
public partial class OcrRegionsOverlay : Window
{
    private static OcrRegionsOverlay? _instance;

    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private readonly List<RegionVisual> _visuals = new();
    private OcrSettings? _settings;
    private double _dpi = 1.0;
    /// <summary>Set true while we mutate region.X/Y/W/H from inside a drag so the resulting
    /// PropertyChanged doesn't cascade back into a visual rebuild and stutter the drag.</summary>
    private bool _suppressRegionSync;

    public OcrRegionsOverlay()
    {
        InitializeComponent();
        _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
    }

    public bool EditMode => EditModeCheckBox.IsChecked == true;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        this.HideForCaptureIfEnabled();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionOnPrimaryScreen();
        _settings = AppConfig.Current?.OcrSettings;
        if (_settings is not null)
        {
            _settings.Regions.CollectionChanged += OnRegionsCollectionChanged;
            foreach (var r in _settings.Regions) Subscribe(r);
        }
        Rebuild();
        _timer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        if (_settings is not null)
        {
            _settings.Regions.CollectionChanged -= OnRegionsCollectionChanged;
            foreach (var r in _settings.Regions) Unsubscribe(r);
        }
        base.OnClosed(e);
    }

    private void PositionOnPrimaryScreen()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null) return;
        var src = PresentationSource.FromVisual(this);
        _dpi = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var b = screen.Bounds;
        Left = b.X / _dpi;
        Top = b.Y / _dpi;
        Width = b.Width / _dpi;
        Height = b.Height / _dpi;
    }

    private void Subscribe(OcrRegion r) => r.PropertyChanged += OnRegionPropertyChanged;
    private void Unsubscribe(OcrRegion r) => r.PropertyChanged -= OnRegionPropertyChanged;

    private void OnRegionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (OcrRegion r in e.OldItems) Unsubscribe(r);
        if (e.NewItems != null) foreach (OcrRegion r in e.NewItems) Subscribe(r);
        Dispatcher.BeginInvoke(new Action(Rebuild));
    }

    private void OnRegionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressRegionSync) return;
        if (sender is not OcrRegion r) return;
        // Enabled flip ⇒ may add/remove a visual ⇒ full rebuild. Other shape changes ⇒ just
        // refresh the existing visual in place so we don't churn during external edits.
        if (e.PropertyName is nameof(OcrRegion.Enabled) or nameof(OcrRegion.Name))
        {
            Dispatcher.BeginInvoke(new Action(Rebuild));
            return;
        }
        var v = _visuals.FirstOrDefault(x => ReferenceEquals(x.Region, r));
        v?.SyncFromRegion();
    }

    private void Rebuild()
    {
        // Drop existing visuals; build a fresh visual for every enabled region.
        foreach (var v in _visuals) v.Detach(RegionsCanvas);
        _visuals.Clear();
        if (_settings is null) return;
        foreach (var r in _settings.Regions)
        {
            if (!r.Enabled) continue;
            var vis = new RegionVisual(r, this);
            _visuals.Add(vis);
            vis.AttachTo(RegionsCanvas);
        }
        ApplyEditMode();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var latest = OcrService.Instance.Latest;
        foreach (var v in _visuals) v.UpdateValue(latest);
    }

    private void EditModeCheckBox_CheckedChanged(object sender, RoutedEventArgs e) => ApplyEditMode();

    private void ApplyEditMode()
    {
        foreach (var v in _visuals) v.SetEditMode(EditMode);
        AddRegionButton.Visibility = EditMode ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    ///     Spawns a fresh <see cref="OcrRegion"/> in the centre of the primary screen with a sane
    ///     default size and an auto-generated name. CollectionChanged on
    ///     <see cref="OcrSettings.Regions"/> picks it up, the overlay rebuilds, and the new region
    ///     immediately becomes draggable + resizable.
    /// </summary>
    private void AddRegion_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null) return;
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null) return;
        var bounds = screen.Bounds;
        const int defW = 200, defH = 60;
        int x = bounds.X + (bounds.Width - defW) / 2;
        int y = bounds.Y + (bounds.Height - defH) / 2;
        // Pick the next free "Region N" name so the new region has something readable to start with.
        int n = 1;
        var existing = _settings.Regions.Select(r => r.Name ?? "").ToHashSet();
        while (existing.Contains($"Region {n}")) n++;
        _settings.Regions.Add(new OcrRegion
        {
            Name = $"Region {n}",
            X = x, Y = y,
            Width = defW, Height = defH,
            Enabled = true,
        });
    }

    /// <summary>
    ///     Convenience entry point for the "Visual edit" button on <see cref="OcrRegionsDialog"/>:
    ///     ensures the overlay is shown and switches it straight into edit mode.
    /// </summary>
    public static void OpenInEditMode()
    {
        var ts = AppConfig.Current?.ToggleState;
        if (ts is not null && !ts.ShowOcrRegionsOverlay)
            ts.ShowOcrRegionsOverlay = true; // setter creates the instance via ShowOrHide
        else
            ShowOrHide(true);
        // Defer so the instance's XAML has had a tick to finish loading before we flip the checkbox.
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
        {
            if (_instance is not null) _instance.EditModeCheckBox.IsChecked = true;
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public double Dpi => _dpi;
    public bool SuppressRegionSync { get => _suppressRegionSync; set => _suppressRegionSync = value; }

    public static void ShowOrHide(bool visible)
    {
        if (visible)
        {
            if (_instance is null)
            {
                _instance = new OcrRegionsOverlay();
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

    // =============================================================== Per-region visual ====

    /// <summary>
    ///     Owns the WPF elements (frame, header label, value label, resize thumb) for one
    ///     <see cref="OcrRegion"/>. Builds them once on construction; <see cref="SyncFromRegion"/>
    ///     re-positions/sizes/labels them when the underlying region changes. Edit-mode toggles
    ///     drag + resize affordances on the frame.
    /// </summary>
    private sealed class RegionVisual
    {
        public OcrRegion Region { get; }
        private readonly OcrRegionsOverlay _owner;
        private readonly Border _frame;
        private readonly Border _headerChip;
        private readonly TextBlock _headerName;
        private readonly TextBlock _headerValue;
        private readonly Thumb _resizeThumb;
        private readonly Thumb _moveThumb;

        // Running drag/resize deltas — committed to the region on DragCompleted to avoid
        // re-entrant PropertyChanged churn during the drag itself.
        private double _moveDx, _moveDy;
        private double _resizeDw, _resizeDh;

        public RegionVisual(OcrRegion region, OcrRegionsOverlay owner)
        {
            Region = region;
            _owner = owner;

            // Frame: semi-transparent fill + accent stroke. Lives on the canvas at the region's
            // pixel position (translated to DIPs).
            _frame = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, 139, 92, 246)),   // 11% accent fill
                BorderBrush = new SolidColorBrush(Color.FromArgb(220, 139, 92, 246)), // accent
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(4),
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
            };

            // "Group-box" header chip floating above the frame's top-left corner.
            _headerName = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable Display"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xF0)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _headerValue = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono,Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0xE6, 0xB7)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(_headerName);
            headerPanel.Children.Add(_headerValue);

            _headerChip = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0x1A, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x3E, 0x3E, 0x4A)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Child = headerPanel,
                IsHitTestVisible = false,
            };

            // Edit-mode-only handles: a fullsize transparent Thumb on the frame for moving, and
            // a small bottom-right Thumb for resizing. Both default to Collapsed.
            _moveThumb = new Thumb
            {
                Background = System.Windows.Media.Brushes.Transparent,
                Opacity = 0.001, // invisible but hit-testable
                Cursor = Cursors.SizeAll,
                Visibility = Visibility.Collapsed,
                Template = MakeTransparentThumbTemplate(),
            };
            _moveThumb.DragStarted   += (_, _) => { _moveDx = 0; _moveDy = 0; };
            _moveThumb.DragDelta     += MoveThumb_OnDragDelta;
            _moveThumb.DragCompleted += MoveThumb_OnDragCompleted;

            _resizeThumb = new Thumb
            {
                Width = 12, Height = 12,
                Cursor = Cursors.SizeNWSE,
                Visibility = Visibility.Collapsed,
                Template = MakeCornerThumbTemplate(),
            };
            _resizeThumb.DragStarted   += (_, _) => { _resizeDw = 0; _resizeDh = 0; };
            _resizeThumb.DragDelta     += ResizeThumb_OnDragDelta;
            _resizeThumb.DragCompleted += ResizeThumb_OnDragCompleted;
        }

        public void AttachTo(Canvas canvas)
        {
            canvas.Children.Add(_frame);
            canvas.Children.Add(_moveThumb);
            canvas.Children.Add(_headerChip);
            canvas.Children.Add(_resizeThumb);
            SyncFromRegion();
        }

        public void Detach(Canvas canvas)
        {
            canvas.Children.Remove(_frame);
            canvas.Children.Remove(_moveThumb);
            canvas.Children.Remove(_headerChip);
            canvas.Children.Remove(_resizeThumb);
        }

        /// <summary>Re-pull all geometry + labels from the underlying region.</summary>
        public void SyncFromRegion()
        {
            double dpi = _owner.Dpi;
            double x = Region.X / dpi;
            double y = Region.Y / dpi;
            double w = Region.Width / dpi;
            double h = Region.Height / dpi;

            Canvas.SetLeft(_frame, x);
            Canvas.SetTop(_frame, y);
            _frame.Width = w;
            _frame.Height = h;

            Canvas.SetLeft(_moveThumb, x);
            Canvas.SetTop(_moveThumb, y);
            _moveThumb.Width = Math.Max(0, w);
            _moveThumb.Height = Math.Max(0, h);

            // Header chip sits just above the frame (or just below if there's no room above).
            _headerChip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double chipH = _headerChip.DesiredSize.Height;
            double chipY = y - chipH - 2;
            if (chipY < 0) chipY = y + 2;
            Canvas.SetLeft(_headerChip, x);
            Canvas.SetTop(_headerChip, chipY);

            // Resize handle at the bottom-right corner of the frame.
            Canvas.SetLeft(_resizeThumb, x + w - _resizeThumb.Width / 2);
            Canvas.SetTop(_resizeThumb, y + h - _resizeThumb.Height / 2);

            _headerName.Text = string.IsNullOrEmpty(Region.Name) ? "—" : Region.Name;
        }

        /// <summary>Refresh the value chip from the latest OCR reading (no-op if no result).</summary>
        public void UpdateValue(IReadOnlyDictionary<string, OcrResult> latest)
        {
            if (latest.TryGetValue(Region.Name ?? "", out var r))
            {
                _headerValue.Text = string.IsNullOrEmpty(r.Text) ? "·" : r.Text;
            }
            else
            {
                _headerValue.Text = "—";
            }
        }

        public void SetEditMode(bool on)
        {
            _moveThumb.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            _resizeThumb.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            // Make the frame slightly more prominent in edit mode.
            _frame.BorderThickness = new Thickness(on ? 2.0 : 1.5);
        }

        private void MoveThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            _moveDx += e.HorizontalChange;
            _moveDy += e.VerticalChange;
            double newX = Region.X / _owner.Dpi + _moveDx;
            double newY = Region.Y / _owner.Dpi + _moveDy;
            // Live preview by updating the canvas positions only.
            Canvas.SetLeft(_frame, newX);
            Canvas.SetTop(_frame, newY);
            Canvas.SetLeft(_moveThumb, newX);
            Canvas.SetTop(_moveThumb, newY);
            // Chip + resize handle follow the frame.
            double w = _frame.Width;
            double h = _frame.Height;
            double chipH = _headerChip.DesiredSize.Height;
            double chipY = newY - chipH - 2;
            if (chipY < 0) chipY = newY + 2;
            Canvas.SetLeft(_headerChip, newX);
            Canvas.SetTop(_headerChip, chipY);
            Canvas.SetLeft(_resizeThumb, newX + w - _resizeThumb.Width / 2);
            Canvas.SetTop(_resizeThumb, newY + h - _resizeThumb.Height / 2);
        }

        private void MoveThumb_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (_moveDx == 0 && _moveDy == 0) return;
            try
            {
                _owner.SuppressRegionSync = true;
                Region.X = (int)Math.Round(Region.X + _moveDx * _owner.Dpi);
                Region.Y = (int)Math.Round(Region.Y + _moveDy * _owner.Dpi);
            }
            finally { _owner.SuppressRegionSync = false; }
            _moveDx = 0; _moveDy = 0;
            SyncFromRegion(); // snap to authoritative integer pixel values
        }

        private void ResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            _resizeDw += e.HorizontalChange;
            _resizeDh += e.VerticalChange;
            double newW = Math.Max(8, Region.Width / _owner.Dpi + _resizeDw);
            double newH = Math.Max(8, Region.Height / _owner.Dpi + _resizeDh);
            _frame.Width = newW;
            _frame.Height = newH;
            _moveThumb.Width = newW;
            _moveThumb.Height = newH;
            double x = Canvas.GetLeft(_frame);
            double y = Canvas.GetTop(_frame);
            Canvas.SetLeft(_resizeThumb, x + newW - _resizeThumb.Width / 2);
            Canvas.SetTop(_resizeThumb, y + newH - _resizeThumb.Height / 2);
        }

        private void ResizeThumb_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (_resizeDw == 0 && _resizeDh == 0) return;
            try
            {
                _owner.SuppressRegionSync = true;
                Region.Width = Math.Max(8, (int)Math.Round(Region.Width + _resizeDw * _owner.Dpi));
                Region.Height = Math.Max(8, (int)Math.Round(Region.Height + _resizeDh * _owner.Dpi));
            }
            finally { _owner.SuppressRegionSync = false; }
            _resizeDw = 0; _resizeDh = 0;
            SyncFromRegion();
        }

        private static ControlTemplate MakeTransparentThumbTemplate()
        {
            // Thumb's default template draws nothing visible but still hit-tests. We replace it
            // with a Rectangle whose Fill is transparent so the whole region area is grabbable.
            var template = new ControlTemplate(typeof(Thumb));
            var rect = new FrameworkElementFactory(typeof(System.Windows.Shapes.Rectangle));
            rect.SetValue(System.Windows.Shapes.Rectangle.FillProperty, System.Windows.Media.Brushes.Transparent);
            template.VisualTree = rect;
            return template;
        }

        private static ControlTemplate MakeCornerThumbTemplate()
        {
            // Small filled square with a clear border so the resize handle is visible.
            var template = new ControlTemplate(typeof(Thumb));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0xFF, 0x8B, 0x5C, 0xF6)));
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            template.VisualTree = border;
            return template;
        }
    }
}
