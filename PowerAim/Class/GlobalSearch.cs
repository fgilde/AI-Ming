using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PowerAim.UILibrary;

namespace PowerAim.Class;

/// <summary>
///     One result-entry in <see cref="GlobalSearch"/>. Holds the label string we matched against,
///     the actual control we want to scroll to / flash, and an optional menu Tag that lets the
///     navigator switch pages before scrolling (most controls live inside a collapsed ScrollViewer
///     for whichever page they're on).
/// </summary>
public sealed class SearchEntry
{
    public string Label { get; init; } = "";
    public string Category { get; init; } = "";
    public FrameworkElement Target { get; init; } = null!;
    public string? MenuTag { get; init; }

    /// <summary>
    ///     Extra searchable text — tooltips, help captions, descriptions. Shown as a faint
    ///     subtitle on the result row when it's the reason for a match.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
///     Indexes labelled controls (AToggle, ASlider, AKeyChanger, AColorChanger, ATitle, plus plain
///     Buttons with text content) across the whole MainWindow visual tree, filters them by query,
///     and on selection scrolls the matched control into view + flashes its border so the user
///     can see where the setting lives.
///     <para>
///     The index is rebuilt on every popup-open so newly-instantiated UI (e.g. dialogs that hosted
///     templated content) is captured. Cost is negligible — a few hundred elements at most.
///     </para>
/// </summary>
public static class GlobalSearch
{
    /// <summary>
    ///     Walk the visual tree of <paramref name="root"/> and emit a <see cref="SearchEntry"/>
    ///     for every labelled control encountered.
    /// </summary>
    public static List<SearchEntry> BuildIndex(DependencyObject root)
    {
        var entries = new List<SearchEntry>();
        Walk(root, entries, currentMenu: null);
        // De-duplicate on (Label + Target) — XAML sometimes wraps the same control in a host.
        var seen = new HashSet<(string, FrameworkElement)>();
        return entries.Where(e => seen.Add((e.Label, e.Target))).ToList();
    }

    private static void Walk(DependencyObject node, List<SearchEntry> sink, string? currentMenu)
    {
        if (node is FrameworkElement fe)
        {
            // Detect menu page boundaries — anything with a name matching the menu Tags qualifies.
            // The MenuTag drives the page-switch when the user clicks a result.
            if (!string.IsNullOrEmpty(fe.Name) && IsMenuPageName(fe.Name))
                currentMenu = fe.Name;
        }

        switch (node)
        {
            case AToggle t when !string.IsNullOrWhiteSpace(t.Text):
                sink.Add(new SearchEntry { Label = t.Text, Category = "Toggle", Target = t, MenuTag = currentMenu, Description = ExtractTooltip(t) });
                break;
            case ASlider s when !string.IsNullOrWhiteSpace(s.Text):
                sink.Add(new SearchEntry { Label = s.Text, Category = "Slider", Target = s, MenuTag = currentMenu, Description = ExtractTooltip(s) });
                break;
            case AKeyChanger k when !string.IsNullOrWhiteSpace(k.Text):
                sink.Add(new SearchEntry { Label = k.Text, Category = "Keybind", Target = k, MenuTag = currentMenu, Description = ExtractTooltip(k) });
                break;
            case AColorChanger c when !string.IsNullOrWhiteSpace(c.Title):
                sink.Add(new SearchEntry { Label = c.Title, Category = "Color", Target = c, MenuTag = currentMenu, Description = ExtractTooltip(c) });
                break;
            case ATitle ti when ti.FindName("LabelTitle") is Label lbl && lbl.Content is string s2 && !string.IsNullOrWhiteSpace(s2):
                sink.Add(new SearchEntry { Label = s2, Category = "Section", Target = ti, MenuTag = currentMenu, Description = ExtractTooltip(ti) });
                break;
            case Button b when b.Visibility == Visibility.Visible && ExtractButtonLabel(b) is string bl:
                sink.Add(new SearchEntry { Label = bl, Category = "Button", Target = b, MenuTag = currentMenu, Description = ExtractTooltip(b) });
                break;
            case ACredit cr:
                // ACredit = a labelled help/info row. Both Title and Description are searchable
                // because they often hold long-form text that's not represented anywhere else.
                {
                    string title = ExtractLabelControlContent(cr, "NameLabel") ?? "";
                    string desc  = ExtractLabelControlContent(cr, "Description") ?? "";
                    if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(desc))
                    {
                        // If the title is missing (common for free-floating help blocks), surface the
                        // first ~50 chars of the description as the searchable label.
                        string effectiveLabel = string.IsNullOrWhiteSpace(title)
                            ? (desc.Length > 60 ? desc[..60] + "…" : desc)
                            : title;
                        sink.Add(new SearchEntry
                        {
                            Label = effectiveLabel,
                            Category = "Help",
                            Target = cr,
                            MenuTag = currentMenu,
                            Description = desc
                        });
                    }
                }
                break;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < childCount; i++)
            Walk(VisualTreeHelper.GetChild(node, i), sink, currentMenu);
    }

