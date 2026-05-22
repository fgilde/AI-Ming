using System.Windows;
using System.Windows.Input;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Visuality;

namespace Visuality
{
    public partial class AutoPlayProfileEditDialog : BaseDialog
    {
        public AutoPlayProfile Profile
        {
            get => ProfileEdit.Profile;
            set
            {
                ProfileEdit.Profile = value;
                Profile?.BeginEdit();
            }
        }

        public AutoPlayProfileEditDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Profile?.CancelEdit();
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Profile?.EndEdit();
            DialogResult = true;
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
