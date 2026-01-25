using System.Windows;
using System.Windows.Input;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.Visuality;

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
            MainBorder.BindMouseGradientAngle(ShouldBindGradientMouse);
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
