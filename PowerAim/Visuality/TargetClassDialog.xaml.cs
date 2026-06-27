using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.Config;

namespace PowerAim.Visuality;

/// <summary>
///     Modal picker that lets the user choose which class IDs from the currently loaded model the
///     aim/trigger pipeline should accept. Writes back to <see cref="AISettings.TargetClassIds"/>
///     and <see cref="AISettings.TargetClassFilterMode"/> on Save.
/// </summary>
public partial class TargetClassDialog
{
    private readonly Dictionary<int, CheckBox> _classBoxes = [];
    private readonly IReadOnlyDictionary<int, string> _classes;

    public TargetClassDialog(IReadOnlyDictionary<int, string> classes)
    {
        InitializeComponent();
        _classes = classes ?? new Dictionary<int, string>();

        TitleText.Text = Locale.GetAll().TryGetValue("TargetClasses", out var t) ? t : "Target Classes";
        ModeLabel.Text = Locale.GetAll().TryGetValue("TargetClassFilterMode", out var ml) ? ml : "Filter mode";
        RbAll.Content = Locale.GetAll().TryGetValue("AllClasses", out var ac) ? ac : "All Classes";
        RbSpecific.Content = Locale.GetAll().TryGetValue("SpecificClasses", out var sc) ? sc : "Only selected";
        CancelButton.Content = Locale.Cancel;
        SaveButton.Content = Locale.Save;
        HelpText.Text = Locale.GetAll().TryGetValue("TargetClassesHelp", out var help)
            ? help
            : "Choose which classes from the loaded model are considered targets.";

        BuildClassList();
        LoadCurrentSelection();
        UpdateBoxesEnabled();
    }

    private void BuildClassList()
    {
        ClassListPanel.Children.Clear();
        _classBoxes.Clear();

        if (_classes.Count == 0)
        {
            ClassListPanel.Children.Add(new TextBlock
            {
                Text = Locale.GetAll().TryGetValue("NoModelLoaded", out var nm) ? nm : "No model loaded.",
                Foreground = TryFindResource("FluentTextSecondary") as Brush ?? Brushes.Gray,
                FontFamily = new("Segoe UI Variable Text"),
                FontSize = 13,
                Margin = new(10, 16, 10, 16),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return;
        }

        foreach (var kv in _classes.OrderBy(k => k.Key))
        {
            var cb = new CheckBox
            {
                Margin = new(8, 4, 8, 4),
                Foreground = TryFindResource("FluentTextPrimary") as Brush ?? Brushes.White,
                FontFamily = new("Segoe UI Variable Text"),
                FontSize = 13,
                Content = $"  {kv.Key}  ·  {kv.Value}"
            };
            _classBoxes[kv.Key] = cb;
            ClassListPanel.Children.Add(cb);
        }
    }

    private void LoadCurrentSelection()
    {
        var settings = AppConfig.Current?.AISettings;
        if (settings is null) return;

        if (settings.TargetClassFilterMode == TargetClassFilterMode.SpecificIds)
        {
            RbSpecific.IsChecked = true;
            foreach (var id in settings.TargetClassIds)
                if (_classBoxes.TryGetValue(id, out var cb)) cb.IsChecked = true;
        }
        else
        {
            RbAll.IsChecked = true;
            foreach (var cb in _classBoxes.Values) cb.IsChecked = false;
        }
    }

    private void UpdateBoxesEnabled()
    {
        // RbSpecific is null while XAML is still being parsed (the IsChecked="True" on RbAll fires
        // Checked before named-element assignment completes). Guard against that.
        if (RbSpecific is null) return;
        bool specific = RbSpecific.IsChecked == true;
        foreach (var cb in _classBoxes.Values) cb.IsEnabled = specific;
    }

    private void Rb_Checked(object sender, RoutedEventArgs e) => UpdateBoxesEnabled();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppConfig.Current?.AISettings;
        if (settings is null) { Close(); return; }

        if (RbSpecific.IsChecked == true)
        {
            settings.TargetClassFilterMode = TargetClassFilterMode.SpecificIds;
            var selected = _classBoxes.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            settings.TargetClassIds.Clear();
            foreach (var id in selected) settings.TargetClassIds.Add(id);
        }
        else
        {
            settings.TargetClassFilterMode = TargetClassFilterMode.AllClasses;
            settings.TargetClassIds.Clear();
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
