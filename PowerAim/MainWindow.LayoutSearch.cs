using Nextended.Core.Extensions;
using PowerAim.Other;
using PowerAim.Config;
using PowerAim.InputLogic;
using PowerAim.Localizations;
using PowerAim.MouseMovementLibraries.GHubSupport;
using PowerAim.Types;
using PowerAim.Visuality;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;

namespace PowerAim;

public partial class MainWindow
{
    // ===================================================================== LAYOUT MANAGER ====

    private readonly Dictionary<string, PageLayoutManager> _pageLayouts = new();
    private HiddenBoxesPill? _hiddenBoxesPill;

    private static readonly string[] _layoutManagedPages =
    [
        "AimMenu", "ModelMenu", "SettingsMenu", "AutoPlayMenu",
        "Tools", "Logs", "AboutMenu", "GamepadSettings"
    ];

    /// <summary>
    ///     Attach a <see cref="PageLayoutManager"/> to whichever pages already
    ///     have a fully-realised visual tree. Collapsed pages still lazy-attach on first nav via
    ///     <see cref="EnsurePageAttached"/>. Called after <see cref="CreateUI"/> finishes.
    /// </summary>
    private void AttachLayoutManagers()
    {
        // Do NOT clear _pageLayouts here. CreateUI() re-runs on every config load / language
        // change, but it only repopulates the inner StackPanels — the FluentCard Borders, named
        // panels and their already-attached chrome (drag + hide-×) survive. Throwing the managers
        // away and re-Attaching would yield empty new managers (DiscoverBoxes skips borders already
        // tagged from the first attach) while the × buttons still drive the old manager — so the
        // hidden-boxes pill would never update. Keeping the existing managers keeps the pill bound
        // to the same manager the × buttons use. EnsurePageAttached skips pages already attached.
        foreach (var name in _layoutManagedPages)
            EnsurePageAttached(name);
    }

    /// <summary>
    ///     Lazy attach for a single page. Idempotent in the success case.
    ///     <para>
    ///     A collapsed ScrollViewer hasn't been measured yet, so its template isn't applied
    ///     and its visual tree is empty — the initial bulk pass in <see cref="AttachLayoutManagers"/>
    ///     therefore inserts an empty PageLayoutManager for every off-screen page. When the user
    ///     later navigates to that page (it goes Visible, WPF realises the template), we need to
    ///     re-attach. So: if the existing entry has zero boxes, drop it and try again. Pages that
    ///     genuinely have no boxes will just keep ending up with an empty manager — harmless.
    ///     </para>
    /// </summary>
    private void EnsurePageAttached(string name)
    {
        if (_pageLayouts.TryGetValue(name, out var existing) && existing.Boxes.Count > 0)
        {
            // Page already instrumented (its boxes survive CreateUI). On a config switch the new
            // config's layout must still be applied — otherwise the previous config's hidden/order
            // state sticks. ReapplyLayout reads the now-current AppConfig.LayoutConfiguration.
            existing.ReapplyLayout();
            return;
        }
        if (FindName(name) is not FrameworkElement page) return;
        var mgr = PageLayoutManager.Attach(name, page);
        // Persist the layout as soon as the user hides/restores/reorders a section. The layout
        // lives in AppConfig.LayoutConfiguration (per-config), but nothing saved it before — so it
        // only stuck by accident on the next config save. LayoutChanged fires on user actions only
        // (not during Attach/ApplyPersistedLayout), so this won't save on plain config load.
        mgr.LayoutChanged += PersistLayoutConfig;
        _pageLayouts[name] = mgr;
    }

    private void PersistLayoutConfig()
    {
        try
        {
            if (!string.IsNullOrEmpty(AppConfig.Current?.Path))
                AppConfig.Current.Save();
        }
        catch { /* never let a layout tweak crash the UI */ }
    }

    private void EnsureHiddenBoxesPill()
    {
        if (_hiddenBoxesPill is not null) return;
        // Inject the pill into the outermost Grid that hosts the page area. The first child of
        // MainWindow is a Grid (the row/column layout); we put the pill there with high Z-index.
        if (Content is Grid root)
            _hiddenBoxesPill = new HiddenBoxesPill(root);
    }

    private void BindHiddenBoxesPillForCurrentPage()
    {
        EnsureHiddenBoxesPill();
        _pageLayouts.TryGetValue(CurrentMenu ?? "", out var mgr);
        _hiddenBoxesPill?.Bind(mgr);
    }

    // ===================================================================== GLOBAL SEARCH ====

    /// <summary>Bound to Ctrl+F via Window.InputBindings in XAML.</summary>
    public static readonly System.Windows.Input.RoutedUICommand OpenSearchCommand =
        new("Open Search", nameof(OpenSearchCommand), typeof(MainWindow));


    private const double SidebarCompactWidth = 48;
    private const double SidebarExpandedWidth = 220;
    private bool _sidebarExpanded;

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        _sidebarExpanded = !_sidebarExpanded;
        var target = _sidebarExpanded ? SidebarExpandedWidth : SidebarCompactWidth;
        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        Sidebar.BeginAnimation(FrameworkElement.WidthProperty, anim);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _magnifier?.Dispose();
        _fileManager.Dispose();
        FileManager.AIManager?.Dispose();
        GamepadManager.Dispose();

        FOV.Instance?.Close();

        if (AppConfig.Current.DropdownState.MouseMovementMethod == MouseMovementMethod.LGHUB) LGMouse.Close();

        AppConfig.Current.Save();
        Application.Current.Shutdown();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

}
