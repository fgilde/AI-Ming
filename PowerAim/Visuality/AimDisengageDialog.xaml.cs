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
        foreach (var rule in AppConfig.Current.AimDisengageRules.ToList())
            RulesBox.Children.Add(BuildRow(rule, regionNames));
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
            ToolTip = Locale.AimDisengageValueHint
        };
        valueBox.TextChanged += (_, _) => rule.Value = valueBox.Text;
        Grid.SetColumn(valueBox, 3);
        grid.Children.Add(valueBox);

        var matchBox = new TextBox
        {
            Text = rule.MatchProcess,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "cs2*, valorant, *fortnite*"
        };
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
