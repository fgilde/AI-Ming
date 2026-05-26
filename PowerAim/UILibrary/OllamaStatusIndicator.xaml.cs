using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim.AILogic;
using PowerAim.AILogic.Contracts;
using PowerAim.Config;
using PowerAim;

namespace PowerAim.UILibrary
{
    public partial class OllamaStatusIndicator : UserControl
    {
        private readonly IOllamaClient _ollamaClient;
        private bool _isChecking;

        public OllamaStatusIndicator()
        {
            InitializeComponent();
            _ollamaClient = new OllamaClient();
            _ollamaClient.StatusChanged += OnStatusChanged;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateUrlDisplay();
            await CheckStatusAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _ollamaClient.StatusChanged -= OnStatusChanged;
            _ollamaClient.Dispose();
        }

        private void UpdateUrlDisplay()
        {
            var url = AppConfig.Current?.OllamaSettings?.BaseUrl ?? "http://localhost:11434";
            UrlText.Text = string.Format(Locale.OllamaUrlFormat, url);
        }

        private void OnStatusChanged(object? sender, OllamaStatusEventArgs e)
        {
            Dispatcher.BeginInvoke(() => UpdateStatusDisplay(e.IsAvailable, e.AvailableModels, e.ErrorMessage));
        }

        private void UpdateStatusDisplay(bool isAvailable, string[]? models, string? error)
        {
            if (isAvailable)
            {
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0x55, 0xFF, 0x55));
                StatusText.Text = Locale.OllamaConnected;
                ErrorText.Visibility = Visibility.Collapsed;

                if (models != null && models.Length > 0)
                {
                    var visionModels = models.Where(m =>
                        m.Contains("moondream", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("llava", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("qwen", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("bakllava", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("minicpm", StringComparison.OrdinalIgnoreCase)).ToArray();

                    ModelsText.Text = visionModels.Length > 0
                        ? string.Format(Locale.OllamaVisionModelsFormat, string.Join(", ", visionModels.Take(5)))
                        : string.Format(Locale.OllamaNoVisionModelsFormat, string.Join(", ", models.Take(5)));
                }
                else
                {
                    ModelsText.Text = Locale.OllamaNoModelsFound;
                }
            }
            else
            {
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
                StatusText.Text = Locale.OllamaDisconnected;
                ModelsText.Text = Locale.ModelsDashFallback;

                if (!string.IsNullOrEmpty(error))
                {
                    ErrorText.Text = string.Format(Locale.ErrorFormat, error);
                    ErrorText.Visibility = Visibility.Visible;
                }
                else
                {
                    ErrorText.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task CheckStatusAsync()
        {
            if (_isChecking) return;
            _isChecking = true;

            try
            {
                StatusText.Text = Locale.OllamaChecking;
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00));

                var isAvailable = await _ollamaClient.IsAvailableAsync();
                var models = await _ollamaClient.GetAvailableModelsAsync();

                UpdateStatusDisplay(isAvailable, models, _ollamaClient.LastError);
            }
            finally
            {
                _isChecking = false;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateUrlDisplay();
            await CheckStatusAsync();
        }
    }
}
