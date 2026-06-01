using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.Extensions;

namespace PowerAim.UILibrary;

/// <summary>
///     Reusable, recursive editor for an <see cref="OcrConditionGroup"/>. Renders:
///     <list type="bullet">
///       <item>An <b>operator chip</b> (AND / OR) for the group at this level.</item>
///       <item>Each child node — either inline as a leaf row (region · comparison · value · remove)
///             or recursively as a nested <see cref="OcrConditionBuilder"/>.</item>
///       <item><b>+ Condition</b> appends a new <see cref="OcrConditionLeaf"/>.</item>
///       <item><b>+ Group</b> appends a new nested <see cref="OcrConditionGroup"/>.</item>
///     </list>
///     The control mutates the bound <see cref="Group"/> in place — callers should own the tree
///     and snapshot it via <c>Clone()</c> when they need rollback (e.g. BeginEdit on ActionTrigger).
///     <para>
///     Bound by assigning <see cref="Group"/> from the parent. <see cref="ShowOperatorChip"/> can
///     be set to <c>false</c> on the root level if you want a label-less plain group (the trigger
///     editor uses that to avoid a redundant "AND" chip at the very top).
///     </para>
/// </summary>
public partial class OcrConditionBuilder : UserControl
{
    public static readonly DependencyProperty GroupProperty =
        DependencyProperty.Register(nameof(Group), typeof(OcrConditionGroup), typeof(OcrConditionBuilder),
            new PropertyMetadata(null, OnGroupChanged));

    public OcrConditionGroup? Group
    {
        get => (OcrConditionGroup?)GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    public static readonly DependencyProperty ShowOperatorChipProperty =
        DependencyProperty.Register(nameof(ShowOperatorChip), typeof(bool), typeof(OcrConditionBuilder),
            new PropertyMetadata(true, (d, _) => ((OcrConditionBuilder)d).Rebuild()));

    /// <summary>Hide the AND/OR chip on the root level (the parent caller already labelled it).</summary>
    public bool ShowOperatorChip
    {
        get => (bool)GetValue(ShowOperatorChipProperty);
        set => SetValue(ShowOperatorChipProperty, value);
    }

    public static readonly DependencyProperty ShowConfigureRegionsButtonProperty =
        DependencyProperty.Register(nameof(ShowConfigureRegionsButton), typeof(bool), typeof(OcrConditionBuilder),
            new PropertyMetadata(true, (d, _) => ((OcrConditionBuilder)d).Rebuild()));

    /// <summary>
    ///     When true (default), the operator row gets a "Configure regions…" button on the right
    ///     that opens the OCR regions configurator inline. The parent rebuild repopulates region
    ///     ComboBoxes when the dialog closes so newly added regions appear immediately. Nested
    ///     groups set this to false — only the root builder shows the button.
    /// </summary>
    public bool ShowConfigureRegionsButton
    {
        get => (bool)GetValue(ShowConfigureRegionsButtonProperty);
        set => SetValue(ShowConfigureRegionsButtonProperty, value);
    }

    /// <summary>Fires after any structural edit (add/remove/operator change). Useful for dirty-tracking.</summary>
    public event EventHandler? TreeChanged;

    public OcrConditionBuilder()
    {
        InitializeComponent();
    }

    private static void OnGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((OcrConditionBuilder)d).Rebuild();
    }

    // ====================================================================== BUILD ====

