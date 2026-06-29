using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nextended.Core.Extensions;
using PowerAim;
using PowerAim.AILogic;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.Types;
using PowerAim.UILibrary;

namespace PowerAim.UILibrary
{
    /// <summary>
    ///     Aim profile list. Every enabled profile is "live" and aims on its OWN aim-key (set in the
    ///     editor) — there is no single-active radio any more. Each row has an Enabled toggle, an
    ///     "aiming now" badge (<see cref="AimProfile.IsEffective"/>) and edit/delete buttons.
    /// </summary>
    public partial class AimProfileList : UserControl
    {
        public ObservableCollection<AimProfile> Profiles
        {
            get => (ObservableCollection<AimProfile>)GetValue(ProfilesProperty);
            set => SetValue(ProfilesProperty, value);
        }

        public static readonly DependencyProperty ProfilesProperty =
            DependencyProperty.Register(nameof(Profiles), typeof(ObservableCollection<AimProfile>), typeof(AimProfileList),
                new PropertyMetadata(new ObservableCollection<AimProfile>()));

        public AimProfileList()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void SwallowMouse(object sender, MouseButtonEventArgs e) => e.Handled = true;

        /// <summary>Per-profile keybind: press toggles this profile Enabled/disabled (like a trigger's enable key).</summary>
        private void ApplyBindingEnabled(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
        {
            if (e.Value.Sender.Tag is not AimProfile profile) return;
            // Swallow duplicate events from a double-subscribed keybind control (otherwise it toggles
            // twice = net zero and the notice fires twice).
            if (!PowerAim.KeybindToggleGuard.ShouldHandle(profile)) return;
            profile.Enabled = !profile.Enabled;
            Notifier.Notify(profile.Name, profile.Enabled);
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AimProfile profile)
                MainWindow.Instance!.OpenAimEditor(profile, isNew: false, commit: _ => { });
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var draft = new AimProfile { Name = "New profile" };
            draft.CaptureFromGlobals(); // start from the current live feel
            MainWindow.Instance!.OpenAimEditor(draft, isNew: true, commit: saved =>
            {
                if (saved == null) return;
                Profiles.Add(saved);
                // No activation step — the profile is live as soon as it exists and aims whenever its
                // own aim-key is held (set in the editor).
            });
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not AimProfile profile) return;
            var prompt = string.Format(Locale.ConfirmDeleteAutoPlayProfileFormat, profile.Name);
            if (PowerAim.Visuality.MessageDialog.Show(prompt, Locale.DeleteProfile,
                    PowerAim.Visuality.MessageDialog.DialogButtons.YesNo,
                    PowerAim.Visuality.MessageDialog.DialogIcon.Question,
                    owner: Window.GetWindow(this),
                    defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No) == PowerAim.Visuality.MessageDialog.DialogResult.Yes)
            {
                var settings = AppConfig.Current?.AimSettings;
                Profiles.Remove(profile);
                // ActiveProfileId is now only an internal migration anchor; keep it pointing at a real
                // profile if it happened to reference the deleted one.
                if (settings != null && settings.ActiveProfileId == profile.Id)
                {
                    string newId = "";
                    foreach (var p in settings.Profiles) { if (p.IsValid) { newId = p.Id; break; } }
                    settings.ActiveProfileId = newId;
                }
            }
        }

        public AimProfileList BindTo(Expression<Func<ObservableCollection<AimProfile>>> fn)
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Profiles = fn.Compile()();

            owner.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == propertyInfo.Name) Profiles = fn.Compile()();
            };

            Profiles.CollectionChanged += (_, _) => propertyInfo.SetValue(owner, Profiles);

            return this;
        }
    }
}
