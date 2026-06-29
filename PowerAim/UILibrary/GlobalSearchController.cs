using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Nextended.Core.Extensions;
using PowerAim.Class;
using PowerAim.Localizations;

namespace PowerAim.UILibrary;

/// <summary>
///     Owns the Ctrl+F global settings search: (re)builds the index over the live UI on open,
///     renders the drop-down results, and activates a hit (navigate to its page, then reveal +
///     flash the control). The host wires up the five chrome elements plus the navigation hooks;
///     all search behaviour lives here.
/// </summary>
internal sealed class GlobalSearchController
{
    private readonly FrameworkElement _host;
    private readonly Popup _popup;
    private readonly TextBox _box;
    private readonly ItemsControl _results;
    private readonly TextBlock _hint;
    private readonly Func<string?> _currentMenu;
    private readonly Func<string, bool, Task> _navigateTo;

    private List<SearchEntry>? _index;

    public GlobalSearchController(FrameworkElement host, Button button, Popup popup, TextBox box,
        ItemsControl results, TextBlock hint, Func<string?> currentMenu, Func<string, bool, Task> navigateTo)
    {
        _host = host;
        _popup = popup;
        _box = box;
        _results = results;
        _hint = hint;
        _currentMenu = currentMenu;
        _navigateTo = navigateTo;

        button.Click += (_, _) => Open();
        _box.TextChanged += (_, _) => Render(_box.Text);
        _box.KeyDown += OnBoxKeyDown;
    }

    /// <summary>Open the popup, rebuild the index (the UI may have grown) and focus the box.</summary>
    public void Open()
    {
        _index = GlobalSearch.BuildIndex(_host);
        _popup.IsOpen = true;
        _box.Text = "";
        Render("");
        _box.Dispatcher.BeginInvoke(new Action(() => _box.Focus()),
            System.Windows.Threading.DispatcherPriority.Render);
    }

    private void OnBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _popup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (_results.Items.Count > 0 && _results.Items[0] is FrameworkElement { Tag: SearchEntry entry })
                _ = Activate(entry);
            e.Handled = true;
        }
    }

    private void Render(string query)
    {
        _results.Items.Clear();
        if (_index is null) return;

        var matches = GlobalSearch.Filter(_index, query);
        if (matches.Count == 0)
        {
            _hint.Text = Locale.NoMatches;
            return;
        }

        _hint.Text = Locale.SearchMatchesFormat.FormatWith(matches.Count);
        foreach (var entry in matches)
            _results.Items.Add(BuildResultRow(entry));
    }

    private Border BuildResultRow(SearchEntry entry)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = entry.Label,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("FluentTextPrimary"),
        });
        text.Children.Add(new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Small"),
            FontSize = 11,
            Foreground = Brush("FluentTextTertiary"),
            Text = string.IsNullOrEmpty(entry.MenuTag)
                ? entry.Category
                : $"{entry.Category}  ·  {entry.MenuTag.Replace("Menu", "")}",
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var row = new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 2),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand,
            Tag = entry,
            Background = Brushes.Transparent,
            ToolTip = string.IsNullOrEmpty(entry.Description) ? null : entry.Description,
            Child = grid,
        };
        row.MouseEnter += (_, _) => row.Background = Brush("FluentSurface3");
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        row.MouseLeftButtonDown += async (_, _) => await Activate(entry);
        return row;
    }

    private async Task Activate(SearchEntry entry)
    {
        _popup.IsOpen = false;
        // Switch pages first if the hit lives elsewhere, then give layout one render cycle.
        if (!string.IsNullOrEmpty(entry.MenuTag) && _currentMenu() != entry.MenuTag)
        {
            try { await _navigateTo(entry.MenuTag, true); }
            catch { /* navigation failure isn't fatal — the flash will still try */ }
            await Task.Delay(220);
        }
        try { GlobalSearch.RevealAndFlash(entry.Target); }
        catch { /* visual tree may be transient — best-effort */ }
    }

    private Brush Brush(string key) => (Brush)_host.FindResource(key);
}
