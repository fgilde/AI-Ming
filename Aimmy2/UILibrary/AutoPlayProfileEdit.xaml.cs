using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aimmy2.Config;
using Aimmy2.Extensions;

namespace UILibrary
{
    public partial class AutoPlayProfileEdit : UserControl
    {
        public AutoPlayProfile Profile
        {
            get => (AutoPlayProfile)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        public static readonly DependencyProperty ProfileProperty =
            DependencyProperty.Register(nameof(Profile), typeof(AutoPlayProfile), typeof(AutoPlayProfileEdit),
                new PropertyMetadata(null, ProfileChanged));

        private static void ProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AutoPlayProfileEdit)d).ProfileChanged();
        }

        private void ProfileChanged()
        {
            UpdateDynamicUi();
            // Ensure the ActionsList binding is updated when Profile changes
            if (ActionsList != null && Profile != null)
            {
                ActionsList.Actions = Profile.Actions;
            }
        }

        private void UpdateDynamicUi()
        {
            DecisionIntervalPanel.RemoveAll();
            DecisionIntervalPanel.AddSlider("Decision Interval", "seconds", 0.1, 0.5, 0.3, 10).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = "Time between AI decisions. Lower = more responsive but higher load.";
            }).BindTo(() => Profile.DecisionInterval);
        }

        public AutoPlayProfileEdit()
        {
            InitializeComponent();
            DataContext = this;
        }
    }
}
