using PowerAim.Config;
using PowerAim.UILibrary;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PowerAim.Extensions;
using System.Windows.Input;
using PowerAim;
using PowerAim.InputLogic;
using PowerAim.Types;
using Nextended.Core.Extensions;
using PowerAim.Visuality;

namespace PowerAim.UILibrary
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
                MainWindow.Instance.OpenTriggerEditor(trigger, isNew: false, commit: _ => { });
            }
        }

        private void AddTrigger_Click(object sender, RoutedEventArgs e)
        {
            var draft = new ActionTrigger();
            MainWindow.Instance.OpenTriggerEditor(draft, isNew: true, commit: saved =>
            {
                if (saved != null) Triggers.Add(saved);
            });
        }

        private void DeleteTrigger_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ActionTrigger trigger
                && PowerAim.Visuality.MessageDialog.Confirm(
                    Locale.ConfirmDeleteTrigger.FormatWith(trigger.Name),
                    Locale.DeleteTrigger,
                    owner: Window.GetWindow(this),
                    icon: PowerAim.Visuality.MessageDialog.DialogIcon.Warning,
                    defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No))
                Triggers.Remove(trigger);
        }

        private void ApplyBindingEnabled(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
        {
            var args = e.Value;
            if (args.Sender.Tag is ActionTrigger trigger)
            {
               // Swallow duplicate events from a double-subscribed keybind control (otherwise the
               // trigger toggles twice = net zero, and the notice fires twice off/on).
               if (!KeybindToggleGuard.ShouldHandle(trigger)) return;
               trigger.Enabled = !trigger.Enabled;
               Notifier.Notify(trigger.Name, trigger.Enabled);
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
