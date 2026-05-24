using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PowerAim.Class.Native;
using PowerAim.Config;

namespace PowerAim.Visuality;

/// <summary>
///     Modal editor for <see cref="AISettings.IgnoreRegions"/>. Edits a working copy and only writes
///     back to the live <see cref="AISettings"/> on Save, so Cancel leaves the running config
///     untouched.
///     <para>
///     UX: master list of masks on the left (one row per mask with enabled-checkbox, name input
///     and delete button), preview canvas on the right where the user can drag-draw a new mask or
///     drag-move the currently selected mask. Sliders below the preview do precision edits.
///     </para>
/// </summary>
public partial class DetectionMasksDialog
{
    private readonly ObservableCollection<DetectionMaskRegion> _working = new();
    private DetectionMaskRegion? _selected;
    private readonly Dictionary<DetectionMaskRegion, Border> _rowByMask = new();

    // Drag state
    private bool _isDragging;
    private bool _isDrawingNew;
    private Point _dragStart;
    private double _dragStartX, _dragStartY, _dragStartW, _dragStartH;
    private bool _suppressSliderUpdate;

    public DetectionMasksDialog()
    {
        InitializeComponent();

        var src = AppConfig.Current?.AISettings?.IgnoreRegions;
        if (src != null)
        {
            foreach (var m in src)
            {
                _working.Add(new DetectionMaskRegion
                {
                    Name = m.Name,
                    X = m.X, Y = m.Y, Width = m.Width, Height = m.Height,
                    Enabled = m.Enabled
                });
            }
        }

        RebuildList();
        RedrawPreview();
        UpdateSlidersFromSelection();
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }
    // ---------- list building ----------

    private void RebuildList()
    {
        MaskItems.Items.Clear();
        _rowByMask.Clear();
        foreach (var mask in _working)
        {
            var row = BuildRow(mask);
            _rowByMask[mask] = row;
            MaskItems.Items.Add(row);
        }
        UpdateRowHighlights();
    }

    private Border BuildRow(DetectionMaskRegion mask)
    {
        var border = new Border
        {
            BorderBrush = TryFindResource("FluentStroke") as Brush ?? Brushes.DimGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = TryFindResource("FluentSurface3") as Brush ?? Brushes.Black,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 4),
            Cursor = Cursors.Hand,
            Tag = mask
        };
        border.MouseLeftButtonDown += (_, _) => Select(mask);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var enabledBox = new CheckBox
        {
            IsChecked = mask.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        enabledBox.Checked += (_, _) => { mask.Enabled = true; RedrawPreview(); };
        enabledBox.Unchecked += (_, _) => { mask.Enabled = false; RedrawPreview(); };
        Grid.SetColumn(enabledBox, 0);
        grid.Children.Add(enabledBox);

        var nameBox = new TextBox
        {
            Text = mask.Name,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            MinHeight = 26
        };
        nameBox.TextChanged += (_, _) => mask.Name = nameBox.Text;
        nameBox.GotFocus += (_, _) => Select(mask);
        Grid.SetColumn(nameBox, 1);
        grid.Children.Add(nameBox);

        var deleteBtn = new Button
        {
            Content = "",
            FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
            FontSize = 12,
            Width = 28,
            Height = 28,
            Margin = new Thickness(6, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent
        };
        deleteBtn.Click += (_, _) => RemoveMask(mask);
        Grid.SetColumn(deleteBtn, 2);
        grid.Children.Add(deleteBtn);

        border.Child = grid;
        return border;
    }

    private void UpdateRowHighlights()
    {
        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
        var stroke = TryFindResource("FluentStroke") as Brush ?? Brushes.DimGray;
        foreach (var kv in _rowByMask)
        {
            kv.Value.BorderBrush = ReferenceEquals(kv.Key, _selected) ? accent : stroke;
            kv.Value.BorderThickness = new Thickness(ReferenceEquals(kv.Key, _selected) ? 2 : 1);
        }
    }

    private void Select(DetectionMaskRegion mask)
    {
        _selected = mask;
        UpdateRowHighlights();
        UpdateSlidersFromSelection();
        RedrawPreview();
    }

    private void RemoveMask(DetectionMaskRegion mask)
    {
        _working.Remove(mask);
        if (ReferenceEquals(_selected, mask)) _selected = null;
        RebuildList();
        UpdateSlidersFromSelection();
        RedrawPreview();
    }

    // ---------- sliders ----------

    private void UpdateSlidersFromSelection()
    {
        _suppressSliderUpdate = true;
        try
        {
            if (_selected == null)
            {
                SelectedHeader.Text = "No mask selected";
                XSlider.IsEnabled = YSlider.IsEnabled = WSlider.IsEnabled = HSlider.IsEnabled = false;
                XSlider.Value = YSlider.Value = 0;
                WSlider.Value = HSlider.Value = 0.1;
                return;
            }
            SelectedHeader.Text = $"Editing: {_selected.Name}";
            XSlider.IsEnabled = YSlider.IsEnabled = WSlider.IsEnabled = HSlider.IsEnabled = true;
            XSlider.Value = _selected.X;
            YSlider.Value = _selected.Y;
            WSlider.Value = _selected.Width;
            HSlider.Value = _selected.Height;
        }
        finally
        {
            _suppressSliderUpdate = false;
        }
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderUpdate || _selected == null) return;
        // Clamp so X+W and Y+H stay within [0,1].
        if (sender == XSlider) _selected.X = Math.Min(e.NewValue, 1.0 - _selected.Width);
        else if (sender == YSlider) _selected.Y = Math.Min(e.NewValue, 1.0 - _selected.Height);
        else if (sender == WSlider) _selected.Width = Math.Min(e.NewValue, 1.0 - _selected.X);
        else if (sender == HSlider) _selected.Height = Math.Min(e.NewValue, 1.0 - _selected.Y);
        RedrawPreview();
    }

