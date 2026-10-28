using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PowerAim.AI.Rules;

namespace PowerAim.UI.UILibrary
{
    /// <summary>
    ///     Edits a rule the way a user thinks about it: pick a preset or describe it in words ("fire when the
    ///     head would very likely be hit"), let the local model write the rule, and see right away whether it
    ///     is valid. The rule text stays editable; the signal list explains what it can use.
    /// </summary>
    public class RuleEditor : UserControl
    {
        public static readonly DependencyProperty RuleProperty = DependencyProperty.Register(nameof(Rule), typeof(string), typeof(RuleEditor),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((RuleEditor)d).Validate()));

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(RuleEditor),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Rule { get => (string)GetValue(RuleProperty); set => SetValue(RuleProperty, value); }
        public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

        public RuleKind Kind { get; }

        private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 0, 0) };
        private readonly Button _generate = new() { Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };

        public RuleEditor(RuleKind kind)
        {
            Kind = kind;
            var root = new StackPanel();

            var presets = new ComboBox
            {
                ItemsSource = kind == RuleKind.TriggerCondition ? RulePresets.Trigger : RulePresets.Aim,
                DisplayMemberPath = nameof(RulePreset.Description),
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = Locale.RulePresetTooltip,
            };
            presets.SelectionChanged += (_, _) =>
            {
                if (presets.SelectedItem is not RulePreset p) return;
                Description = p.Description;
                Rule = p.Expression;
            };
            root.Children.Add(Caption(Locale.RulePresets));
            root.Children.Add(presets);

            root.Children.Add(Caption(Locale.RuleDescription));
            root.Children.Add(Box(DescriptionProperty, multiline: true, Locale.RuleDescriptionTooltip));

            _generate.Content = Locale.RuleGenerate;
            _generate.ToolTip = Locale.RuleGenerateTooltip;
            _generate.SetResourceReference(StyleProperty, "FluentStandardButton");
            _generate.Click += async (_, _) =>
            {
                _generate.IsEnabled = false;
                _status.Text = Locale.RuleGenerating;
                try
                {
                    var (rule, error) = await RuleTranslator.TranslateAsync(Description, Kind);
                    if (rule != null) Rule = rule;
                    else ShowError(error ?? "?");
                }
                finally { _generate.IsEnabled = true; }
            };
            root.Children.Add(_generate);

            root.Children.Add(Caption(Kind == RuleKind.TriggerCondition ? Locale.RuleConditionLabel : Locale.RuleScoreLabel));
            var ruleBox = Box(RuleProperty, multiline: false, Locale.RuleTextTooltip);
            ruleBox.FontFamily = new FontFamily("Cascadia Mono,Consolas");
            root.Children.Add(ruleBox);
            root.Children.Add(_status);

            var signals = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 6, 0, 0),
                Text = Locale.RuleSignalsTitle + "\n" + string.Join("\n", TargetScope.Signals.Select(kv => $"  {kv.Key} — {kv.Value}")),
            };
            signals.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
            root.Children.Add(new Expander { Header = Locale.RuleSignalsTitle, Content = signals, Margin = new Thickness(0, 4, 0, 0) });

            Content = root;
            Validate();
        }

        private TextBlock Caption(string text)
        {
            var t = new TextBlock { Text = text, FontSize = 12, Margin = new Thickness(2, 6, 0, 2) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
            return t;
        }

        private TextBox Box(DependencyProperty property, bool multiline, string tooltip)
        {
            var box = new TextBox
            {
                Padding = new Thickness(3, 5, 1, 5),
                BorderThickness = new Thickness(1),
                AcceptsReturn = multiline,
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                MinHeight = multiline ? 48 : 0,
                ToolTip = tooltip,
                Background = Brushes.Transparent,
            };
            box.SetResourceReference(BorderBrushProperty, "FluentStroke");
            box.SetResourceReference(ForegroundProperty, "FluentTextPrimary");
            box.SetBinding(TextBox.TextProperty, new Binding(property.Name)
            {
                Source = this, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });
            return box;
        }

        private void Validate()
        {
            var (rule, error) = TriggerRules.Compile(Rule);
            if (rule == null) ShowError(error ?? "?");
            else
            {
                _status.Text = "✓ " + Locale.RuleValid;
                _status.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");
            }
        }

        private void ShowError(string error)
        {
            _status.Text = "✗ " + error;
            _status.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
        }
    }
}
