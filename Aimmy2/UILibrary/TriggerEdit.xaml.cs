using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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

            var drop = ChargeEnterIntersectionBox.AddDropdown(Locale.TriggerCheckChargeIn, Trigger.BeginIntersectionCheck,
                check => Trigger.BeginIntersectionCheck = check);
            drop.ToolTip = Locale.TriggerCheckChargeInToolTip;
            drop.Margin = new Thickness(-11, 0, -11, -10);
            drop.BorderBrush = Brushes.Transparent;
            drop.Background = Brushes.Transparent;
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
                b.ToolTip = Locale.ConfigureHeadAreaTooltip;
            }).Reader.Click += (s, e) =>
            {
                new EditHeadArea(Trigger.BeginIntersectionArea, model => Trigger.BeginIntersectionArea = model.ToRelativeRect()).Show();
            };



            var d = IntersectionBox.AddDropdown(Locale.TriggerCheck, Trigger.ExecutionIntersectionCheck,
                check => Trigger.ExecutionIntersectionCheck = check);
            d.Margin = new Thickness(-11, 0, -11 ,-10);
            d.BorderBrush = Brushes.Transparent;
            d.Background = Brushes.Transparent;
            IntersectionBox.AddButton(Locale.ConfigureHeadArea, b =>
            {
                Trigger.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(Trigger.ExecutionIntersectionCheck))
                    {
                        b.Visibility = Trigger.ExecutionIntersectionCheck == TriggerCheck.HeadIntersectingCenter ? Visibility.Visible : Visibility.Collapsed;
                    }
                };
                b.Visibility = Trigger.ExecutionIntersectionCheck == TriggerCheck.HeadIntersectingCenter ? Visibility.Visible : Visibility.Collapsed;
                b.ToolTip = Locale.ConfigureHeadAreaTooltip;
            }).Reader.Click += (s, e) =>
            {
                new EditHeadArea(Trigger.ExecutionIntersectionArea, model => Trigger.ExecutionIntersectionArea = model.ToRelativeRect()).Show();
            };
            
            TimeSettings.AddSlider(Locale.MinTimeTriggerKey, Locale.Seconds, 0.01, 0.1, 0.0, 5).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = Locale.MinTimeTriggerKeyTooltip;
            }).BindTo(() => Trigger.TriggerKeyMin);
            TimeSettings.AddSlider(Locale.AutoTriggerDelay, Locale.Seconds, 0.01, 0.1, 0.00, 5).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = Locale.AutoTriggerDelayTooltip;
            }).BindTo(() => Trigger.Delay);
            TimeSettings.AddSlider(Locale.AutoTriggerBreakTime, Locale.Seconds, 0.01, 0.1, 0.0, 5).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = Locale.AutoTriggerBreakTimeTooltip;
            }).BindTo(() => Trigger.BreakTime);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            ActionKeyChanger.Text = Locale.LabelTriggerAction;
        }

        public TriggerEdit()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void ActionKeyChanger_OnKeyBindChanged(object? sender, EventArgs<(AKeyChanger Sender, StoredInputBinding KeyBinding)> e)
        {
            ChargeModeDescription.Text = Locale.ChargeModeToolTip.FormatWith(Trigger.Action.Key);
        }
    }
}
