using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim;

namespace PowerAim.Visuality;

/// <summary>
///     Bottom-right floating chip ("3 hidden sections") + flyout menu that lets the user restore
///     boxes hidden via the per-box × button. A single instance is owned by <c>MainWindow</c> and
///     rebinds to the currently-active page's <see cref="PageLayoutManager"/> on every navigation.
///     <para>
///     Implementation note: we deliberately do NOT use a <see cref="System.Windows.Controls.Primitives.Popup"/>
///     for the flyout. WPF's <c>Popup.StaysOpen=false</c> sets up a mouse-capture that eats
///     bubbling MouseUp/Click events, which silently dropped restore clicks regardless of whether
///     the rows were Buttons or Borders with MouseLeftButtonUp handlers. A plain Border sibling
///     of the pill, toggled by visibility, sidesteps the entire capture machinery and lets normal
///     event routing work.
///     </para>
/// </summary>
public sealed class HiddenBoxesPill
{
    private readonly Grid _hostGrid;
    private readonly Border _pill;
    private readonly TextBlock _label;
    private readonly Border _flyout;
    private readonly StackPanel _flyoutItems;
    private PageLayoutManager? _activeManager;

    public HiddenBoxesPill(Grid host)
    {
        _hostGrid = host;

        _pill = BuildPill(out _label);
        _flyout = BuildFlyout(out _flyoutItems);

        Panel.SetZIndex(_pill, 100);
        Panel.SetZIndex(_flyout, 101);
        _hostGrid.Children.Add(_pill);
        _hostGrid.Children.Add(_flyout);

        // CRITICAL — the MainWindow has a Window-level MouseLeftButtonDown handler that calls
        // DragMove() unconditionally on every click that bubbles to it. DragMove blocks the
        // message loop until MouseUp, which means inner controls never see their MouseUp /
        // Click. By marking the tunnel-phase event handled inside the pill+flyout subtree, the
        // Window's bubble-phase handler (a default registration that ignores already-handled
        // events) never fires, and DragMove never starts.  MouseLeftButtonUp is a separate
        // routed event — it still fires normally on the row.
        _pill.PreviewMouseLeftButtonDown   += (_, e) => e.Handled = true;
        _flyout.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;

        _pill.MouseLeftButtonUp += (_, _) => ToggleFlyout();

        // Click anywhere outside the flyout closes it — we attach to the host once so any click
        // on the page is observed. The flyout itself stops propagation in its own handler.
        _hostGrid.PreviewMouseDown += (_, e) =>
        {
            if (_flyout.Visibility != Visibility.Visible) return;
            if (IsAncestor(_flyout, e.OriginalSource as DependencyObject)) return;
            if (IsAncestor(_pill,   e.OriginalSource as DependencyObject)) return;
            _flyout.Visibility = Visibility.Collapsed;
        };
    }

    private Border BuildPill(out TextBlock labelOut)
    {
        var pill = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new(0, 0, 24, 24),
            CornerRadius = new(16),
            Padding = new(12, 6, 12, 6),
            Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed,
            BorderThickness = new(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 14, Opacity = 0.3, ShadowDepth = 2 },
        };
        pill.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");
        pill.SetResourceReference(Border.BorderBrushProperty, "FluentAccent");

        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = new TextBlock
        {
            Text = "", // view glyph
            FontFamily = new("Segoe Fluent Icons,Segoe MDL2 Assets"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 0, 6, 0),
        };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "FluentAccent");
        sp.Children.Add(icon);

        var label = new TextBlock
        {
            FontFamily = new("Segoe UI Variable Text"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        sp.Children.Add(label);
        pill.Child = sp;

        labelOut = label;
        return pill;
    }

    private Border BuildFlyout(out StackPanel itemsOut)
    {
        var card = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            // Positioned above the pill: pill margin-bottom is 24, pill height ≈ 32, so we
            // offset the flyout's bottom margin to skip past the pill plus a small gap.
            Margin = new(0, 0, 24, 64),
            CornerRadius = new(8),
            Padding = new(8),
            MinWidth = 240,
            Visibility = Visibility.Collapsed,
            BorderThickness = new(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 16, Opacity = 0.4, ShadowDepth = 2 },
        };
        card.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
        card.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");

        var heading = new TextBlock
        {
            Text = Locale.HiddenSections,
            FontFamily = new("Segoe UI Variable Display"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Margin = new(4, 2, 4, 6),
        };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");

        var items = new StackPanel { Margin = new(4) };
        card.Child = new StackPanel { Children = { heading, items } };
        itemsOut = items;
        return card;
    }

    private static bool IsAncestor(DependencyObject ancestor, DependencyObject? node)
    {
        for (int i = 0; i < 32 && node is not null; i++)
        {
            if (ReferenceEquals(node, ancestor)) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    public void Bind(PageLayoutManager? manager)
    {
        if (_activeManager is not null) _activeManager.LayoutChanged -= Refresh;
        _activeManager = manager;
        if (manager is not null) manager.LayoutChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (_activeManager is null)
        {
            _pill.Visibility = Visibility.Collapsed;
            _flyout.Visibility = Visibility.Collapsed;
            return;
        }
        var hidden = _activeManager.HiddenBoxes().ToList();
        if (hidden.Count == 0)
        {
            _pill.Visibility = Visibility.Collapsed;
            _flyout.Visibility = Visibility.Collapsed;
            return;
        }
        _pill.Visibility = Visibility.Visible;
        _label.Text = hidden.Count == 1 ? Locale.OneHiddenSection : string.Format(Locale.HiddenSectionsCountFormat, hidden.Count);

        _flyoutItems.Children.Clear();
        foreach (var box in hidden)
            _flyoutItems.Children.Add(BuildRow(box));
    }

    /// <summary>
    ///     Single restore row. Plain Border with a MouseLeftButtonUp handler — works reliably
    ///     because we're not inside a capturing Popup. Hover-feedback via PointerOver-style
    ///     background swap.
    /// </summary>
    private Border BuildRow(BoxRef box)
    {
        var rowSp = new StackPanel { Orientation = Orientation.Horizontal };

        var icon = new TextBlock
        {
            Text = "", // show glyph
            FontFamily = new("Segoe Fluent Icons,Segoe MDL2 Assets"),
            FontSize = 11,
            Margin = new(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
        rowSp.Children.Add(icon);

        var name = new TextBlock
        {
            Text = box.DisplayName,
            FontFamily = new("Segoe UI Variable Text"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        rowSp.Children.Add(name);

        var row = new Border
        {
            Padding = new(8, 6, 8, 6),
            Margin = new(0, 0, 0, 2),
            CornerRadius = new(4),
            Cursor = Cursors.Hand,
            Background = System.Windows.Media.Brushes.Transparent,
            Child = rowSp,
            Tag = box.Identifier,
        };
        row.MouseEnter += (_, _) => row.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");
        row.MouseLeave += (_, _) => row.Background = System.Windows.Media.Brushes.Transparent;
        row.MouseLeftButtonUp += (_, e) =>
        {
            if (_activeManager is null) return;
            _activeManager.RestoreBox(box.Identifier);
            e.Handled = true;
            // Refresh will tear down this row, so we read identifier eagerly above. After
            // restoring, if nothing is left to show, the flyout collapses with the pill.
            if (!_activeManager.HiddenBoxes().Any())
                _flyout.Visibility = Visibility.Collapsed;
            // Refresh content for the (still-open) flyout if items remain.
            Refresh();
        };
        return row;
    }

    private void ToggleFlyout()
    {
        _flyout.Visibility = _flyout.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
