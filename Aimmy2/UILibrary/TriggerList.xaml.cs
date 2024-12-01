using Aimmy2.Config;
using Aimmy2.UILibrary;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Aimmy2.Extensions;
using System.Windows.Input;
using Aimmy2;
using Aimmy2.InputLogic;
using Aimmy2.Types;
using Nextended.Core.Extensions;
using Visuality;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for TriggerList.xaml
    /// </summary>
    public partial class TriggerList : UserControl
    {
        
        public ObservableCollection<ActionTrigger> Triggers          
        {
            get => (ObservableCollection<ActionTrigger>)GetValue(TriggersProperty);
            set => SetValue(TriggersProperty, value);
        }

        public static readonly DependencyProperty TriggersProperty =
            DependencyProperty.Register(nameof(Triggers), typeof(ObservableCollection<ActionTrigger>), typeof(TriggerList), new PropertyMetadata(new ObservableCollection<ActionTrigger>()));


        public TriggerList()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void EditTrigger_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ActionTrigger trigger)
            {
                var dlg = new TriggerEditDialog()
                {
                    Title = "Edit Trigger",
                    Trigger = trigger
                };
                dlg.ShowDialog();
            }
        }

        private void AddTrigger_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new TriggerEditDialog()
            {
                Title = "Add Trigger",
                Trigger = new ActionTrigger()
            };
            if(dlg.ShowDialog() ?? false)
            {
                Triggers.Add(dlg.Trigger);
            }
        }

        private void DeleteTrigger_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ActionTrigger trigger && MessageBox.Show(Locale.ConfirmDeleteTrigger.FormatWith(trigger.Name), Locale.DeleteModel, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                Triggers.Remove(trigger);
        }

        private void ApplyBindingEnabled(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
        {
            var args = e.Value;
            if (args.Sender.Tag is ActionTrigger trigger)
            {
               trigger.Enabled = !trigger.Enabled;
            }
        }

        public TriggerList BindTo(Expression<Func<ObservableCollection<ActionTrigger>>> fn)
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Triggers = fn.Compile()();

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Triggers = fn.Compile()();
                }
            };

            Triggers.CollectionChanged += (s, e) =>
            {
                propertyInfo.SetValue(owner, Triggers);
            };

            return this;
        }


    }
}
