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
            DurationPanel.AddSlider("Key Hold Duration", "seconds", 0.01, 0.05, 0.01, 2).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = "How long to hold the key(s) down.";
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
