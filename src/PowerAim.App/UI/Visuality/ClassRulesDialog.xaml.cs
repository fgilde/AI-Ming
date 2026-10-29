using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Other;

namespace PowerAim.UI.Visuality;

/// <summary>
///     Editor for an <see cref="AimProfile"/>'s <see cref="AimProfile.ClassSpecs"/>. Opening it adds a
///     neutral row for every class of the loaded model that has none yet, so the user only edits values.
/// </summary>
public partial class ClassRulesDialog
{
    private static readonly Dictionary<string, Func<string>> Headers = new()
    {
        [nameof(ClassTargetSpec.ClassId)] = () => Locale.AimClassId,
        [nameof(ClassTargetSpec.Name)] = () => Locale.AimClassName,
        [nameof(ClassTargetSpec.Priority)] = () => Locale.AimClassPriority,
        [nameof(ClassTargetSpec.Ignored)] = () => Locale.AimClassIgnored,
        [nameof(ClassTargetSpec.MinConfidence)] = () => Locale.AimClassMinConfidence,
        [nameof(ClassTargetSpec.UseAnchor)] = () => Locale.AimClassUseAnchor,
        [nameof(ClassTargetSpec.AnchorX)] = () => Locale.AimClassAnchorX,
        [nameof(ClassTargetSpec.AnchorY)] = () => Locale.AimClassAnchorY,
        [nameof(ClassTargetSpec.FlatOffsetX)] = () => Locale.AimClassOffsetX,
        [nameof(ClassTargetSpec.FlatOffsetY)] = () => Locale.AimClassOffsetY,
    };

    public ClassRulesDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public static void ShowFor(AimProfile profile, Window? owner)
    {
        var classes = FileManager.AIManager?.PredictionLogic?.ModelClasses;
        if (classes != null)
            foreach (var (id, name) in classes.OrderBy(c => c.Key))
                if (profile.ClassSpecs.All(s => s.ClassId != id))
                    profile.ClassSpecs.Add(new ClassTargetSpec { ClassId = id, Name = name });

        var dialog = new ClassRulesDialog { Owner = owner };
        dialog.Grid.ItemsSource = profile.ClassSpecs;
        dialog.ShowDialog();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.HideForCaptureIfEnabled();
    }

    private void Grid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (Headers.TryGetValue(e.PropertyName, out var header)) e.Column.Header = header();
        if (e.PropertyName is nameof(ClassTargetSpec.ClassId) or nameof(ClassTargetSpec.Name)) e.Column.IsReadOnly = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
