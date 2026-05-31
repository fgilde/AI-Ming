using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim;
using PowerAim.Class.Native;
using PowerAim.Config;

namespace PowerAim.Visuality;

/// <summary>
///     Editor for <see cref="AppConfig.AimDisengageRules"/>: a list of OCR-driven rules that pause
///     aim assist while a HUD value matches (e.g. while scoped). Edits the live collection — closing
///     the dialog keeps whatever rows are present (parity with the other config dialogs).
/// </summary>
public partial class AimDisengageDialog
{
    public AimDisengageDialog()
    {
        InitializeComponent();
        DataContext = this;
        BuildRows();
    }

    private List<string> RegionNames() => AppConfig.Current.OcrSettings.Regions
        .Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

    private void BuildRows()
    {
        RulesBox.Children.Clear();
        var regionNames = RegionNames();

        if (regionNames.Count == 0)
        {
            RulesBox.Children.Add(new TextBlock
            {
                Text = Locale.OcrConditionsNoRegions,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 0, 4),
                Foreground = (Brush)FindResource("FluentTextTertiary"),
                FontFamily = new FontFamily("Segoe UI Variable Small"),
                FontSize = 12
            });
            AddBtn.IsEnabled = false;
            return;
        }

        AddBtn.IsEnabled = true;
        RulesBox.Children.Add(BuildHeaderRow());
        foreach (var rule in AppConfig.Current.AimDisengageRules.ToList())
            RulesBox.Children.Add(BuildRow(rule, regionNames));
    }

    /// <summary>
    ///     Header strip above the rule cards: labels the columns so users know which textbox is
    ///     "Value" (what the OCR reading must match) vs. "Game" (process-name pattern). Same grid
    ///     widths as <see cref="BuildRow"/> so it lines up.
    /// </summary>
    private FrameworkElement BuildHeaderRow()
    {
        var headerGrid = new Grid { Margin = new Thickness(10, 0, 10, 6) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        TextBlock Label(string text, int col, Thickness pad)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = (Brush)FindResource("FluentTextTertiary"),
                FontFamily = new FontFamily("Segoe UI Variable Small"),
                FontSize = 11,
                Margin = pad,
            };
            Grid.SetColumn(tb, col);
            return tb;
        }
        // Column 0 = enabled checkbox — labelled "On" so users connect the dot.
        headerGrid.Children.Add(Label(Locale.Enabled, 0, new Thickness(0, 0, 16, 0)));
        headerGrid.Children.Add(Label(Locale.AimDisengageColumnRegion,     1, new Thickness(0, 0, 6, 0)));
        headerGrid.Children.Add(Label(Locale.AimDisengageColumnComparison, 2, new Thickness(0, 0, 6, 0)));
        headerGrid.Children.Add(Label(Locale.AimDisengageColumnValue,      3, new Thickness(0, 0, 6, 0)));
        headerGrid.Children.Add(Label(Locale.AimDisengageColumnGame,       4, new Thickness(0, 0, 6, 0)));
        // Column 5 = delete button — no label needed.
        return headerGrid;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }

    private FrameworkElement BuildRow(AimDisengageRule rule, List<string> regionNames)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("FluentSurface3"),
            BorderBrush = (Brush)FindResource("FluentStroke"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });             // enabled
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // region
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });             // comparison
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });          // value
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });         // match process
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });             // remove

        var enabled = new CheckBox
        {
            IsChecked = rule.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = Locale.EnabledActive
        };
        enabled.Click += (_, _) => rule.Enabled = enabled.IsChecked == true;
        Grid.SetColumn(enabled, 0);
        grid.Children.Add(enabled);

        var regionCombo = new ComboBox
        {
            ItemsSource = regionNames,
            SelectedItem = regionNames.Contains(rule.RegionName) ? rule.RegionName : null,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        regionCombo.SelectionChanged += (_, _) => rule.RegionName = regionCombo.SelectedItem as string ?? "";
        Grid.SetColumn(regionCombo, 1);
        grid.Children.Add(regionCombo);

        var compCombo = new ComboBox
        {
            ItemsSource = System.Enum.GetValues<OcrComparison>(),
            SelectedItem = rule.Comparison,
            Margin = new Thickness(0, 0, 6, 0),
            MinWidth = 70,
            VerticalAlignment = VerticalAlignment.Center
        };
        compCombo.SelectionChanged += (_, _) =>
        {
            if (compCombo.SelectedItem is OcrComparison c) rule.Comparison = c;
        };
        Grid.SetColumn(compCombo, 2);
        grid.Children.Add(compCombo);

        var valueBox = new TextBox
        {
            Text = rule.Value,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Tag = Locale.AimDisengageValuePlaceholder,
            ToolTip = Locale.AimDisengageValueHint,
        };
        // 'placeHolder' is a TextBox style defined in App.xaml that draws Tag as a grey
        // placeholder while Text is empty — matches the GlobalSearchBox treatment.
        if (TryFindResource("placeHolder") is Style ph) valueBox.Style = ph;
        valueBox.TextChanged += (_, _) => rule.Value = valueBox.Text;
        Grid.SetColumn(valueBox, 3);
        grid.Children.Add(valueBox);

        var matchBox = new TextBox
        {
            Text = rule.MatchProcess,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Tag = Locale.AimDisengageMatchProcessHint,
            ToolTip = Locale.AimDisengageMatchProcessHint,
        };
        if (TryFindResource("placeHolder") is Style phm) matchBox.Style = phm;
        matchBox.TextChanged += (_, _) => rule.MatchProcess = matchBox.Text;
        Grid.SetColumn(matchBox, 4);
        grid.Children.Add(matchBox);

        var removeBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 12
            },
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Locale.Delete
        };
        removeBtn.SetResourceReference(StyleProperty, "FluentStandardButton");
        removeBtn.Click += (_, _) =>
        {
            AppConfig.Current.AimDisengageRules.Remove(rule);
            BuildRows();
        };
        Grid.SetColumn(removeBtn, 5);
        grid.Children.Add(removeBtn);

        card.Child = grid;
        return card;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var regionNames = RegionNames();
        AppConfig.Current.AimDisengageRules.Add(new AimDisengageRule
        {
            RegionName = regionNames.FirstOrDefault() ?? "",
            Comparison = OcrComparison.Contains
        });
        BuildRows();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
