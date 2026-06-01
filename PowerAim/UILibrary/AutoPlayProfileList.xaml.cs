using PowerAim.Config;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PowerAim.Extensions;
using PowerAim;
using PowerAim.InputLogic;
using PowerAim.Types;
using PowerAim.UILibrary;
using Nextended.Core.Extensions;
using Visuality;

namespace UILibrary
{
    public partial class AutoPlayProfileList : UserControl
    {
        public ObservableCollection<AutoPlayProfile> Profiles
        {
            get => (ObservableCollection<AutoPlayProfile>)GetValue(ProfilesProperty);
            set => SetValue(ProfilesProperty, value);
        }

        public static readonly DependencyProperty ProfilesProperty =
            DependencyProperty.Register(nameof(Profiles), typeof(ObservableCollection<AutoPlayProfile>), typeof(AutoPlayProfileList),
                new PropertyMetadata(new ObservableCollection<AutoPlayProfile>()));

        public AutoPlayProfileList()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        ///     Hotkey routed back from the per-row <see cref="AKeyChanger"/>: flip the bound
        ///     profile's <see cref="AutoPlayProfile.Enabled"/> so the keybind acts as a quick
        ///     enable/disable switch. Same pattern as <c>TriggerList.ApplyBindingEnabled</c> and
        ///     <c>MappingProfileList.ApplyBindingEnabled</c>.
        /// </summary>
        private void ApplyBindingEnabled(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
        {
            if (e.Value.Sender.Tag is AutoPlayProfile profile)
            {
                // Swallow duplicate events from a double-subscribed keybind control (e.g. after
                // a CreateUI rebuild leaves a stale row subscribed) — same guard the other lists use.
                if (!KeybindToggleGuard.ShouldHandle(profile)) return;
                profile.Enabled = !profile.Enabled;
                Notifier.Notify(profile.Name, profile.Enabled);
            }
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AutoPlayProfile profile)
                MainWindow.Instance.OpenAutoPlayEditor(profile, isNew: false, commit: _ => { });
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var draft = new AutoPlayProfile();
            MainWindow.Instance.OpenAutoPlayEditor(draft, isNew: true, commit: saved =>
            {
                if (saved != null) Profiles.Add(saved);
            });
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AutoPlayProfile profile &&
                PowerAim.Visuality.MessageDialog.Show(string.Format(Locale.ConfirmDeleteAutoPlayProfileFormat, profile.Name), Locale.DeleteProfile,
                    PowerAim.Visuality.MessageDialog.DialogButtons.YesNo, PowerAim.Visuality.MessageDialog.DialogIcon.Question,
                    owner: Window.GetWindow(this), defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No) == PowerAim.Visuality.MessageDialog.DialogResult.Yes)
            {
                Profiles.Remove(profile);
            }
        }

        public AutoPlayProfileList BindTo(Expression<Func<ObservableCollection<AutoPlayProfile>>> fn)
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Profiles = fn.Compile()();

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Profiles = fn.Compile()();
                }
            };

            Profiles.CollectionChanged += (s, e) =>
            {
                propertyInfo.SetValue(owner, Profiles);
            };

            return this;
        }
    }
}
