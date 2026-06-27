using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim.AILogic;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim;

namespace PowerAim.UILibrary
{
    public partial class AutoPlayProfileEdit : UserControl
    {
        private OllamaClient? _ollama;
        private bool _modelsLoaded;

        public AutoPlayProfile Profile
        {
            get => (AutoPlayProfile)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        public static readonly DependencyProperty ProfileProperty =
            DependencyProperty.Register(nameof(Profile), typeof(AutoPlayProfile), typeof(AutoPlayProfileEdit),
                new PropertyMetadata(null, ProfileChanged));

        private static void ProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AutoPlayProfileEdit)d).ProfileChanged();
        }

        private void ProfileChanged()
        {
            UpdateDynamicUi();
            // Ensure the ActionsList binding is updated when Profile changes
            if (ActionsList != null && Profile != null)
            {
                ActionsList.Actions = Profile.Actions;
            }
        }

        private void UpdateDynamicUi()
        {
            DecisionIntervalPanel.RemoveAll();
            DecisionIntervalPanel.AddSlider(Locale.DecisionInterval, Locale.Seconds, 0.1, 0.5, 0.3, 10).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
                slider.ToolTip = Locale.DecisionIntervalTooltip;
            }).BindTo(() => Profile.DecisionInterval);

            // Per-profile sens-scale: keeps the same multiplier metaphor as the rest of the
            // app's slider-driven knobs (range 0.1x–3x, fine 0.05 steps).
            MouseSensScalePanel.RemoveAll();
            MouseSensScalePanel.AddSlider(Locale.AutoPlayMouseSensScale, Locale.Multiplier, 0.05, 0.1, 0.1, 3.0).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
            }).BindTo(() => Profile.MouseSensScale);

            // Anti-detection jitter: integer pixels and integer ms, both default 0 = off.
            MouseJitterPanel.RemoveAll();
            MouseJitterPanel.AddSlider(Locale.AutoPlayMouseJitter, Locale.Pixels, 1, 1, 0, 15).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
            }).BindTo(() => Profile.MouseJitterPx);

            KeyDelayJitterPanel.RemoveAll();
            KeyDelayJitterPanel.AddSlider(Locale.AutoPlayKeyDelayJitter, Locale.Milliseconds, 1, 1, 0, 25).InitWith(slider =>
            {
                slider.BorderBrush = slider.Background = Brushes.Transparent;
            }).BindTo(() => Profile.KeyDelayJitterMs);
        }

        public AutoPlayProfileEdit()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += AutoPlayProfileEdit_OnLoaded;
            // The Text TwoWay binding alone can lose its value when ItemsSource is (re)set: WPF's
            // editable ComboBox null-resets Text when it can't reconcile against the new items.
            // Mirror the selected item back into Profile.OllamaModel + ComboBox.Text on every
            // SelectionChanged so picking from the dropdown is always sticky.
            ModelCombo.SelectionChanged += ModelCombo_SelectionChanged;
        }

        private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelCombo.SelectedItem is string picked && !string.IsNullOrWhiteSpace(picked))
            {
                if (Profile != null) Profile.OllamaModel = picked;
                ModelCombo.Text = picked;
            }
        }

        private async void AutoPlayProfileEdit_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_modelsLoaded) return;
            _modelsLoaded = true;
            await ReloadInstalledModelsAsync();
        }

        /// <summary>
        ///     Populate the model ComboBox with installed Ollama models (from <c>/api/tags</c>) plus
        ///     the curated recommended vision models so the user always sees a useful list — even
        ///     if Ollama isn't running yet. Recommended-but-not-installed entries are added at the
        ///     end so the installed set comes first.
        /// </summary>
        private async Task ReloadInstalledModelsAsync()
        {
            _ollama ??= new OllamaClient();
            string[] installed = [];
            try
            {
                if (await _ollama.IsAvailableAsync())
                    installed = await _ollama.GetAvailableModelsAsync();
            }
            catch { /* swallow — combobox just falls back to recommended list */ }

            var merged = new List<string>(installed);
            foreach (var rec in OllamaClient.RecommendedVisionModels)
                if (!installed.Any(m => m.StartsWith(rec, StringComparison.OrdinalIgnoreCase)))
                    merged.Add(rec);

            // Setting ItemsSource on an editable ComboBox with a Text TwoWay binding can null-reset
            // the displayed text (and write that null back into Profile.OllamaModel). Read the
            // authoritative value from the source FIRST, populate items, then restore on a Loaded
            // dispatcher tick so the binding has had time to re-sync.
            var authoritative = Profile?.OllamaModel ?? ModelCombo.Text;
            ModelCombo.ItemsSource = merged;
            if (!string.IsNullOrEmpty(authoritative))
            {
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    ModelCombo.Text = authoritative;
                    if (Profile != null && Profile.OllamaModel != authoritative)
                        Profile.OllamaModel = authoritative;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        ///     Pulls the model currently in the ComboBox via <c>ollama pull</c> (HTTP) and shows a
        ///     live status line. Requires the Ollama server to be reachable — if it isn't, we
        ///     short-circuit with a hint instead of timing out at the bottom of the stack.
        /// </summary>
        private async void PullModel_Click(object sender, RoutedEventArgs e)
        {
            var name = (ModelCombo.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowStatus(Locale.OllamaPullEmpty, isError: true);
                return;
            }

            _ollama ??= new OllamaClient();
            if (!await _ollama.IsAvailableAsync())
            {
                ShowStatus(Locale.OllamaPullServerOffline, isError: true);
                return;
            }

            PullModelButton.IsEnabled = false;
            try
            {
                var progress = new Progress<OllamaClient.PullProgress>(p =>
                {
                    // Report compact "<status> · <pct>%" so the user sees both phase + numbers
                    // (manifest → downloading → verifying → success).
                    var pct = p.Percent > 0 ? $" · {p.Percent:0}%" : "";
                    ShowStatus(string.Format(Locale.OllamaPullingFormat, name, p.Status + pct), isError: false);
                });
                await _ollama.PullModelAsync(name, progress);
                ShowStatus(string.Format(Locale.OllamaPullDoneFormat, name), isError: false);
                _modelsLoaded = false;
                await ReloadInstalledModelsAsync();
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(Locale.OllamaPullFailedFormat, ex.Message), isError: true);
            }
            finally
            {
                PullModelButton.IsEnabled = true;
            }
        }

        private void ShowStatus(string text, bool isError)
        {
            PullStatusText.Text = text;
            PullStatusText.Visibility = Visibility.Visible;
            PullStatusText.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23))
                : (Brush)FindResource("FluentTextSecondary");
        }
    }
}