    // ---------- preview canvas ----------

    private void RedrawPreview()
    {
        PreviewCanvas.Children.Clear();
        double w = PreviewHost.Width;
        double h = PreviewHost.Height;

        var accent = TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
        var dim = new SolidColorBrush(Color.FromArgb(70, 200, 200, 200));

        foreach (var mask in _working)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(2, mask.Width * w),
                Height = Math.Max(2, mask.Height * h),
                Fill = ReferenceEquals(mask, _selected)
                    ? new SolidColorBrush(Color.FromArgb(110, ((SolidColorBrush)accent).Color.R, ((SolidColorBrush)accent).Color.G, ((SolidColorBrush)accent).Color.B))
                    : dim,
                Stroke = ReferenceEquals(mask, _selected) ? accent : Brushes.LightGray,
                StrokeThickness = ReferenceEquals(mask, _selected) ? 2 : 1,
                Opacity = mask.Enabled ? 1.0 : 0.4,
                Tag = mask
            };
            rect.MouseLeftButtonDown += Mask_MouseDown;
            Canvas.SetLeft(rect, mask.X * w);
            Canvas.SetTop(rect, mask.Y * h);
            PreviewCanvas.Children.Add(rect);
        }
    }

    private void Mask_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle r && r.Tag is DetectionMaskRegion mask)
        {
            Select(mask);
            BeginDragMove(e.GetPosition(PreviewCanvas));
            PreviewCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Click on empty canvas → draw a new mask starting here.
        var pos = e.GetPosition(PreviewCanvas);
        var fresh = new DetectionMaskRegion
        {
            Name = $"Mask {_working.Count + 1}",
            X = pos.X / PreviewHost.Width,
            Y = pos.Y / PreviewHost.Height,
            Width = 0.01,
            Height = 0.01,
            Enabled = true
        };
        _working.Add(fresh);
        RebuildList();
        Select(fresh);

        _isDragging = true;
        _isDrawingNew = true;
        _dragStart = pos;
        _dragStartX = fresh.X;
        _dragStartY = fresh.Y;
        _dragStartW = fresh.Width;
        _dragStartH = fresh.Height;
        PreviewCanvas.CaptureMouse();
    }

    private void BeginDragMove(Point pos)
    {
        if (_selected == null) return;
        _isDragging = true;
        _isDrawingNew = false;
        _dragStart = pos;
        _dragStartX = _selected.X;
        _dragStartY = _selected.Y;
        _dragStartW = _selected.Width;
        _dragStartH = _selected.Height;
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _selected == null) return;
        var pos = e.GetPosition(PreviewCanvas);
        double dx = (pos.X - _dragStart.X) / PreviewHost.Width;
        double dy = (pos.Y - _dragStart.Y) / PreviewHost.Height;

        if (_isDrawingNew)
        {
            // Anchored at start; resize to mouse.
            _selected.X = Math.Min(_dragStartX, _dragStartX + dx);
            _selected.Y = Math.Min(_dragStartY, _dragStartY + dy);
            _selected.Width = Math.Max(0.01, Math.Abs(dx));
            _selected.Height = Math.Max(0.01, Math.Abs(dy));
        }
        else
        {
            // Translate the existing mask.
            _selected.X = Math.Clamp(_dragStartX + dx, 0.0, 1.0 - _selected.Width);
            _selected.Y = Math.Clamp(_dragStartY + dy, 0.0, 1.0 - _selected.Height);
        }
        UpdateSlidersFromSelection();
        RedrawPreview();
    }

    private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _isDrawingNew = false;
            if (PreviewCanvas.IsMouseCaptured) PreviewCanvas.ReleaseMouseCapture();
        }
    }

    // ---------- buttons ----------

    private void AddMask_Click(object sender, RoutedEventArgs e)
    {
        var fresh = new DetectionMaskRegion
        {
            Name = $"Mask {_working.Count + 1}",
            X = 0.4, Y = 0.4, Width = 0.2, Height = 0.1, Enabled = true
        };
        _working.Add(fresh);
        RebuildList();
        Select(fresh);
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _working.Clear();
        _selected = null;
        RebuildList();
        UpdateSlidersFromSelection();
        RedrawPreview();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppConfig.Current?.AISettings;
        if (settings == null) { Close(); return; }

        settings.IgnoreRegions.Clear();
        foreach (var m in _working) settings.IgnoreRegions.Add(m);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
