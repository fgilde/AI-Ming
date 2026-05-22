using PowerAim.Config;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Visuality;

namespace UILibrary
{
    public partial class AutoPlayActionList : UserControl
    {
        public ObservableCollection<AutoPlayAction> Actions
        {
            get => (ObservableCollection<AutoPlayAction>)GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public static readonly DependencyProperty ActionsProperty =
            DependencyProperty.Register(nameof(Actions), typeof(ObservableCollection<AutoPlayAction>), typeof(AutoPlayActionList),
                new PropertyMetadata(new ObservableCollection<AutoPlayAction>()));

        public AutoPlayActionList()
        {
            InitializeComponent();
        }

        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void EditAction_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AutoPlayAction action)
            {
                var dlg = new AutoPlayActionEditDialog
                {
                    Title = "Edit Action",
                    Action = action
                };
                dlg.ShowDialog();
            }
        }

        private void AddAction_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AutoPlayActionEditDialog
            {
                Title = "Add Action",
                Action = new AutoPlayAction()
            };
            if (dlg.ShowDialog() ?? false)
            {
                Actions.Add(dlg.Action);
            }
        }

        private void DeleteAction_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AutoPlayAction action &&
                PowerAim.Visuality.MessageDialog.Show($"Delete action '{action.Name}'?", "Delete Action",
                    PowerAim.Visuality.MessageDialog.DialogButtons.YesNo, PowerAim.Visuality.MessageDialog.DialogIcon.Question,
                    owner: Window.GetWindow(this), defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No) == PowerAim.Visuality.MessageDialog.DialogResult.Yes)
            {
                Actions.Remove(action);
            }
        }
    }
}
