using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.Types;
using PowerAim.UILibrary;
using InputLogic;
using Nextended.Core.Extensions;
using Visuality;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for TriggerEdit.xaml
    /// </summary>
    public partial class TriggerEdit : UserControl
    {
        public InputBindingManager BindingManager => MainWindow.Instance.BindingManager;

        public ActionTrigger Trigger
        {
            get => (ActionTrigger)GetValue(TriggerProperty);
            set => SetValue(TriggerProperty, value);
        }

        public static readonly DependencyProperty TriggerProperty =
            DependencyProperty.Register(nameof(Trigger), typeof(ActionTrigger), typeof(TriggerEdit), new PropertyMetadata(null, TriggerChanged));

        private static void TriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TriggerEdit)d).TriggerChanged();
        }

        private void TriggerChanged()
        {
            UpdateDynamicUi();
        }

        private void UpdateDynamicUi()
        {
            IntersectionBox.RemoveAll();
            ChargeEnterIntersectionBox.RemoveAll();
            TimeSettings.RemoveAll();
            ModePanel.RemoveAll();
            TriggerKeyOperator.RemoveAll();
            AntiTriggerKeyOperator.RemoveAll();

            if (Trigger == null) return;

            // Charge entry intersection: dropdown (col 0) + Configure-Area button (col 1)
            var beginDropdown = ChargeEnterIntersectionBox.AddDropdown<TriggerCheck>("", Trigger.BeginIntersectionCheck,
                check => Trigger.BeginIntersectionCheck = check).AsSimple();
            beginDropdown.HorizontalAlignment = HorizontalAlignment.Stretch;
            beginDropdown.VerticalAlignment = VerticalAlignment.Center;
            beginDropdown.Margin = new Thickness(0);
            Grid.SetColumn(beginDropdown, 0);
            AddConfigureAreaButton(ChargeEnterIntersectionBox,
                () => Trigger.ChargeMode && Trigger.BeginIntersectionCheck == TriggerCheck.HeadIntersectingCenter,
                nameof(Trigger.BeginIntersectionCheck),
                () => new EditHeadArea(Trigger.BeginIntersectionArea, model => Trigger.BeginIntersectionArea = model.ToRelativeRect()).Show());

            // Execution intersection: dropdown (col 0) + Configure-Area button (col 1)
            var execDropdown = IntersectionBox.AddDropdown<TriggerCheck>("", Trigger.ExecutionIntersectionCheck,
                check => Trigger.ExecutionIntersectionCheck = check).AsSimple();
            execDropdown.HorizontalAlignment = HorizontalAlignment.Stretch;
            execDropdown.VerticalAlignment = VerticalAlignment.Center;
            execDropdown.Margin = new Thickness(0);
            Grid.SetColumn(execDropdown, 0);
            AddConfigureAreaButton(IntersectionBox,
                () => Trigger.ExecutionIntersectionCheck == TriggerCheck.HeadIntersectingCenter,
                nameof(Trigger.ExecutionIntersectionCheck),
                () => new EditHeadArea(Trigger.ExecutionIntersectionArea, model => Trigger.ExecutionIntersectionArea = model.ToRelativeRect()).Show());

            TimeSettings.AddSlider(Locale.AutoTriggerDelay, Locale.Seconds, 0.01, 0.1, 0.00, 5).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = Locale.AutoTriggerDelayTooltip;
            }).BindTo(() => Trigger.Delay);
            TimeSettings.AddSlider(Locale.AutoTriggerBreakTime, Locale.Seconds, 0.01, 0.1, 0.0, 5).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                if (AppConfig.Current?.ToggleState?.ShowHelpTexts == true)
                    slider.ToolTip = Locale.AutoTriggerBreakTimeTooltip;
            }).BindTo(() => Trigger.BreakTime);

            ModePanel.AddDropdown("", Trigger.ExecutionMode,
                mode =>
                {
                    Trigger.ExecutionMode = mode;
                    TriggerActionsHelp.Text = mode switch
                    {
                        TriggerExecutionMode.Sequential => Locale.DescriptionTriggerActionsSequential,
                        TriggerExecutionMode.Simultaneous => Locale.DescriptionTriggerSimultaneous,
                        _ => ""
                    };
                }).AsSimple();

            TriggerKeyOperator.AddDropdown("", Trigger.TriggerKeysOperator,
                mode =>
                {
                    Trigger.TriggerKeysOperator = mode;
                    DescriptionTriggerKeys.Text = mode switch
                    {
                        KeyOperator.And => Locale.DescriptionTriggerKeys,
                        KeyOperator.Or => Locale.DescriptionTriggerKeysOr,
                        _ => ""
                    };
                }).AsSimple().SetProperties(m => m.MinWidth = 80);

            AntiTriggerKeyOperator.AddDropdown("", Trigger.AntiTriggerKeysOperator,
                mode =>
                {
                    Trigger.AntiTriggerKeysOperator = mode;
                    DescriptionAntiTriggerKeys.Text = mode switch
                    {
                        KeyOperator.And => Locale.DescriptionAntiTriggerKeys,
                        KeyOperator.Or => Locale.DescriptionAntiTriggerKeysOr,
                        _ => ""
                    };
                }).AsSimple().SetProperties(m => m.MinWidth = 80);

            // The new tree-based builders own their own lifecycle. Just point them at the trigger's
            // trees; everything else (rebuild on edit, leaf/group add, region picker repopulate)
            // lives inside OcrConditionBuilder.
            OcrConditionsBuilder.Group = Trigger.OcrConditionTree;
            AntiOcrConditionsBuilder.Group = Trigger.AntiOcrConditionTree;
        }

        // BuildOcrConditionsUi / BuildOcrConditionRow were the flat-list editors that lived
        // before the tree refactor. They're superseded by the in-place OcrConditionBuilder
        // controls in XAML — nothing should call these any more.