    private void Rebuild()
    {
        Root.Children.Clear();
        if (Group is null) return;

        // The header row hosts the AND/OR chip + hint + "Configure regions…" button. Either
        // visible-portion is independently optional, but we always build the row if at least one
        // is shown — that keeps the button reachable even when the chip is hidden (the AntiOcr
        // section in TriggerEdit uses that combo).
        if (ShowOperatorChip || ShowConfigureRegionsButton)
            Root.Children.Add(BuildHeaderRow());

        // Discover the OCR-region names once per rebuild so every leaf-row ComboBox sees the
        // same set. Empty list is handled below with a hint that links the user back to the
        // OCR-regions configurator (button in the header row still works, so the user has an
        // immediate way to fix it).
        var regions = AppConfig.Current?.OcrSettings?.Regions
                          .Select(r => r.Name)
                          .Where(n => !string.IsNullOrWhiteSpace(n))
                          .Distinct()
                          .ToList() ?? new List<string>();

        if (regions.Count == 0)
        {
            Root.Children.Add(new TextBlock
            {
                Text = Locale.OcrConditionsNoRegions,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 4, 0, 4),
                Foreground = (Brush)FindResource("FluentTextTertiary"),
                FontFamily = new FontFamily("Segoe UI Variable Small"),
                FontSize = 11,
            });
            return;
        }

        // Snapshot to allow removal mid-iteration via remove handlers.
        foreach (var child in Group.Children.ToList())
        {
            switch (child)
            {
                case OcrConditionLeaf leaf:
                    Root.Children.Add(BuildLeafRow(leaf, regions));
                    break;
                case OcrConditionGroup grp:
                    Root.Children.Add(BuildNestedGroup(grp));
                    break;
            }
        }

