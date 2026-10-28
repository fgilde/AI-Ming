using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using PowerAim.AI.Rules;
using PowerAim.UI.UILibrary;

namespace PowerAim.App.Tests;

public class RuleEditorTests
{
    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var d in Tree(child)) yield return d;
    }

    private static string Status(RuleEditor editor)
        => Tree(editor).OfType<TextBlock>().Select(t => t.Text).First(t => t.StartsWith("✓") || t.StartsWith("✗"));

    [Fact]
    public void Shows_whether_the_rule_is_valid_and_presets_fill_both_fields() => Sta.Run(() =>
    {
        var editor = new RuleEditor(RuleKind.TriggerCondition) { Rule = "head_hit >= 0.8" };
        Assert.StartsWith("✓", Status(editor));

        editor.Rule = "head_hitt >= 0.8";
        Assert.Contains("unknown signal", Status(editor));

        var presets = Tree(editor).OfType<ComboBox>().Single();
        presets.SelectedIndex = 0;
        Assert.Equal(RulePresets.Trigger[0].Expression, editor.Rule);
        Assert.Equal(RulePresets.Trigger[0].Description, editor.Description);
        Assert.StartsWith("✓", Status(editor));
    });

    [Fact]
    public void Aim_editor_offers_score_presets() => Sta.Run(() =>
    {
        var editor = new RuleEditor(RuleKind.AimScore);
        var presets = Tree(editor).OfType<ComboBox>().Single();
        Assert.Equal(RulePresets.Aim.Count, presets.Items.Count);
    });
}