#pragma warning disable CS8321 // Local function is declared but never used
        private FrameworkElement BuildOcrConditionRow_Obsolete(OcrTriggerCondition cond, List<string> regionNames)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var regionCombo = new ComboBox
            {
                ItemsSource = regionNames,
                SelectedItem = regionNames.Contains(cond.RegionName) ? cond.RegionName : null,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            regionCombo.SelectionChanged += (_, _) => cond.RegionName = regionCombo.SelectedItem as string ?? "";
            Grid.SetColumn(regionCombo, 0);
            grid.Children.Add(regionCombo);

            var compCombo = new ComboBox
            {
                ItemsSource = Enum.GetValues<OcrComparison>(),
                SelectedItem = cond.Comparison,
                Margin = new Thickness(0, 0, 6, 0),
                MinWidth = 70,
                VerticalAlignment = VerticalAlignment.Center
            };
            compCombo.SelectionChanged += (_, _) =>
            {
                if (compCombo.SelectedItem is OcrComparison c) cond.Comparison = c;
            };
            Grid.SetColumn(compCombo, 1);
            grid.Children.Add(compCombo);

            var valueBox = new TextBox
            {
                Text = cond.Value,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            valueBox.TextChanged += (_, _) => cond.Value = valueBox.Text;
            Grid.SetColumn(valueBox, 2);
            grid.Children.Add(valueBox);

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
            removeBtn.SetResourceReference(FrameworkElement.StyleProperty, "FluentStandardButton");
            removeBtn.Click += (_, _) => Trigger.OcrConditions.Remove(cond); // obsolete: superseded by OcrConditionBuilder
            Grid.SetColumn(removeBtn, 3);
            grid.Children.Add(removeBtn);

            return grid;
        }

        public TriggerEdit()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void AddConfigureAreaButton(Panel container, Func<bool> visiblePredicate, string trackedPropertyName, Action onClick)
        {
            var iconText = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            var labelText = new TextBlock
            {
                Text = Locale.ConfigureHeadArea,
                Margin = new Thickness(8, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13
            };
            var contentStack = new StackPanel { Orientation = Orientation.Horizontal };
            contentStack.Children.Add(iconText);
            contentStack.Children.Add(labelText);

            var btn = new System.Windows.Controls.Button
            {
                Content = contentStack,
                Margin = new Thickness(10, -10, 0, 0),
                Padding = new Thickness(12, 4, 12, 4),
                Height = 34,
                VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                ToolTip = Locale.ConfigureHeadAreaTooltip,
                Visibility = visiblePredicate() ? Visibility.Visible : Visibility.Collapsed
            };
            btn.SetResourceReference(FrameworkElement.StyleProperty, "FluentStandardButton");
            btn.Click += (_, _) => onClick();
            Trigger.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == trackedPropertyName || args.PropertyName == nameof(Trigger.ChargeMode))
                    btn.Visibility = visiblePredicate() ? Visibility.Visible : Visibility.Collapsed;
            };
            Grid.SetColumn(btn, 1);
            container.Children.Add(btn);
        }

        private void MultiKeyChanger_OnChanged(object? sender, EventArgs<ObservableCollection<StoredInputBinding>> e)
        {
            if (Trigger == null) return;
            ChargeModeDescription.Text = Locale.ChargeModeToolTip.FormatWith(string.Join(", ", Trigger.Actions.Where(t => t is {IsValid: true}).Select(t => t.Key)));
        }
    }
}
