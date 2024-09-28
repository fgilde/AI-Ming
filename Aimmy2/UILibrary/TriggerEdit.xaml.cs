using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aimmy2;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.InputLogic;
using Aimmy2.Types;
using Aimmy2.UILibrary;
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
            ChargeEnterIntersectionBox.AddDropdown(Locale.TriggerCheckChargeIn, Trigger.BeginIntersectionCheck,
                check => Trigger.BeginIntersectionCheck = check).AsSimple(true);
            ChargeEnterIntersectionBox.AddButton(Locale.ConfigureHeadArea, b =>
            {
                Trigger.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(Trigger.BeginIntersectionCheck))
                    {
                        b.Visibility = Trigger.ChargeMode && Trigger.BeginIntersectionCheck == TriggerCheck.HeadIntersectingCenter ? Visibility.Visible : Visibility.Collapsed;
                    }
                };
                b.Visibility = Trigger.ChargeMode && Trigger.BeginIntersectionCheck == TriggerCheck.HeadIntersectingCenter ? Visibility.Visible : Visibility.Collapsed;
                if(AppConfig.Current?.ToggleState?.ShowHelpTexts == true)
                    b.ToolTip = Locale.ConfigureHeadAreaTooltip;
            }).Reader.Click += (s, e) =>
            {
                new EditHeadArea(Trigger.BeginIntersectionArea, model => Trigger.BeginIntersectionArea = model.ToRelativeRect()).Show();
            };

            IntersectionBox.AddDropdown(Locale.TriggerCheck, Trigger.ExecutionIntersectionCheck,
                check => Trigger.ExecutionIntersectionCheck = check).AsSimple(true);
            IntersectionBox.AddButton(Locale.ConfigureHeadArea, b =>
            {
                Trigger.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(Trigger.ExecutionIntersectionCheck))
                    {
                        b.Visibility = Trigger.ExecutionIntersectionCheck == TriggerCheck.HeadIntersectingCenter ? Visibility.Visible : Visibility.Collapsed;
                    }
                };
                b.Margin = new Thickness(0, -10, 0, 0);
                b.Visibility = Trigger.ExecutionIntersectionCheck == TriggerCheck.HeadIntersectingCenter ? Visibility.Visible : Visibility.Collapsed;
                if (AppConfig.Current?.ToggleState?.ShowHelpTexts == true)
                    b.ToolTip = Locale.ConfigureHeadAreaTooltip;
            }).Reader.Click += (s, e) =>
            {
                new EditHeadArea(Trigger.ExecutionIntersectionArea, model => Trigger.ExecutionIntersectionArea = model.ToRelativeRect()).Show();
            };
            
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

        }
        
        public TriggerEdit()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void MultiKeyChanger_OnChanged(object? sender, EventArgs<ObservableCollection<StoredInputBinding>> e)
        {
            ChargeModeDescription.Text = Locale.ChargeModeToolTip.FormatWith(string.Join(", ", Trigger.Actions.Where(t => t is {IsValid: true}).Select(t => t.Key)));
        }
    }
}
