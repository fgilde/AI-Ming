using System.Windows;
using System.Windows.Input;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.Visuality;

namespace Visuality
{
    public partial class TriggerEditDialog : BaseDialog
    {
        public ActionTrigger Trigger
        {
            get => TriggerEdit.Trigger;
            set
            {
                TriggerEdit.Trigger = value;
                Trigger?.BeginEdit();
            }
        }

        public TriggerEditDialog()
        {
            InitializeComponent();
            DataContext = this;
            MainBorder.BindMouseGradientAngle(ShouldBindGradientMouse);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Trigger?.CancelEdit();
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Trigger?.EndEdit();
            DialogResult = true;
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }

}
