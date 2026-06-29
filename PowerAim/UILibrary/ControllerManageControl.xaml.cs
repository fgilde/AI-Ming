using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PowerAim;
using PowerAim.InputLogic.Gamepad;

namespace PowerAim.UILibrary;

/// <summary>How the <see cref="ControllerManageControl"/> behaves: full management vs. a simple picker.</summary>
public enum ControllerListMode { Manage, Select }

/// <summary>
///     Reusable controller list. Shows every detected physical pad AND our virtual pad in one place,
///     tags the current sync source, and (in Manage mode) lets the user pick a different sync source or
///     hide a controller (HidHide if installed, else internal soft-hide). In Select mode it is a picker
///     (e.g. for the gamepad tester): rows are clickable and <see cref="SelectionChanged"/> fires. Built
///     in code-behind (rows) following the <c>HiddenControllersPanel</c> idiom.
/// </summary>
public partial class ControllerManageControl : UserControl
{
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(nameof(Mode), typeof(ControllerListMode), typeof(ControllerManageControl),
            new PropertyMetadata(ControllerListMode.Manage, (d, _) => ((ControllerManageControl)d).Refresh()));

    public ControllerListMode Mode
    {
        get => (ControllerListMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>The row the user picked (Select mode). Null until a selection is made.</summary>
    public ControllerInfo? SelectedController { get; private set; }

    /// <summary>Raised in Select mode when the user clicks a row.</summary>
    public event EventHandler<ControllerInfo?>? SelectionChanged;

    private readonly DispatcherTimer _refresh;
    private string? _selectedId;

    public ControllerManageControl()
    {
        InitializeComponent();
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refresh.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _refresh.Start(); };
        Unloaded += (_, _) => _refresh.Stop();
    }

    private void Refresh()
    {
        if (DevicesPanel == null) return;
        DevicesPanel.Children.Clear();

        List<ControllerInfo> list;
        try { list = ControllerCatalog.Build(); }
        catch (Exception ex) { StatusText.Text = ex.Message; return; }

        if (list.Count == 0)
        {
            DevicesPanel.Children.Add(Muted(Locale.NoHidDevicesDetected));
            StatusText.Text = "";
            return;
        }

        foreach (var info in list)
            DevicesPanel.Children.Add(BuildRow(info));

        // Hint when HidHide isn't installed so the user knows "hide" is PowerAim-internal only.
        StatusText.Text = ControllerCatalog.HidHideAvailable ? "" : Locale.ControllerHideInternalNote;
    }

    private FrameworkElement BuildRow(ControllerInfo info)
    {
        bool selectMode = Mode == ControllerListMode.Select;
        bool selected = selectMode && info.Id == _selectedId;

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Opacity = info.IsConnected ? 1.0 : 0.55,
            Cursor = selectMode ? Cursors.Hand : null,
        };
        border.SetResourceReference(Border.BorderBrushProperty, selected ? "FluentAccent" : "FluentStroke");
        border.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left: connected dot + name, then chips + slot/kind sub-text.
        var left = new StackPanel();
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(new TextBlock
        {
            Text = "●",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Foreground = info.IsConnected ? Brushes.LimeGreen : Brushes.Gray,
        });
        var name = new TextBlock
        {
            Text = info.Name,
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
        nameRow.Children.Add(name);
        left.Children.Add(nameRow);

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        if (info.IsSyncSource) chips.Children.Add(Chip(Locale.ControllerSyncChip, accent: true));
        if (info.IsHidden) chips.Children.Add(Chip(Locale.Hidden, accent: false));
        string sub = info.Kind == ControllerKind.Virtual ? "ViGEm"
                   : info.SlotLabel.Length > 0 ? info.SlotLabel
                   : info.VidPid;
        if (!string.IsNullOrEmpty(sub))
            chips.Children.Add(new TextBlock
            {
                Text = sub,
                FontFamily = new FontFamily("Segoe UI Variable Small"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush?)TryFindResource("FluentTextSecondary") ?? Brushes.Gray,
            });
        left.Children.Add(chips);

        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        // Right: per-row actions — Manage mode only.
        if (!selectMode)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (info.CanBeSyncSource && !info.IsSyncSource)
            {
                var srcBtn = new Button
                {
                    Content = Locale.ControllerUseForSync,
                    MinHeight = 30, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0),
                    Tag = info,
                };
                srcBtn.SetResourceReference(StyleProperty, "FluentStandardButton");
                srcBtn.Click += (_, _) => { ControllerCatalog.SetSyncSource(info); Refresh(); };
                actions.Children.Add(srcBtn);
            }

            if (info.Kind == ControllerKind.Physical)
            {
                var hideBtn = new Button
                {
                    Content = info.IsHidden ? Locale.ShowAgain : Locale.HideFromGames,
                    MinHeight = 30, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0),
                    Tag = info,
                };
                hideBtn.SetResourceReference(StyleProperty, info.IsHidden ? "FluentAccentButton" : "FluentStandardButton");
                hideBtn.Click += (_, _) => { ControllerCatalog.SetHidden(info, !info.IsHidden); Refresh(); };
                actions.Children.Add(hideBtn);
            }

            Grid.SetColumn(actions, 1);
            grid.Children.Add(actions);
        }

        border.Child = grid;

        if (selectMode)
            border.MouseLeftButtonUp += (_, _) =>
            {
                _selectedId = info.Id;
                SelectedController = info;
                SelectionChanged?.Invoke(this, info);
                Refresh();
            };

        return border;
    }

    private Border Chip(string text, bool accent)
    {
        var b = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1, 6, 2),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        b.SetResourceReference(Border.BackgroundProperty, accent ? "FluentAccent" : "FluentSurface2");
        var t = new TextBlock { Text = text, FontSize = 10, FontFamily = new FontFamily("Segoe UI Variable Small") };
        t.SetResourceReference(TextBlock.ForegroundProperty, accent ? "FluentAccentForeground" : "FluentTextSecondary");
        b.Child = t;
        return b;
    }

    private TextBlock Muted(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI Variable Small"),
        FontSize = 12,
        Margin = new Thickness(0, 4, 0, 0),
        Foreground = (Brush?)TryFindResource("FluentTextTertiary") ?? Brushes.Gray,
    };
}
