using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nextended.Core;
using Nextended.Core.Extensions;
using PowerAim;
using PowerAim.AILogic;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.Types;
using PowerAim.UILibrary;
using PowerAim.Visuality;

namespace PowerAim.UILibrary
{
    /// <summary>
    ///     Anti-Recoil profile list. Each row exposes a per-profile hotkey (via
    ///     <see cref="AKeyChanger"/>) plus a toggle that flips the radio-active state. The hotkey
    ///     dispatches through <see cref="ApplyBindingActive"/> which calls into
    ///     <see cref="AntiRecoilProfileManager"/> — that's the single source of truth for which
    ///     profile is active. Editing pushes the user into the in-window AntiRecoil edit page
    ///     (analog to the Trigger / AutoPlay editors).
    /// </summary>
    public partial class AntiRecoilProfileList : UserControl
    {
        public ObservableCollection<AntiRecoilProfile> Profiles
        {
            get => (ObservableCollection<AntiRecoilProfile>)GetValue(ProfilesProperty);
            set => SetValue(ProfilesProperty, value);
        }

        public static readonly DependencyProperty ProfilesProperty =
            DependencyProperty.Register(nameof(Profiles), typeof(ObservableCollection<AntiRecoilProfile>), typeof(AntiRecoilProfileList),
                new PropertyMetadata(new ObservableCollection<AntiRecoilProfile>()));

        public AntiRecoilProfileList()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void SwallowMouse(object sender, MouseButtonEventArgs e) => e.Handled = true;

        /// <summary>
        ///     Hotkey routed back from <see cref="AKeyChanger"/>: toggle the bound profile's
        ///     active state through <see cref="AntiRecoilProfileManager"/> (radio behaviour:
        ///     same key again on the currently-active profile clears active).
        /// </summary>
        private void ApplyBindingActive(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
        {
            if (e.Value.Sender.Tag is not AntiRecoilProfile profile) return;
            // Guard duplicates from a double-subscribed keybind control (same shape as Mapping).
            if (!PowerAim.KeybindToggleGuard.ShouldHandle(profile)) return;

            var settings = AppConfig.Current?.AntiRecoilSettings;
            if (settings == null) return;
            var newId = settings.ActiveProfileId == profile.Id ? "" : profile.Id;
            AntiRecoilProfileManager.Instance.SetActiveProfile(newId, notify: true);
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AntiRecoilProfile profile)
                MainWindow.Instance!.OpenAntiRecoilEditor(profile, isNew: false, commit: _ => { });
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var draft = new AntiRecoilProfile { Name = "New profile" };
            MainWindow.Instance!.OpenAntiRecoilEditor(draft, isNew: true, commit: saved =>
            {
                if (saved == null) return;
                Profiles.Add(saved);
                // First-profile UX: if nothing is currently active (no migration seed + no manual
                // hotkey activation yet), auto-activate the newly created profile. Otherwise the
                // master AntiRecoil toggle has nothing to apply and silently no-ops — that was
                // the "I have it on and a profile on, but nothing happens" bug.
                var settings = AppConfig.Current?.AntiRecoilSettings;
                if (settings != null && settings.ActiveProfile == null)
                {
                    AntiRecoilProfileManager.Instance.SetActiveProfile(saved.Id, notify: true);
                }
            });
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not AntiRecoilProfile profile) return;
            var prompt = string.Format(Locale.ConfirmDeleteAutoPlayProfileFormat, profile.Name);
            if (PowerAim.Visuality.MessageDialog.Show(prompt, Locale.DeleteProfile,
                    PowerAim.Visuality.MessageDialog.DialogButtons.YesNo,
                    PowerAim.Visuality.MessageDialog.DialogIcon.Question,
                    owner: Window.GetWindow(this),
                    defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.No) == PowerAim.Visuality.MessageDialog.DialogResult.Yes)
            {
                var settings = AppConfig.Current?.AntiRecoilSettings;
                if (settings != null && settings.ActiveProfileId == profile.Id)
                {
                    // Fall back to a sibling instead of clearing the active id outright — same
                    // reasoning as AddProfile_Click. Picking the first remaining valid profile
                    // keeps anti-recoil working when the user just wanted to swap one of several.
                    AntiRecoilProfile? fallback = null;
                    foreach (var p in settings.Profiles)
                    {
                        if (p.Id == profile.Id) continue;
                        if (p.IsValid) { fallback = p; break; }
                    }
                    AntiRecoilProfileManager.Instance.SetActiveProfile(fallback?.Id ?? "", notify: false);
                }
                Profiles.Remove(profile);
            }
        }

        public AntiRecoilProfileList BindTo(Expression<Func<ObservableCollection<AntiRecoilProfile>>> fn)
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
