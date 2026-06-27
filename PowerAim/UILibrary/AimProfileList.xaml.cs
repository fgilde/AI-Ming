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
    ///     Aim profile list — direct analog of <see cref="AntiRecoilProfileList"/>. Each row has a
    ///     per-profile hotkey (<see cref="AKeyChanger"/>) that toggles the profile active, an
    ///     active-state toggle, and edit/delete buttons. Activation routes through
    ///     <see cref="AimProfileManager"/> (radio + apply-to-globals).
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

        /// <summary>Hotkey routed from <see cref="AKeyChanger"/>: toggle the bound profile active (radio).</summary>
        private void ApplyBindingActive(object? sender, EventArgs<(AKeyChanger Sender, string Key, StoredInputBinding KeyBinding)> e)
        {
            if (e.Value.Sender.Tag is not AimProfile profile) return;
            if (!PowerAim.KeybindToggleGuard.ShouldHandle(profile)) return;

            var settings = AppConfig.Current?.AimSettings;
            if (settings == null) return;
            var newId = settings.ActiveProfileId == profile.Id ? "" : profile.Id;
            AimProfileManager.Instance.SetActiveProfile(newId, notify: true);
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
                // First-profile UX: auto-activate when nothing is active yet so it takes effect.
                var settings = AppConfig.Current?.AimSettings;
                if (settings != null && settings.ActiveProfile == null)
                    AimProfileManager.Instance.SetActiveProfile(saved.Id, notify: true);
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
                if (settings != null && settings.ActiveProfileId == profile.Id)
                {
                    AimProfile? fallback = null;
                    foreach (var p in settings.Profiles)
                    {
                        if (p.Id == profile.Id) continue;
                        if (p.IsValid) { fallback = p; break; }
                    }
                    AimProfileManager.Instance.SetActiveProfile(fallback?.Id ?? "", notify: false);
                }
                Profiles.Remove(profile);
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
