using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.Config;

namespace PowerAim.Visuality;

/// <summary>
///     Per-page layout controller. Attached to a page once after <c>CreateUI()</c> builds it.
///     Discovers every <i>box</i> (a FluentCard <see cref="Border"/> wrapping a named
///     <see cref="StackPanel"/>), wraps each one with a small chrome row (drag-thumb + hide-X),
///     and applies the persisted layout from <see cref="AppConfig.LayoutConfiguration"/>.
///     <para>
///     Drag-reorder works within the box's hosting <see cref="StackPanel"/> only — cross-column
///     moves on 2-col pages stay column-local. Hidden boxes are reachable via
///     <see cref="HiddenBoxesPill"/> bottom-right of the active page.
///     </para>
/// </summary>
public sealed class PageLayoutManager
{
    private readonly string _pageName;
    private readonly FrameworkElement _pageRoot;
    private readonly List<BoxRef> _boxes = new();
    /// <summary>Distinct host StackPanels in document order. Index = column ID for persistence.</summary>
    private readonly List<StackPanel> _hosts = new();

    private PageLayoutManager(string pageName, FrameworkElement pageRoot)
    {
        _pageName = pageName;
        _pageRoot = pageRoot;
    }

    /// <summary>
    ///     Entry point. Walks <paramref name="pageRoot"/>, identifies every box, instruments it,
    ///     and applies any persisted layout for <paramref name="pageName"/>. Idempotent — calling
    ///     it a second time is a no-op because we tag each box.
    /// </summary>
    public static PageLayoutManager Attach(string pageName, FrameworkElement pageRoot)
    {
        var mgr = new PageLayoutManager(pageName, pageRoot);
        mgr.DiscoverBoxes();
        mgr.InstrumentBoxes();
        mgr.ApplyPersistedLayout();
        return mgr;
    }

    /// <summary>The current list of boxes (visible + hidden) for the floating pill to inspect.</summary>
    public IReadOnlyList<BoxRef> Boxes => _boxes;

    /// <summary>Fires whenever the layout of this page changes (order or hidden set).</summary>
    public event Action? LayoutChanged;

    // ============================================================================ DISCOVERY ====

    private void DiscoverBoxes()
    {
        foreach (var border in WalkBorders(_pageRoot))
        {
            if (border.Tag is BoxRef) continue; // already attached
            // FluentCard styling check — string-compare the style key. Style.TargetType is not
            // sufficient because many UserControls use FluentCard internally.
            if (!IsFluentCard(border)) continue;
            // Child must be a *named* StackPanel — that's our stable identifier.
            if (border.Child is not StackPanel sp || string.IsNullOrEmpty(sp.Name)) continue;
            // Boxes need a host StackPanel parent we can reorder children in.
            if (VisualTreeHelper.GetParent(border) is not StackPanel host) continue;

            _boxes.Add(new BoxRef
            {
                Identifier = sp.Name,
                Border = border,
                InnerPanel = sp,
                HostPanel = host
            });
            if (!_hosts.Contains(host)) _hosts.Add(host);
        }
    }

    private static bool IsFluentCard(Border b)
    {
        // Style might be set inline or via SetResourceReference — both end up with the same
        // Style instance under the same key. Compare by reference against the app-resource.
        var fluent = Application.Current.TryFindResource("FluentCard") as Style;
        return fluent is not null && ReferenceEquals(b.Style, fluent);
    }

