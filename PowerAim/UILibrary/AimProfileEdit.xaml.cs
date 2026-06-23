using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PowerAim;
using PowerAim.Config;
using PowerAim.Extensions;

namespace UILibrary
{
    /// <summary>
    ///     Editor form for a single <see cref="AimProfile"/>. Hosted by the in-window aim edit page
    ///     (analog to <see cref="AntiRecoilProfileEdit"/>). The tuning sliders/toggles are populated
    ///     from code-behind (reusing AddSlider/AddToggle + BindTo). A preset dropdown at the top
    ///     overwrites the tuning fields after a confirmation.
    /// </summary>
    public partial class AimProfileEdit : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ProfileProperty =
            DependencyProperty.Register(nameof(Profile), typeof(AimProfile), typeof(AimProfileEdit),
                new PropertyMetadata(null, OnProfileChanged));

        public AimProfile? Profile
        {
            get => (AimProfile?)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        private bool _suppressPreset;
        private AimProfile? _boundProfile;
        private PropertyChangedEventHandler? _tuneHandler;

        public AimProfileEdit()
        {
            InitializeComponent();
            DataContext = this;
        }

        private static void OnProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AimProfileEdit self)
            {
                // Rebuild the tuning panel whenever SmartAim flips so the smart-only controls
                // (deadzone, coasting, switch delay, lead, adaptive smoothing) show/hide.
                if (self._boundProfile != null && self._tuneHandler != null)
                    self._boundProfile.PropertyChanged -= self._tuneHandler;
                self._boundProfile = self.Profile;
                self._tuneHandler = (s, ev) =>
                {
                    if (ev.PropertyName == nameof(AimProfile.SmartAim))
                        self.Dispatcher.BeginInvoke(new Action(self.BuildTunePanel));
                };
                if (self.Profile != null) self.Profile.PropertyChanged += self._tuneHandler;

                self.BuildTunePanel();
                self.PopulatePresetCombo();
                self.PopulateOcrRegionCombo();
                self.PropertyChanged?.Invoke(self, new PropertyChangedEventArgs(nameof(Profile)));
            }
        }

        private void PopulatePresetCombo()
        {
            if (PresetCombo == null) return;
            _suppressPreset = true;
            var items = new List<string> { Locale.AimPresetChoose };
            items.AddRange(AimPreset.All.Select(p => p.DisplayName));
            PresetCombo.ItemsSource = items;
            PresetCombo.SelectedIndex = 0;
            _suppressPreset = false;
        }

        public void PopulateOcrRegionCombo()
        {
            if (OcrRegionCombo == null) return;
            var regions = AppConfig.Current?.OcrSettings?.Regions;
            var names = regions == null
                ? new List<string>()
                : regions.Select(r => r.Name ?? "").Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            OcrRegionCombo.ItemsSource = names;
            if (Profile != null && !string.IsNullOrEmpty(Profile.OcrRegionName) && names.Contains(Profile.OcrRegionName))
                OcrRegionCombo.SelectedItem = Profile.OcrRegionName;
        }

        private void BuildTunePanel()
        {
            if (TunePanel == null || Profile == null) return;
            TunePanel.Children.Clear();

            TunePanel.AddToggle(Locale.SmartAimEnabled, t => t.ToolTip = Locale.SmartAimEnabledTooltip)
                .BindTo(() => Profile!.SmartAim);

            // Sensitivity: fine-grained for high-DPI mice (issue #10) — 0.001 steps, no snap, 4 decimals.
            // Applies in both modes (different formula), so it's always shown.
            TunePanel.AddSlider(Locale.MouseSensitivity, Locale.Sensitivity, 0.001, 0.001, 0.0001, 1, false, 4, false)
                .BindTo(() => Profile!.Sensitivity);

            // Smart-only tuning — hidden when SmartAim is off (the legacy path ignores these).
            if (Profile!.SmartAim)
            {
                TunePanel.AddSlider(Locale.AimDeadzone, Locale.Pixels, 1, 1, 0, 20)
                    .InitWith(s => s.ToolTip = Locale.AimDeadzoneTooltip)
                    .BindTo(() => Profile!.DeadzonePx);
                TunePanel.AddSlider(Locale.AimCoastFrames, Locale.Amount, 1, 1, 1, 20)
                    .InitWith(s => s.ToolTip = Locale.AimCoastFramesTooltip)
                    .BindTo(() => Profile!.CoastFrames);
                TunePanel.AddSlider(Locale.AimSwitchDelay, Locale.Amount, 1, 1, 1, 20)
                    .InitWith(s => s.ToolTip = Locale.AimSwitchDelayTooltip)
                    .BindTo(() => Profile!.SwitchFrames);
                TunePanel.AddSlider(Locale.AimLeadMs, Locale.Milliseconds, 1, 1, 0, 100)
                    .InitWith(s => s.ToolTip = Locale.AimLeadMsTooltip)
                    .BindTo(() => Profile!.LeadTimeMs);
                TunePanel.AddToggle(Locale.AimAdaptiveSmoothing, t => t.ToolTip = Locale.AimAdaptiveSmoothingTooltip)
                    .BindTo(() => Profile!.UseOneEuro);
            }

            TunePanel.AddToggle(Locale.RandomAimPoint, t => t.ToolTip = Locale.RandomAimPointTooltip)
                .BindTo(() => Profile!.RandomAimPoint);
        }

        // ====================================================================== HANDLERS ====

        private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPreset || Profile == null) return;
            int idx = PresetCombo.SelectedIndex;
            if (idx <= 0) return; // placeholder
            var preset = AimPreset.All[idx - 1];

            var res = PowerAim.Visuality.MessageDialog.Show(
                string.Format(Locale.AimPresetApplyConfirmFormat, preset.DisplayName),
                Locale.AimPreset,
                PowerAim.Visuality.MessageDialog.DialogButtons.YesNo,
                PowerAim.Visuality.MessageDialog.DialogIcon.Question,
                owner: Window.GetWindow(this),
                defaultResult: PowerAim.Visuality.MessageDialog.DialogResult.Yes);

            if (res == PowerAim.Visuality.MessageDialog.DialogResult.Yes)
            {
                preset.Apply(Profile);
                BuildTunePanel(); // re-read all controls from the new values
            }

            // Reset back to the placeholder — the profile is "custom" now, the dropdown isn't a mode.
            _suppressPreset = true;
            PresetCombo.SelectedIndex = 0;
            _suppressPreset = false;
        }

        private void OcrRegionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Profile == null) return;
            if (OcrRegionCombo.SelectedItem is string name) Profile.OcrRegionName = name;
        }

        private void EditRegion_Click(object sender, RoutedEventArgs e)
        {
            if (Profile == null) return;
            new global::Visuality.EditHeadArea(Profile.AimRegion, model => Profile.AimRegion = model.ToRelativeRect())
            { Owner = Window.GetWindow(this) }.Show();
        }

        private void EditRegions_Click(object sender, RoutedEventArgs e)
        {
            new PowerAim.Visuality.OcrRegionsDialog { Owner = Window.GetWindow(this) }.ShowDialog();
            PopulateOcrRegionCombo();
        }

        private void EditDisengage_Click(object sender, RoutedEventArgs e)
        {
            if (Profile == null) return;
            new PowerAim.Visuality.AimDisengageDialog
            {
                Rules = Profile.DisengageRules,
                Owner = Window.GetWindow(this)
            }.ShowDialog();
        }
    }
}
