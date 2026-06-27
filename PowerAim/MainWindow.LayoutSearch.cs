using Core;
using InputLogic;
using Microsoft.Xaml.Behaviors.Core;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Nextended.Core;
using Nextended.Core.Extensions;
using Nextended.Core.Helper;
using Nextended.UI.Helper;
using Other;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.InputLogic.HidHide;
using PowerAim.Localizations;
using PowerAim.Models;
using PowerAim.MouseMovementLibraries.GHubSupport;
using PowerAim.Other;
using PowerAim.Types;
using PowerAim.UILibrary;
using PowerAim.Visuality;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using UILibrary;
using Visuality;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;

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

    private List<PowerAim.Class.SearchEntry>? _searchIndex;

    private void GlobalSearchButton_Click(object sender, RoutedEventArgs e)
    {
        OpenGlobalSearch();
    }

    private void OpenSearch_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        OpenGlobalSearch();
    }

    private void OpenGlobalSearch()
    {
        // Rebuild on each open — UI may have grown (e.g. profiles added, dialogs hosted).
        _searchIndex = PowerAim.Class.GlobalSearch.BuildIndex(this);
        GlobalSearchPopup.IsOpen = true;
        GlobalSearchBox.Text = "";
        RenderSearchResults("");
        Dispatcher.BeginInvoke(new Action(() => GlobalSearchBox.Focus()),
            System.Windows.Threading.DispatcherPriority.Render);
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => RenderSearchResults(GlobalSearchBox.Text);

    private void GlobalSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            GlobalSearchPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Enter)
        {
            // Activate the first result if there is one.
            if (GlobalSearchResults.Items.Count > 0
                && GlobalSearchResults.Items[0] is FrameworkElement first
                && first.Tag is PowerAim.Class.SearchEntry entry)
            {
                _ = ActivateSearchResult(entry);
            }
            e.Handled = true;
        }
    }

    private void RenderSearchResults(string query)
    {
        GlobalSearchResults.Items.Clear();
        if (_searchIndex is null) return;
        var matches = PowerAim.Class.GlobalSearch.Filter(_searchIndex, query);
        if (matches.Count == 0)
        {
            GlobalSearchHint.Text = Locale.NoMatches;
            return;
        }
        GlobalSearchHint.Text = Locale.SearchMatchesFormat.FormatWith(matches.Count);
        foreach (var entry in matches)
            GlobalSearchResults.Items.Add(BuildResultRow(entry));
    }

    private FrameworkElement BuildResultRow(PowerAim.Class.SearchEntry entry)
    {
        var border = new System.Windows.Controls.Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 2),
            CornerRadius = new CornerRadius(4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = entry,
            Background = System.Windows.Media.Brushes.Transparent,
            ToolTip = string.IsNullOrEmpty(entry.Description) ? null : entry.Description
        };
        var grid = new System.Windows.Controls.Grid();
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

        var sp = new System.Windows.Controls.StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = entry.Label,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextPrimary")
        });
        var sub = new TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Small"),
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextTertiary"),
            Text = string.IsNullOrEmpty(entry.MenuTag)
                ? entry.Category
                : $"{entry.Category}  ·  {entry.MenuTag.Replace("Menu", "")}"
        };
        sp.Children.Add(sub);
        System.Windows.Controls.Grid.SetColumn(sp, 0);
        grid.Children.Add(sp);
        border.Child = grid;

        border.MouseEnter += (_, _) => border.Background = (System.Windows.Media.Brush)FindResource("FluentSurface3");
        border.MouseLeave += (_, _) => border.Background = System.Windows.Media.Brushes.Transparent;
        border.MouseLeftButtonDown += async (_, _) => await ActivateSearchResult(entry);
        return border;
    }

    private async Task ActivateSearchResult(PowerAim.Class.SearchEntry entry)
    {
        GlobalSearchPopup.IsOpen = false;
        // Switch pages if the entry lives on a different one.
        if (!string.IsNullOrEmpty(entry.MenuTag) && CurrentMenu != entry.MenuTag)
        {
            try { await NavigateTo(entry.MenuTag, animate: true); }
            catch { /* navigation failure isn't fatal — flash will still try */ }
            // Give the section layout one render cycle before measuring scroll positions.
            await Task.Delay(220);
        }
        try { PowerAim.Class.GlobalSearch.RevealAndFlash(entry.Target); }
        catch { /* visual tree could be in a transient state — best-effort */ }
    }

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
