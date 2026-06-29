using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PowerAim;
using PowerAim.Config;
using PowerAim.Extensions;

namespace PowerAim.UILibrary
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
        private bool _buildingTunePanel;
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
                // (deadzone, smoothing) show/hide.
                if (self._boundProfile != null && self._tuneHandler != null)
                    self._boundProfile.PropertyChanged -= self._tuneHandler;
                self._boundProfile = self.Profile;
                self._tuneHandler = (s, ev) =>
                {
                    // Rebuild when a control that shows/hides dependent controls changes: SmartAim (the
                    // whole smart-only block), UseTargetTracking (coast/switch sliders) and SmoothingMode
                    // (the 1€ min-cutoff/beta sliders, shown only in OneEuro mode).
                    if (ev.PropertyName is nameof(AimProfile.SmartAim)
                        or nameof(AimProfile.UseTargetTracking)
                        or nameof(AimProfile.SmoothingMode))
                        self.Dispatcher.BeginInvoke(new Action(self.BuildTunePanel));
                };
                if (self.Profile != null) self.Profile.PropertyChanged += self._tuneHandler;

                self.BuildTunePanel();
                self.PopulateAimKeyBinder();
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

        /// <summary>
        ///     Build the per-profile aim-key binder (hold-to-aim) once per profile load. Kept out of
        ///     <see cref="BuildTunePanel"/> so it isn't rebuilt (and any in-progress recording lost)
        ///     every time a tuning toggle flips. Binds the same <see cref="MultiKeyChanger"/> used for
        ///     the global aim key to this profile's own <see cref="AimProfile.AimKeyBindings"/>.
        /// </summary>
        private void PopulateAimKeyBinder()
        {
            if (AimKeyPanel == null || Profile == null) return;
            AimKeyPanel.Children.Clear();
            // Reuse the same titled MultiKeyChanger the global aim key uses (consistent UX), bound to
            // this profile's own AimKeyBindings. Built here (not in BuildTunePanel) so a tuning toggle
            // doesn't rebuild it. Recompute the key warning whenever the keys change.
            AimKeyPanel.AddMultiKeyChanger(Locale.AimProfileAimKey, Locale.AimProfileAimKeyHelp)
                .BindTo(() => Profile!.AimKeyBindings)
                .Changed += (_, _) => UpdateAimKeyWarning();
            UpdateAimKeyWarning();
        }

        /// <summary>
        ///     Show a warning when the profile has no aim key (it can never aim) or when one of its keys
        ///     is already used by another profile (holding it would make two profiles fight for the mouse).
        /// </summary>
        private void UpdateAimKeyWarning()
        {
            if (AimKeyWarning == null || Profile == null) return;

            if (!Profile.HasAimKey)
            {
                AimKeyWarning.Text = Locale.AimProfileNoKeyWarning;
                AimKeyWarning.Visibility = Visibility.Visible;
                return;
            }

            string? dupName = null;
            var profiles = AppConfig.Current?.AimSettings?.Profiles;
            if (profiles != null)
            {
                foreach (var other in profiles)
                {
                    if (ReferenceEquals(other, Profile) || other.Id == Profile.Id) continue;
                    bool shares = Profile.AimKeyBindings.Any(k => k is { IsValid: true }
                                  && other.AimKeyBindings.Any(o => o is { IsValid: true } && o.Equals(k)));
                    if (shares) { dupName = string.IsNullOrWhiteSpace(other.Name) ? "—" : other.Name; break; }
                }
            }

            if (dupName != null)
            {
                AimKeyWarning.Text = string.Format(Locale.AimProfileDuplicateKeyWarning, dupName);
                AimKeyWarning.Visibility = Visibility.Visible;
            }
            else
            {
                AimKeyWarning.Visibility = Visibility.Collapsed;
            }
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

            // Measure how far the game's view moves per unit of mouse input and apply a matching
            // sensitivity to THIS profile (re-added on request — the closed loop converges without it,
            // but calibration makes the slider game-independent instead of trial-and-error per game).
            TunePanel.AddButton(Locale.CalibrateSensitivity).Reader.Click += (_, _) =>
                new PowerAim.Visuality.CalibrationWizardDialog
                {
                    Owner = System.Windows.Window.GetWindow(this),
                    TargetProfile = Profile,
                }.ShowDialog();

            // Smart-only tuning — hidden when SmartAim is off (the legacy path ignores these). The aim
            // path is "nearest detection + proportional move", with two opt-in, ego-immune layers:
            // aim-point smoothing (None/EMA/1€) and target tracking (stable identity + switch
            // hysteresis). Velocity lead is intentionally absent (broken in this closed loop).
            if (Profile!.SmartAim)
            {
                TunePanel.AddSlider(Locale.AimDeadzone, Locale.Pixels, 1, 1, 0, 20)
                    .InitWith(s => s.ToolTip = Locale.AimDeadzoneTooltip)
                    .BindTo(() => Profile!.DeadzonePx);

                // Smoothing mode dropdown (replaces the old boolean that silently ran a fixed EMA).
                // The combo auto-selects its first item while populating, which synchronously raises the
                // item's Selected; _buildingTunePanel suppresses that spurious write so it can't clobber
                // SmoothingMode (and churn the profile's dirty state) during the build.
                _buildingTunePanel = true;
                TunePanel.AddDropdown(Locale.AimSmoothing, Profile!.SmoothingMode,
                    new[] { AimSmoothingMode.None, AimSmoothingMode.Ema, AimSmoothingMode.OneEuro },
                    v => { if (!_buildingTunePanel) Profile!.SmoothingMode = v; },
                    toStringFn: SmoothingLabel);
                _buildingTunePanel = false;

                // 1€ filter has two intuitive params — only meaningful (and only shown) in OneEuro mode.
                if (Profile!.SmoothingMode == AimSmoothingMode.OneEuro)
                {
                    TunePanel.AddSlider(Locale.AimSmoothingCutoff, Locale.Amount, 0.1, 0.1, 0.1, 10, false, 2, false)
                        .InitWith(s => s.ToolTip = Locale.AimSmoothingCutoffTooltip)
                        .BindTo(() => Profile!.OneEuroMinCutoff);
                    TunePanel.AddSlider(Locale.AimSmoothingBeta, Locale.Amount, 0.05, 0.05, 0, 5, false, 2, false)
                        .InitWith(s => s.ToolTip = Locale.AimSmoothingBetaTooltip)
                        .BindTo(() => Profile!.OneEuroBeta);
                }

                TunePanel.AddToggle(Locale.AimTargetTracking, t => t.ToolTip = Locale.AimTargetTrackingTooltip)
                    .BindTo(() => Profile!.UseTargetTracking);
                if (Profile!.UseTargetTracking)
                {
                    TunePanel.AddSlider(Locale.AimCoastFrames, Locale.Amount, 1, 1, 1, 20)
                        .InitWith(s => s.ToolTip = Locale.AimCoastFramesTooltip)
                        .BindTo(() => Profile!.CoastFrames);
                    TunePanel.AddSlider(Locale.AimSwitchDelay, Locale.Amount, 1, 1, 1, 20)
                        .InitWith(s => s.ToolTip = Locale.AimSwitchDelayTooltip)
                        .BindTo(() => Profile!.SwitchFrames);
                }
            }
            else
            {
                // Legacy-path feel — moved here from the old global menu (the smart path uses
                // SmoothingMode + target tracking instead and ignores these).
                TunePanel.AddToggle(Locale.EMASmoothening).BindTo(() => Profile!.EmaSmoothing);
                TunePanel.AddToggle(Locale.Predictions).BindTo(() => Profile!.Predictions);
                _buildingTunePanel = true;
                TunePanel.AddDropdown(Locale.PredictionMethod, Profile!.PredictionMethod,
                    v => { if (!_buildingTunePanel) Profile!.PredictionMethod = v; });
                _buildingTunePanel = false;
            }
            // RandomAimPoint lives in the Aim-region section of the XAML (it picks a point INSIDE the
            // region), not here.
        }

        private static string SmoothingLabel(AimSmoothingMode mode) => mode switch
        {
            AimSmoothingMode.None => Locale.AimSmoothingNone,
            AimSmoothingMode.Ema => Locale.AimSmoothingEma,
            AimSmoothingMode.OneEuro => Locale.AimSmoothingOneEuro,
            _ => mode.ToString(),
        };

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
            new global::PowerAim.Visuality.EditHeadArea(Profile.AimRegion, model => Profile.AimRegion = model.ToRelativeRect())
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
