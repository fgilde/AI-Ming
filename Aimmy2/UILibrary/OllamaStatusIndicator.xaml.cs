using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aimmy2.AILogic;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;

namespace Aimmy2.UILibrary
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
            UrlText.Text = $"URL: {url}";
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
                StatusText.Text = "Ollama: Connected";
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
                        ? $"Vision Models: {string.Join(", ", visionModels.Take(5))}"
                        : $"Models: {string.Join(", ", models.Take(5))} (no vision models)";
                }
                else
                {
                    ModelsText.Text = "Models: None found";
                }
            }
            else
            {
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
                StatusText.Text = "Ollama: Disconnected";
                ModelsText.Text = "Models: -";

                if (!string.IsNullOrEmpty(error))
                {
                    ErrorText.Text = $"Error: {error}";
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
                StatusText.Text = "Ollama: Checking...";
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
