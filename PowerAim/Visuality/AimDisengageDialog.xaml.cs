using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.UILibrary;

namespace PowerAim.Visuality;

/// <summary>
///     Editor for <see cref="AppConfig.AimDisengageRules"/>: a list of OCR-driven rules that pause
///     aim assist while a HUD value matches (e.g. while scoped). Each rule now carries a full
///     <see cref="OcrConditionGroup"/> so the condition itself supports AND / OR nesting; the dialog
///     hosts one <see cref="OcrConditionBuilder"/> per rule, plus per-rule Enabled + MatchProcess.
///     Edits the live collection — closing the dialog keeps whatever rows are present.
/// </summary>
public partial class AimDisengageDialog
{
    public AimDisengageDialog()
    {
        InitializeComponent();
        DataContext = this;
        BuildRows();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }

    private void BuildRows()
    {
        RulesBox.Children.Clear();
        var regions = AppConfig.Current.OcrSettings.Regions
            .Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        if (regions.Count == 0)
        {
            // No OCR regions configured → no rules can fire. Show a hint and disable the add
            // button so the user goes to OCR Regions first.
            RulesBox.Children.Add(new TextBlock
            {
                Text = Locale.OcrConditionsNoRegions,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 0, 4),
                Foreground = (Brush)FindResource("FluentTextTertiary"),
                FontFamily = new FontFamily("Segoe UI Variable Small"),
                FontSize = 12,
            });
            AddBtn.IsEnabled = false;
            return;
        }

        AddBtn.IsEnabled = true;
        foreach (var rule in AppConfig.Current.AimDisengageRules.ToList())
            RulesBox.Children.Add(BuildRuleCard(rule));
    }

    /// <summary>
    ///     One card per rule: title header (Enabled toggle + optional Name + MatchProcess + Delete)
    ///     and below it the recursive <see cref="OcrConditionBuilder"/> editing
    ///     <see cref="AimDisengageRule.ConditionTree"/>. Replaces the old flat (region · comparison
    ///     · value · game) single-row layout.
    /// </summary>
    private FrameworkElement BuildRuleCard(AimDisengageRule rule)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("FluentSurface3"),
            BorderBrush = (Brush)FindResource("FluentStroke"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 12),
            Margin = new Thickness(0, 0, 0, 10),
        };

        var stack = new StackPanel();

        // ---- Header row: Enabled · Name · MatchProcess · Delete ----
        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });            // enabled
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });        // process pattern
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });            // delete

        var enabled = new CheckBox
        {
            IsChecked = rule.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            ToolTip = Locale.EnabledActive,
        };
        enabled.Click += (_, _) => rule.Enabled = enabled.IsChecked == true;
        Grid.SetColumn(enabled, 0);
        header.Children.Add(enabled);

        var nameBox = new TextBox
        {
            Text = rule.Name,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Tag = Locale.AimDisengageRuleNamePlaceholder,
            ToolTip = Locale.AimDisengageRuleNameHint,
        };
        // 'placeHolder' is the shared TextBox style that renders Tag as faded placeholder text
        // until the user types something. Same treatment as the global search box.
        if (TryFindResource("placeHolder") is Style phn) nameBox.Style = phn;
        nameBox.TextChanged += (_, _) => rule.Name = nameBox.Text;
        Grid.SetColumn(nameBox, 1);
        header.Children.Add(nameBox);

        var matchBox = new TextBox
        {
            Text = rule.MatchProcess,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Tag = Locale.AimDisengageMatchProcessHint,
            ToolTip = Locale.AimDisengageMatchProcessHint,
        };
        if (TryFindResource("placeHolder") is Style phm) matchBox.Style = phm;
        matchBox.TextChanged += (_, _) => rule.MatchProcess = matchBox.Text;
        Grid.SetColumn(matchBox, 2);
        header.Children.Add(matchBox);

        var removeBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 12,
            },
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Locale.Delete,
        };
        removeBtn.SetResourceReference(StyleProperty, "FluentStandardButton");
        removeBtn.Click += (_, _) =>
        {
            AppConfig.Current.AimDisengageRules.Remove(rule);
            BuildRows();
        };
        Grid.SetColumn(removeBtn, 3);
        header.Children.Add(removeBtn);

        stack.Children.Add(header);

        // ---- Body: tree builder editing rule.ConditionTree ----
        var builder = new OcrConditionBuilder { Group = rule.ConditionTree };
        stack.Children.Add(builder);

        card.Child = stack;
        return card;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        AppConfig.Current.AimDisengageRules.Add(new AimDisengageRule());
        BuildRows();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
