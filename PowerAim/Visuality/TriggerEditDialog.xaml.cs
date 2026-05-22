using System.Windows;
using System.Windows.Input;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Visuality;

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
