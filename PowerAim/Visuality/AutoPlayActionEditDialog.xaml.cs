using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim;
using PowerAim.Visuality;

namespace Visuality
{
    public partial class AutoPlayActionEditDialog : BaseDialog
    {
        public AutoPlayAction Action
        {
            get;
            set
            {
                field = value;
                field?.BeginEdit();
                OnPropertyChanged();
                UpdateDynamicUi();
            }
        }

        public AutoPlayActionEditDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UpdateDynamicUi()
        {
            if (Action is null) return;

            DurationPanel.RemoveAll();

            // Action Type dropdown
            DurationPanel.AddDropdown(Locale.ActionType, Action.ActionType, type =>
            {
                Action.ActionType = type;
            }, dropdown =>
            {
                dropdown.BorderBrush = dropdown.Background = Brushes.Transparent;
                dropdown.ToolTip = Locale.ActionTypeTooltip;
            });

            // Duration slider (mainly for Instant actions)
            DurationPanel.AddSlider(Locale.TapDuration, Locale.Seconds, 0.01, 0.05, 0.01, 1).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = Locale.TapDurationTooltip;
            }).BindTo(() => Action.Duration);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Action?.CancelEdit();
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Action?.EndEdit();
            DialogResult = true;
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
