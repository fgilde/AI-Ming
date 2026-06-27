using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     Saved layout of a single menu page: order of boxes (FluentCard sections) and which of them
///     are currently hidden. Identified by the inner StackPanel's <c>x:Name</c> (stable across
///     builds — those names are referenced from XAML).
/// </summary>
public class PageLayout : BaseSettings
{
    /// <summary>
    ///     [DEPRECATED — read on load for back-compat; new saves go through <see cref="Columns"/>.]
    ///     Flat top-to-bottom order across all hosts on the page.
    /// </summary>
    public ObservableCollection<string> Order
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>
    ///     Identifiers the user has hidden. Hidden boxes don't appear in the column ordering —
    ///     they're tucked away until restored via the bottom-right pill.
    /// </summary>
    public ObservableCollection<string> Hidden
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>
    ///     Multi-column ordering. Column index maps to the host StackPanel's position within the
    ///     page (0 = first host encountered when walking top-down, 1 = second, …). The list under
    ///     each key is the top-to-bottom box-identifier order for that column.
    ///     <para>
    ///     A box that's referenced here but doesn't exist in the current build is silently
    ///     ignored; a box that exists but isn't referenced lands at the end of its XAML-default
    ///     column. This keeps the layout robust against refactors that add or rename boxes.
    ///     </para>
    /// </summary>
    public Dictionary<int, List<string>> Columns
    {
        get;
        set => SetField(ref field, value);
    } = new();
}

/// <summary>
///     Per-page layout configuration. Keyed by menu page name (e.g. <c>"AimMenu"</c>,
///     <c>"SettingsMenu"</c>). Missing entries fall back to the XAML-defined layout.
/// </summary>
public class LayoutConfiguration : BaseSettings
{
    public Dictionary<string, PageLayout> Pages
    {
        get;
        set => SetField(ref field, value);
    } = new();

    /// <summary>Get or create the layout entry for <paramref name="pageName"/>.</summary>
    public PageLayout For(string pageName)
    {
        if (!Pages.TryGetValue(pageName, out var layout))
        {
            layout = new PageLayout();
            Pages[pageName] = layout;
        }
        return layout;
    }
}
