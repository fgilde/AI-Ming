using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PowerAim.AI;
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
        var logic = FileManager.AIManager?.PredictionLogic;
        var sidecar = ModelSidecar.Load((logic as PredictionLogic)?.ModelPath);
        AddMissingRows(profile, logic?.ModelClasses, sidecar);

        var dialog = new ClassRulesDialog { Owner = owner };
        dialog.Grid.ItemsSource = profile.ClassSpecs;
        if (!string.IsNullOrWhiteSpace(sidecar?.Notes))
            dialog.DescriptionText.Text += Environment.NewLine + Environment.NewLine + Locale.AimClassModelNotes + " " + sidecar!.Notes;
        dialog.ShowDialog();
    }

    /// <summary>
    ///     One row per model class the profile has no rule for yet: the model's recommended rule from its
    ///     sidecar when there is one, otherwise a neutral row.
    /// </summary>
    internal static void AddMissingRows(AimProfile profile, IReadOnlyDictionary<int, string>? classes, ModelSidecar? sidecar)
    {
        if (classes == null) return;
        foreach (var (id, name) in classes.OrderBy(c => c.Key))
        {
            if (profile.ClassSpecs.Any(s => s.ClassId == id)) continue;
            var recommended = sidecar?.ClassSpecs?.FirstOrDefault(s => s.ClassId == id);
            var row = recommended == null ? new ClassTargetSpec { ClassId = id } : recommended.Copy();
            row.Name = name;
            profile.ClassSpecs.Add(row);
        }
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
