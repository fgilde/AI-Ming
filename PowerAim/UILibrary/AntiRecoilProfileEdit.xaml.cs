using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim;
using PowerAim.AILogic;
using PowerAim.Config;
using PowerAim.Extensions;

namespace UILibrary
{
    /// <summary>
    ///     Form for editing a single <see cref="AntiRecoilProfile"/>. Hosted by the in-window
    ///     AntiRecoil edit page (analog to the AutoPlay / Trigger edit pages). Sliders, mode-
    ///     specific sub-panels and the OCR-region picker are populated from code-behind so the
    ///     binding wiring matches the project's <see cref="UIElementExtensions.AddSlider"/> +
    ///     <see cref="UIElementExtensions.BindTo"/> conventions.
    /// </summary>
    public partial class AntiRecoilProfileEdit : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ProfileProperty =
            DependencyProperty.Register(nameof(Profile), typeof(AntiRecoilProfile),
                typeof(AntiRecoilProfileEdit),
                new PropertyMetadata(null, OnProfileChanged));

        public AntiRecoilProfile? Profile
        {
            get => (AntiRecoilProfile?)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        public AntiRecoilProfileEdit()
        {
            InitializeComponent();
            DataContext = this;
        }

        private static void OnProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AntiRecoilProfileEdit self)
            {
                self.RebuildModeSpecificSections();
                self.PopulateModeCombo();
                self.PopulateOcrRegionCombo();
                self.PropertyChanged?.Invoke(self, new PropertyChangedEventArgs(nameof(Profile)));
            }
        }

        // ====================================================================== INIT BLOCKS ====

        private void PopulateModeCombo()
        {
            if (ModeCombo == null || Profile == null) return;
            ModeCombo.ItemsSource = new[]
            {
                new ModeOption(AntiRecoilMode.Legacy,          Locale.AntiRecoilModeLegacy),
                new ModeOption(AntiRecoilMode.ImageBased,      Locale.AntiRecoilModeImageBased),
                new ModeOption(AntiRecoilMode.PatternPlayback, Locale.AntiRecoilModePatternPlayback),
            };
            ModeCombo.DisplayMemberPath = nameof(ModeOption.Label);
            ModeCombo.SelectedItem = ((IEnumerable<ModeOption>)ModeCombo.ItemsSource)
                .FirstOrDefault(o => o.Mode == Profile.Mode);
            UpdateModeSectionVisibility();
        }

        /// <summary>
        ///     (Re)load the OCR-region picker from <see cref="OcrSettings.Regions"/>. Called when
        ///     the Profile changes and after the user closes the OCR-regions dialog so newly
        ///     created regions appear without leaving the editor.
        /// </summary>
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

        // -------------- mode-specific section builders --

        private void RebuildModeSpecificSections()
        {
            if (Profile == null) return;
            BuildLegacyPanel();
            BuildImageBasedPanel();
            BuildPatternPanel();
            UpdateModeSectionVisibility();
        }

        private void BuildLegacyPanel()
        {
            LegacyPanel.Children.Clear();
            LegacyPanel.AddSlider(Locale.HoldTime,        Locale.Milliseconds, 1, 1, 1, 1000, true).BindTo(() => Profile!.HoldTime);
            LegacyPanel.AddSlider(Locale.FireRate,        Locale.Milliseconds, 1, 1, 1, 5000, true).BindTo(() => Profile!.FireRate);
            LegacyPanel.AddSlider(Locale.YRecoilUpDown,   Locale.Move,         1, 1, -1000, 1000, true).BindTo(() => Profile!.YRecoil);
            LegacyPanel.AddSlider(Locale.XRecoilLeftRight,Locale.Move,         1, 1, -1000, 1000, true).BindTo(() => Profile!.XRecoil);
        }

        private void BuildImageBasedPanel()
        {
            ImageBasedPanel.Children.Clear();
            ImageBasedPanel.AddSlider(Locale.AntiRecoilStrength, Locale.Amount, 0.05, 0.05, 0, 1.5)
                .InitWith(s => s.ToolTip = Locale.AntiRecoilStrengthHelp)
                .BindTo(() => Profile!.AutoStrength);
        }

        private void BuildPatternPanel()
        {
            PatternPanel.Children.Clear();
            // Pattern picker (ComboBox) — built explicitly here so we can name it and reach it from
            // SelectionChanged.
            var label = new Label
            {
                Foreground = (Brush)FindResource("Foreground"),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                Content = Locale.AntiRecoilPatternName,
            };
            var combo = new ComboBox
            {
                Name = "PatternCombo",
                Margin = new Thickness(2, 5, 2, 5),
                DisplayMemberPath = nameof(RecoilPattern.Name),
            };
            combo.SelectionChanged += PatternCombo_SelectionChanged;
            PatternPanel.Children.Add(label);
            PatternPanel.Children.Add(combo);
            PatternPanel.AddSlider(Locale.AntiRecoilStrength, Locale.Amount, 0.05, 0.05, 0, 3)
                .BindTo(() => Profile!.PatternStrength);
            // Loop-on-hold toggle. Defaults true on the profile, so the visible state matches a
            // freshly-created profile out of the box. Disabling it restores the previous "freeze
            // at last sample" behaviour for one-shot patterns covering the whole mag.
            PatternPanel.AddToggle(Locale.AntiRecoilLoopPattern,
                    t => t.ToolTip = Locale.AntiRecoilLoopPatternTooltip)
                .BindTo(() => Profile!.LoopPattern);

            var patterns = AppConfig.Current?.AntiRecoilSettings?.Patterns;
            combo.ItemsSource = patterns;
            if (Profile != null && patterns != null)
                combo.SelectedItem = patterns.FirstOrDefault(p => p.Name == Profile.PatternName);
        }

        // ====================================================================== HANDLERS ====

        private void UpdateModeSectionVisibility()
        {
            if (Profile == null) return;
            LegacyPanel.Visibility     = Profile.Mode == AntiRecoilMode.Legacy          ? Visibility.Visible : Visibility.Collapsed;
            ImageBasedPanel.Visibility = Profile.Mode == AntiRecoilMode.ImageBased      ? Visibility.Visible : Visibility.Collapsed;
            PatternPanel.Visibility    = Profile.Mode == AntiRecoilMode.PatternPlayback ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Profile == null) return;
            if (ModeCombo.SelectedItem is ModeOption opt)
            {
                Profile.Mode = opt.Mode;
                UpdateModeSectionVisibility();
            }
        }

        private void PatternCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Profile == null) return;
            if (sender is ComboBox cb && cb.SelectedItem is RecoilPattern p)
                Profile.PatternName = p.Name;
        }

        private void OcrRegionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Profile == null) return;
            if (OcrRegionCombo.SelectedItem is string s)
                Profile.OcrRegionName = s;
        }

        /// <summary>
        ///     Open the OCR regions configurator without leaving the AntiRecoil profile editor.
        ///     After the dialog closes we re-populate the picker so freshly added regions show up.
        /// </summary>
        private void EditRegions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new PowerAim.Visuality.OcrRegionsDialog
                {
                    Owner = Window.GetWindow(this),
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AntiRecoilProfileEdit] OcrRegionsDialog failed: {ex.Message}");
            }
            PopulateOcrRegionCombo();
        }

        private sealed record ModeOption(AntiRecoilMode Mode, string Label);
    }
}