    private static string? ExtractButtonLabel(Button b)
    {
        switch (b.Content)
        {
            case string s when s.Length is > 1 and < 60: return s;
            case TextBlock tb when !string.IsNullOrWhiteSpace(tb.Text) && tb.Text.Length < 60:
                // Skip icon-only buttons (segoe glyphs are typically a single private-use char).
                return tb.Text.Length > 1 ? tb.Text : null;
            default: return null;
        }
    }

    /// <summary>
    ///     Read the <c>ToolTip</c> property as a string for any FrameworkElement. WPF tooltips can
    ///     be strings, TextBlocks, or full controls — we accept the first two and ignore the rest.
    /// </summary>
    private static string? ExtractTooltip(FrameworkElement fe)
    {
        var t = fe.ToolTip;
        return t switch
        {
            string s when !string.IsNullOrWhiteSpace(s) => s,
            TextBlock tb when !string.IsNullOrWhiteSpace(tb.Text) => tb.Text,
            _ => null,
        };
    }

    /// <summary>
    ///     Read the textual content of a named Label child inside a UserControl. Used for ACredit
    ///     where the title + description live on two fields.
    /// </summary>
    private static string? ExtractLabelControlContent(FrameworkElement host, string fieldName)
    {
        var node = host.FindName(fieldName);
        return node switch
        {
            Label lbl => lbl.Content as string,
            TextBlock tb => tb.Text,
            ContentControl cc => cc.Content as string,
            _ => null,
        };
    }

    private static readonly HashSet<string> _menuPageNames = new(StringComparer.Ordinal)
    {
        "AimMenu", "ModelMenu", "SettingsMenu", "AutoPlayMenu",
        "Tools", "Logs", "AboutMenu", "GamepadSettings", "GamepadTestPage"
    };

    public static bool IsMenuPageName(string name) => _menuPageNames.Contains(name);

    /// <summary>
    ///     Filter the precomputed <paramref name="all"/> entries down to those whose label or
    ///     category contains the query (case-insensitive). Empty query returns top-N entries.
    /// </summary>
    public static List<SearchEntry> Filter(IReadOnlyList<SearchEntry> all, string query, int max = 40)
    {
        if (string.IsNullOrWhiteSpace(query)) return all.Take(max).ToList();
        var q = query.Trim();
        return all
            .Where(e => Matches(e, q))
            .OrderBy(e => RankScore(e, q))
            .ThenBy(e => e.Label.Length)
            .Take(max)
            .ToList();
    }

    private static bool Matches(SearchEntry e, string q) =>
        e.Label.Contains(q, StringComparison.OrdinalIgnoreCase)
        || e.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
        || (e.Description != null && e.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
        || (e.MenuTag != null && e.MenuTag.Contains(q, StringComparison.OrdinalIgnoreCase));

    private static int RankScore(SearchEntry e, string q)
    {
        // Lower = better. Prefer prefix matches in the label, then substring in label, then
        // description matches, then category/page only.
        if (e.Label.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 0;
        if (e.Label.Contains(q, StringComparison.OrdinalIgnoreCase)) return 10;
        if (e.Description != null && e.Description.Contains(q, StringComparison.OrdinalIgnoreCase)) return 30;
        return 50;
    }

    /// <summary>
    ///     Scroll the target into view inside its hosting <see cref="ScrollViewer"/> and play a
    ///     short border-flash animation so the user's eye lands on the right place.
    /// </summary>
    public static void RevealAndFlash(FrameworkElement target)
    {
        target.BringIntoView();

        // Ascend visual tree to find the first Border we can pulse. Most A* controls are wrapped
        // in or contain a Border somewhere; if none is found we flash the FrameworkElement's own
        // Opacity instead so something still happens.
        var border = FindBorderUp(target) ?? FindBorderDown(target);
        if (border != null)
        {
            var originalBrush = border.BorderBrush;
            var originalThickness = border.BorderThickness;
            var accent = Application.Current.TryFindResource("FluentAccent") as Brush ?? Brushes.MediumPurple;
            border.BorderBrush = accent;
            border.BorderThickness = new Thickness(2);
            var fade = new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(180))
            { AutoReverse = true, RepeatBehavior = new RepeatBehavior(2) };
            fade.Completed += (_, _) =>
            {
                border.BorderBrush = originalBrush;
                border.BorderThickness = originalThickness;
            };
            border.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        else
        {
            var fade = new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(180))
            { AutoReverse = true, RepeatBehavior = new RepeatBehavior(2) };
            target.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }

    private static Border? FindBorderUp(DependencyObject node)
    {
        for (int i = 0; i < 6 && node != null; i++)
        {
            if (node is Border b) return b;
            node = VisualTreeHelper.GetParent(node)!;
            if (node == null) break;
        }
        return null;
    }

    private static Border? FindBorderDown(DependencyObject node)
    {
        if (node is Border b0) return b0;
        int n = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Border b) return b;
            var nested = FindBorderDown(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
