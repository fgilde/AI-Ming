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
using InputLogic;
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
            TimeSettings.RemoveAll();
            var d = IntersectionBox.AddDropdown(Locale.TriggerCheck, Trigger.IntersectionCheck,
                check => Trigger.IntersectionCheck = check);
            d.Margin = new Thickness(-11, 0, -11 ,-10);
            d.BorderBrush = Brushes.Transparent;
            d.Background = Brushes.Transparent;

            IntersectionBox.AddButton(Locale.ConfigureHeadArea, b =>
            {
                Trigger.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(Trigger.IntersectionCheck))
                    {
                        b.Visibility = Trigger.IntersectionCheck == TriggerCheck.HeadIntersectingCenter ? Visibility.Visible : Visibility.Collapsed;
                    }
                };
                b.Visibility = Trigger.IntersectionCheck == TriggerCheck.HeadIntersectingCenter ? Visibility.Visible : Visibility.Collapsed;
                b.ToolTip = Locale.ConfigureHeadAreaTooltip;
            }).Reader.Click += (s, e) =>
            {
                new EditHeadArea(Trigger.IntersectionArea, model => Trigger.IntersectionArea = model.ToRelativeRect()).Show();
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
    }
}
