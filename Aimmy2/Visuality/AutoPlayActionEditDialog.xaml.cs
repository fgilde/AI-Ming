using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.Visuality;

namespace Visuality
{
    public partial class AutoPlayActionEditDialog : BaseDialog
    {
        private AutoPlayAction _action;

        public AutoPlayAction Action
        {
            get => _action;
            set
            {
                _action = value;
                _action?.BeginEdit();
                OnPropertyChanged();
                UpdateDynamicUi();
            }
        }

        public AutoPlayActionEditDialog()
        {
            InitializeComponent();
            DataContext = this;
            MainBorder.BindMouseGradientAngle(ShouldBindGradientMouse);
        }

        private void UpdateDynamicUi()
        {
            if (Action == null) return;

            DurationPanel.RemoveAll();

            // Action Type dropdown
            DurationPanel.AddDropdown("Action Type", Action.ActionType, type =>
            {
                Action.ActionType = type;
            }, dropdown =>
            {
                dropdown.BorderBrush = dropdown.Background = Brushes.Transparent;
                dropdown.ToolTip = "How this action behaves:\n" +
                    "• Continuous: Held until another action is chosen\n" +
                    "• Instant: Quick tap (jump, reload)\n" +
                    "• Modifier: Can combine with other actions (sprint, aim)\n" +
                    "• Toggle: Press once to toggle on/off (crouch)";
            });

            // Duration slider (mainly for Instant actions)
            DurationPanel.AddSlider("Tap Duration", "seconds", 0.01, 0.05, 0.01, 1).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = "How long to hold the key for instant/toggle actions.";
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