    private static IEnumerable<Border> WalkBorders(DependencyObject root)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Border b) yield return b;
            foreach (var nested in WalkBorders(child)) yield return nested;
        }
    }

    // ============================================================================ INSTRUMENT ====

    private void InstrumentBoxes()
    {
        foreach (var box in _boxes) InstrumentBox(box);
    }

    private void InstrumentBox(BoxRef box)
    {
        var inner = box.InnerPanel;
        var border = box.Border;

        // Replace Border.Child = inner StackPanel with a Grid that hosts both the StackPanel and
        // the chrome (drag thumb + hide button). The StackPanel reference is preserved — XAML
        // bindings against `x:Name` continue to resolve because Name is on the StackPanel.
        border.Child = null;

        var wrapper = new Grid();
        wrapper.Children.Add(inner);

        var chrome = BuildChromeRow(box);
        wrapper.Children.Add(chrome);

        border.Child = wrapper;
        border.Tag = box; // mark as instrumented

        // Add a little top padding to the inner content so the chrome row doesn't visually
        // collide with the first ATitle.
        if (inner.Margin == new Thickness(0)) inner.Margin = new(0, 6, 0, 0);
    }

    /// <summary>
    ///     Build the small "chrome" row (drag thumb + hide-X) overlaid at the box's top-right.
    /// </summary>
    private FrameworkElement BuildChromeRow(BoxRef box)
    {
        // Sit the chrome inline with the ATitle's row, just to the LEFT of its expander chevron.
        // The chevron lives at the right edge of the title row (≈ 8–28px from the card border);
        // we leave a ~32px gutter so our two buttons land cleanly next to it instead of stacked
        // above it. Background is transparent so it blends with the card — the icons alone are
        // enough affordance.
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new(0, 14, 24, 0),
        };
        Panel.SetZIndex(row, 10);

        var thumb = new Thumb
        {
            Width = 12, Height = 12,
            Cursor = Cursors.SizeAll,
            ToolTip = "Drag to reorder",
            Template = MakeChromeButtonTemplate("") // E76F = drag handle (4 dots)
        };
        thumb.DragStarted   += (s, e) => OnDragStarted(box);
        thumb.DragDelta     += (s, e) => OnDragDelta(box, e);
        thumb.DragCompleted += (s, e) => OnDragCompleted(box);
        row.Children.Add(thumb);

        // Hide button.
        var hide = new Button
        {
            Width = 12, Height = 12,
            Margin = new(4, 0, 4, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new(0),
            ToolTip = "Hide this section",
            Template = MakeChromeButtonTemplate("") // E8BB = ×
        };
        hide.Click += (_, _) => HideBox(box);
        row.Children.Add(hide);

        return row;
    }

    private ControlTemplate MakeChromeButtonTemplate(string glyph)
    {
        // Tiny rounded-corner overlay with a Fluent glyph. Implemented via XAML string parse so
        // we don't have to repeat the boilerplate for every chrome button.
        var xaml = $@"
<ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                 xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                 TargetType=""Control"">
  <Border x:Name=""bd"" Background=""Transparent"" CornerRadius=""4"">
    <TextBlock Text=""{glyph}"" FontFamily=""Segoe Fluent Icons,Segoe MDL2 Assets""
               FontSize=""11""
               HorizontalAlignment=""Center"" VerticalAlignment=""Center""
               Foreground=""{{DynamicResource FluentTextSecondary}}"" />
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property=""IsMouseOver"" Value=""True"">
      <Setter TargetName=""bd"" Property=""Background""
              Value=""{{DynamicResource FluentSurface3}}"" />
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
        using var sr = new System.IO.StringReader(xaml);
        using var xr = System.Xml.XmlReader.Create(sr);
        return (ControlTemplate)System.Windows.Markup.XamlReader.Load(xr);
    }

    // ============================================================================ HIDE / SHOW ====

    public void HideBox(BoxRef box)
    {
        box.Border.Visibility = Visibility.Collapsed;
        var layout = AppConfig.Current.LayoutConfiguration.For(_pageName);
        if (!layout.Hidden.Contains(box.Identifier)) layout.Hidden.Add(box.Identifier);
        // Also drop from order — when restored we re-append at the end.
        layout.Order.Remove(box.Identifier);
        LayoutChanged?.Invoke();
    }

    public void RestoreBox(string identifier)
    {
        var box = _boxes.FirstOrDefault(b => b.Identifier == identifier);
        if (box is null) return;
        box.Border.Visibility = Visibility.Visible;
        var layout = AppConfig.Current.LayoutConfiguration.For(_pageName);
        layout.Hidden.Remove(identifier);
        if (!layout.Order.Contains(identifier)) layout.Order.Add(identifier);
        LayoutChanged?.Invoke();
    }

    public IEnumerable<BoxRef> HiddenBoxes()
        => _boxes.Where(b => b.Border.Visibility == Visibility.Collapsed);

    /// <summary>
    ///     Re-apply the active config's layout from scratch. Used when the config changes while the
    ///     page's boxes already exist (the boxes survive <c>CreateUI()</c>, so without this the
    ///     previous config's hidden/order state would stick). Resets every box to visible, then
    ///     hides + reorders per the current <see cref="AppConfig.LayoutConfiguration"/>. Sets
    ///     <see cref="UIElement.Visibility"/> directly (not via Hide/RestoreBox) so it does NOT
    ///     fire <see cref="LayoutChanged"/> — re-applying on load must never trigger a save.
    /// </summary>
    public void ReapplyLayout()
    {
        foreach (var box in _boxes)
            box.Border.Visibility = Visibility.Visible;
        ApplyPersistedLayout();
    }

    // ============================================================================ DRAG ====

    private BoxRef? _dragging;
    private Border? _dropMarker;
    private StackPanel? _dropHost;
    private int _dropIndex = -1;

    private void OnDragStarted(BoxRef box)
    {
        _dragging = box;
        // Dim the source. We deliberately don't reparent the box during drag — the Thumb that
        // drives the gesture lives *inside* the Border, and re-parenting kills mouse capture.
        // Instead we float a thin accent line that snaps between siblings (and across columns)
        // to show the drop target. Commit happens on DragCompleted.
        box.Border.Opacity = 0.55;
        _dropMarker = CreateDropMarker();
        _dropHost = box.HostPanel;
        _dropHost.Children.Add(_dropMarker);
    }

    private static Border CreateDropMarker()
    {
        var m = new Border
        {
            Height = 3,
            Margin = new(8, 2, 8, 2),
            CornerRadius = new(2),
            IsHitTestVisible = false,
        };
        m.SetResourceReference(Border.BackgroundProperty, "FluentAccent");
        return m;
    }

    private void OnDragDelta(BoxRef box, DragDeltaEventArgs e)
    {
        if (_dragging is null || _dropMarker is null) return;

        // 1) Find which host (column) the mouse is currently over. Falls back to source host
        //    when mouse is between columns / outside any known column.
        var targetHost = ResolveTargetHost(box.HostPanel);
        if (targetHost is null) return;

        // 2) Migrate the marker to the right column if needed.
        if (!ReferenceEquals(_dropHost, targetHost))
        {
            _dropHost?.Children.Remove(_dropMarker);
            _dropHost = targetHost;
            _dropHost.Children.Add(_dropMarker);
        }

        // 3) Position the marker at the insertion index in the target host.
        var mouse = Mouse.GetPosition(targetHost);
        int targetIndex = ComputeInsertionIndex(targetHost, box.Border, mouse.Y, _dropMarker);
        _dropIndex = targetIndex;

        int markerCurrent = targetHost.Children.IndexOf(_dropMarker);
        if (markerCurrent != targetIndex)
        {
            targetHost.Children.Remove(_dropMarker);
            int clamped = Math.Clamp(targetIndex, 0, targetHost.Children.Count);
            targetHost.Children.Insert(clamped, _dropMarker);
        }
    }

    /// <summary>
    ///     Which known HostPanel is the mouse cursor currently inside? Returns null when over
    ///     no known column — keeps the marker where it last was.
    /// </summary>
    private StackPanel? ResolveTargetHost(StackPanel fallback)
    {
        // Hit-test by computing the mouse position relative to each candidate host.
        foreach (var host in _hosts)
        {
            // Skip detached hosts (e.g. page rebuilt).
            if (!host.IsVisible) continue;
            var pos = Mouse.GetPosition(host);
            if (pos.X >= 0 && pos.X <= host.ActualWidth
                && pos.Y >= 0 && pos.Y <= host.ActualHeight)
                return host;
        }
        return fallback;
    }

    private void OnDragCompleted(BoxRef box)
    {
        if (_dragging is null) return;

        // Commit: move the source Border into the target column at the target index.
        if (_dropHost is not null && _dropIndex >= 0)
        {
            var sourceHost = box.HostPanel;
            sourceHost.Children.Remove(box.Border);

            int effective = _dropIndex;
            // If we removed the marker from the same host as where we're inserting and the
            // source had been BEFORE the marker, the indices shift left by 1.
            if (ReferenceEquals(sourceHost, _dropHost))
            {
                // Already pulled source out, so the marker index might need decrement.
                int markerIdxAfterRemove = _dropHost.Children.IndexOf(_dropMarker!);
                if (markerIdxAfterRemove >= 0) effective = markerIdxAfterRemove;
            }
            else
            {
                // Cross-column: replace the marker's slot directly.
                int markerIdx = _dropHost.Children.IndexOf(_dropMarker!);
                if (markerIdx >= 0) effective = markerIdx;
            }

            // Remove marker, then insert the Border at the resolved slot.
            if (_dropMarker is not null) _dropHost.Children.Remove(_dropMarker);
            effective = Math.Clamp(effective, 0, _dropHost.Children.Count);
            _dropHost.Children.Insert(effective, box.Border);

            // Rebind the box's host so further operations know where it lives now.
            box.HostPanel = _dropHost;
        }
        else if (_dropMarker is not null)
        {
            // No valid drop position computed — just take the marker out.
            (_dropHost ?? box.HostPanel).Children.Remove(_dropMarker);
        }

        _dropMarker = null;
        _dropHost = null;
        _dropIndex = -1;

        box.Border.Opacity = 1.0;
        _dragging = null;

        PersistCurrentOrder();
        LayoutChanged?.Invoke();
    }

    private static int ComputeInsertionIndex(StackPanel host, FrameworkElement self, double mouseY, Border marker)
    {
        // First visible sibling whose midpoint sits below the mouseY = insertion target.
        for (int i = 0; i < host.Children.Count; i++)
        {
            if (host.Children[i] is not FrameworkElement s) continue;
            if (ReferenceEquals(s, self) || ReferenceEquals(s, marker)) continue;
            if (s.Visibility != Visibility.Visible) continue;
            double top = s.TranslatePoint(new(0, 0), host).Y;
            double mid = top + s.ActualHeight / 2.0;
            if (mouseY < mid) return i;
        }
        return host.Children.Count;
    }

    // ============================================================================ PERSIST ====

    private void PersistCurrentOrder()
    {
        var layout = AppConfig.Current?.LayoutConfiguration?.For(_pageName);
        if (layout is null) return;

        // Rebuild Columns from scratch based on the current visual state. Each known host
        // becomes a column. Hidden boxes don't appear here — they're tracked separately in
        // layout.Hidden and re-appended on restore.
        layout.Columns.Clear();
        for (int colIdx = 0; colIdx < _hosts.Count; colIdx++)
        {
            var host = _hosts[colIdx];
            var ordered = new List<string>();
            foreach (var child in host.Children)
            {
                if (child is Border b && b.Tag is BoxRef br && b.Visibility == Visibility.Visible)
                    ordered.Add(br.Identifier);
            }
            layout.Columns[colIdx] = ordered;
        }
        // Keep the legacy flat Order in sync (read on next load if Columns is empty for some
        // reason) — purely defensive.
        layout.Order.Clear();
        foreach (var col in layout.Columns.Values)
            foreach (var id in col)
                layout.Order.Add(id);
    }

    private void ApplyPersistedLayout()
    {
        var layout = AppConfig.Current?.LayoutConfiguration?.For(_pageName);
        if (layout is null) return;

        // 1) Hide whatever the user previously hid.
        foreach (var id in layout.Hidden.ToArray())
        {
            var box = _boxes.FirstOrDefault(b => b.Identifier == id);
            if (box is not null) box.Border.Visibility = Visibility.Collapsed;
        }

        // 2) Restore columns. Newer config = layout.Columns (per-column lists). Older config
        //    (or first run after the upgrade) = layout.Order (flat) — in that case we honour
        //    the relative order within each XAML-default column.
        if (layout.Columns.Count > 0)
        {
            ApplyColumnsLayout(layout);
        }
        else if (layout.Order.Count > 0)
        {
            ApplyFlatOrderFallback(layout);
        }
    }

    private void ApplyColumnsLayout(PageLayout layout)
    {
        // For each persisted column index, find the corresponding host and re-arrange its
        // visible boxes to match the recorded order. Boxes that exist in code but aren't
        // referenced get appended to their original XAML column.
        var assigned = new HashSet<string>();
        foreach (var kv in layout.Columns)
        {
            int colIdx = kv.Key;
            if (colIdx < 0 || colIdx >= _hosts.Count) continue;
            var host = _hosts[colIdx];

            int targetIndex = 0;
            foreach (var id in kv.Value)
            {
                var box = _boxes.FirstOrDefault(b => b.Identifier == id);
                if (box is null || box.Border.Visibility != Visibility.Visible) continue;
                // Pull from current host and insert into target host.
                if (!ReferenceEquals(box.HostPanel, host))
                {
                    box.HostPanel.Children.Remove(box.Border);
                    box.HostPanel = host;
                }
                else
                {
                    host.Children.Remove(box.Border);
                }
                host.Children.Insert(Math.Min(targetIndex, host.Children.Count), box.Border);
                targetIndex++;
                assigned.Add(id);
            }
        }
        // Anything not in Columns lands at the end of its original (XAML-default) host.
        foreach (var box in _boxes)
        {
            if (assigned.Contains(box.Identifier)) continue;
            if (box.Border.Visibility != Visibility.Visible) continue;
            // box.HostPanel is still pointing at the XAML default — leave the Border there.
        }
    }

    private void ApplyFlatOrderFallback(PageLayout layout)
    {
        // Legacy: order list with no column information. Keep XAML-default column assignment
        // and only re-sort within each column.
        foreach (var group in _boxes.Where(b => b.Border.Visibility == Visibility.Visible)
                                     .GroupBy(b => b.HostPanel))
        {
            var host = group.Key;
            var inGroupIds = group.Select(b => b.Identifier).ToHashSet();
            var persistedFiltered = layout.Order.Where(id => inGroupIds.Contains(id)).ToList();
            var leftovers = group.Where(b => !persistedFiltered.Contains(b.Identifier))
                                  .Select(b => b.Identifier);
            var finalOrder = persistedFiltered.Concat(leftovers).ToList();

            int targetIndex = 0;
            foreach (var id in finalOrder)
            {
                var box = group.FirstOrDefault(b => b.Identifier == id);
                if (box is null) continue;
                int currentIndex = host.Children.IndexOf(box.Border);
                if (currentIndex < 0) continue;
                if (currentIndex != targetIndex)
                {
                    host.Children.Remove(box.Border);
                    host.Children.Insert(Math.Min(targetIndex, host.Children.Count), box.Border);
                }
                targetIndex++;
            }
        }
    }
}

/// <summary>One box on a page, identified by the inner StackPanel's name.</summary>
public sealed class BoxRef
{
    public required string Identifier { get; init; }
    public required Border Border { get; init; }
    public required StackPanel InnerPanel { get; init; }
    /// <summary>Mutable — boxes can migrate between hosts via cross-column drag.</summary>
    public required StackPanel HostPanel { get; set; }

    /// <summary>Best-effort human-readable name for the box, used in the hidden-boxes popup.</summary>
    public string DisplayName
    {
        get
        {
            // First ATitle's LabelTitle content is usually the section header — much friendlier
            // than the raw StackPanel name.
            foreach (var child in InnerPanel.Children)
            {
                if (child is PowerAim.UILibrary.ATitle ti
                    && ti.FindName("LabelTitle") is Label lbl
                    && lbl.Content is string s
                    && !string.IsNullOrWhiteSpace(s))
                    return s;
            }
            return Identifier;
        }
    }
}
