using System.Windows;
using PowerAim.UILibrary;

namespace PowerAim.Visuality;

/// <summary>
///     Dialog around a single <see cref="GildeConnectPanel"/>. The contact form and the support
///     options are tall enough that embedding them straight into the About page pushed everything
///     else out of sight, so About only shows two buttons and the actual widget opens here.
///     <para>
///     Non-modal on purpose — same as <see cref="HelpWindow"/>: writing a message should not freeze
///     the rest of the app. One window per widget kind, so a second click focuses the open one
///     instead of stacking duplicates.
///     </para>
/// </summary>
public partial class ConnectWindow : Window
{
    private static readonly Dictionary<string, ConnectWindow> Open = new();

    private readonly string _key;

    private ConnectWindow(string widget, string title)
    {
        InitializeComponent();
        _key = widget;
        Title = title;
        Panel.Widget = widget;
        Panel.WidgetTitle = title;
        Closed += (_, _) => Open.Remove(_key);
    }

    /// <summary>Shows the widget, reusing an already open window for the same kind.</summary>
    public static void Show(string widget, string title, Window? owner)
    {
        if (Open.TryGetValue(widget, out var existing))
        {
            existing.Activate();
            return;
        }

        var window = new ConnectWindow(widget, title);
        // Only set an owner that is actually on screen — an owner that is still loading throws.
        if (owner is { IsLoaded: true } && !ReferenceEquals(owner, window)) window.Owner = owner;
        Open[widget] = window;
        window.Show();
    }
}