        Root.Children.Add(BuildAddRow(regions));
    }

    private FrameworkElement BuildHeaderRow()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // op combo
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // hint
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // configure regions

        if (ShowOperatorChip)
        {
            var combo = new ComboBox
            {
                Width = 78,
                VerticalAlignment = VerticalAlignment.Center,
            };
            combo.Items.Add(MakeOpItem("AND", OcrLogicOp.And));
            combo.Items.Add(MakeOpItem("OR",  OcrLogicOp.Or));
            combo.SelectedIndex = Group!.Op == OcrLogicOp.Or ? 1 : 0;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ComboBoxItem { Tag: OcrLogicOp op })
                {
                    Group.Op = op;
                    TreeChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            Grid.SetColumn(combo, 0);
            grid.Children.Add(combo);

            var hint = new TextBlock
            {
                Text = Locale.OcrConditionGroupHint,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = (Brush)FindResource("FluentTextTertiary"),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(hint, 1);
            grid.Children.Add(hint);
        }

        if (ShowConfigureRegionsButton)
        {
            // Right-aligned button: opens the OCR regions configurator and rebuilds this control
            // when the dialog closes so newly added regions become pickable immediately.
            var configureBtn = new Button
            {
                Padding = new Thickness(10, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = Locale.EditOcrRegionsButtonTooltip,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            configureBtn.SetResourceReference(FrameworkElement.StyleProperty, "FluentStandardButton");
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            });
            content.Children.Add(new TextBlock
            {
                Text = Locale.EditOcrRegionsButton,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
            });
            configureBtn.Content = content;
            configureBtn.Click += ConfigureRegions_Click;
            Grid.SetColumn(configureBtn, 2);
            grid.Children.Add(configureBtn);
        }
        return grid;
    }

    private void ConfigureRegions_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new PowerAim.Visuality.OcrRegionsDialog
            {
                Owner = Window.GetWindow(this),
            };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OcrConditionBuilder] OcrRegionsDialog failed: {ex.Message}");
        }
        // Repopulate region ComboBoxes (and re-evaluate the empty-state branch) so newly added
        // regions become pickable without the parent having to reload.
        Rebuild();
    }

    private static ComboBoxItem MakeOpItem(string text, OcrLogicOp op) => new()
    {
        Content = text,
        Tag = op,
    };

    private FrameworkElement BuildLeafRow(OcrConditionLeaf leaf, List<string> regions)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var regionCombo = new ComboBox
        {
            ItemsSource = regions,
            SelectedItem = regions.Contains(leaf.RegionName) ? leaf.RegionName : null,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        regionCombo.SelectionChanged += (_, _) =>
        {
            leaf.RegionName = regionCombo.SelectedItem as string ?? "";
            TreeChanged?.Invoke(this, EventArgs.Empty);
        };
        Grid.SetColumn(regionCombo, 0);
        grid.Children.Add(regionCombo);

        var compCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues<OcrComparison>(),
            SelectedItem = leaf.Comparison,
            Margin = new Thickness(0, 0, 6, 0),
            MinWidth = 78,
            VerticalAlignment = VerticalAlignment.Center,
        };
        compCombo.SelectionChanged += (_, _) =>
        {
            if (compCombo.SelectedItem is OcrComparison c)
            {
                leaf.Comparison = c;
                TreeChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        Grid.SetColumn(compCombo, 1);
        grid.Children.Add(compCombo);

        var valueBox = new TextBox
        {
            Text = leaf.Value,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        valueBox.TextChanged += (_, _) =>
        {
            leaf.Value = valueBox.Text;
            TreeChanged?.Invoke(this, EventArgs.Empty);
        };
        Grid.SetColumn(valueBox, 2);
        grid.Children.Add(valueBox);

        var removeBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 12,
            },
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Locale.Delete,
        };
        removeBtn.SetResourceReference(FrameworkElement.StyleProperty, "FluentStandardButton");
        removeBtn.Click += (_, _) =>
        {
            Group!.Children.Remove(leaf);
            Rebuild();
            TreeChanged?.Invoke(this, EventArgs.Empty);
        };
        Grid.SetColumn(removeBtn, 3);
        grid.Children.Add(removeBtn);

        return grid;
    }

    private FrameworkElement BuildNestedGroup(OcrConditionGroup nested)
    {
        // Nested groups get an indented host with a remove button in the corner. The recursive
        // OcrConditionBuilder inside renders its own +/- buttons for its own children.
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var child = new OcrConditionBuilder
        {
            Group = nested,
            Margin = new Thickness(16, 0, 0, 0), // visual indent for nesting
            // Nested groups don't repeat the configure-regions button — it'd be visual noise.
            // The root builder owns that affordance.
            ShowConfigureRegionsButton = false,
        };
        // Bubble the inner-tree changes up to our own subscribers so parents see all edits.
        child.TreeChanged += (_, _) => TreeChanged?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(child, 0);
        grid.Children.Add(child);

        var removeBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 12,
            },
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6, 4, 0, 0),
            ToolTip = Locale.OcrConditionRemoveGroup,
        };
        removeBtn.SetResourceReference(FrameworkElement.StyleProperty, "FluentStandardButton");
        removeBtn.Click += (_, _) =>
        {
            Group!.Children.Remove(nested);
            Rebuild();
            TreeChanged?.Invoke(this, EventArgs.Empty);
        };
        Grid.SetColumn(removeBtn, 1);
        grid.Children.Add(removeBtn);

        return grid;
    }

    private FrameworkElement BuildAddRow(List<string> regions)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var addCondBtn = new Button
        {
            Content = Locale.AddCondition,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
        };
        addCondBtn.SetResourceReference(FrameworkElement.StyleProperty, "FluentStandardButton");
        addCondBtn.Click += (_, _) =>
        {
            Group!.Children.Add(new OcrConditionLeaf { RegionName = regions.FirstOrDefault() ?? "" });
            Rebuild();
            TreeChanged?.Invoke(this, EventArgs.Empty);
        };
        stack.Children.Add(addCondBtn);

        var addGroupBtn = new Button
        {
            Content = Locale.OcrConditionAddGroup,
            Padding = new Thickness(12, 6, 12, 6),
        };
        addGroupBtn.SetResourceReference(FrameworkElement.StyleProperty, "FluentStandardButton");
        addGroupBtn.Click += (_, _) =>
        {
            Group!.Children.Add(new OcrConditionGroup { Op = OcrLogicOp.And });
            Rebuild();
            TreeChanged?.Invoke(this, EventArgs.Empty);
        };
        stack.Children.Add(addGroupBtn);

        return stack;
    }
}
