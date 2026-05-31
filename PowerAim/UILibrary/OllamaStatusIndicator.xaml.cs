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
        private readonly System.Windows.Threading.DispatcherTimer _pollTimer;

        public OllamaStatusIndicator()
        {
            InitializeComponent();
            _ollamaClient = new OllamaClient();
            _ollamaClient.StatusChanged += OnStatusChanged;

            // Re-check status every 4s while the control is on screen so that an Ollama server
            // started outside this UI (or via the Start button) is picked up without the user
            // having to mash Refresh. Stopped on Unload to avoid leaking timers on the orphan
            // indicators left behind by CreateUI rebuilds (language change).
            _pollTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _pollTimer.Tick += async (_, _) => await CheckStatusAsync();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateUrlDisplay();
            await CheckStatusAsync();
            _pollTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _pollTimer.Stop();
            _ollamaClient.StatusChanged -= OnStatusChanged;
            // Intentionally not calling _ollamaClient.Dispose() — OllamaClient now uses a shared
            // static HttpClient, and disposing one instance shouldn't break other live callers.
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
                ActionRow.Visibility = Visibility.Collapsed;

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

                // Disconnected → offer a relevant action button. Installed locally? show
                // "Start Ollama". Otherwise show "Install Ollama" which opens the download page.
                bool installed = OllamaClient.IsInstalled;
                StartOllamaButton.Visibility   = installed  ? Visibility.Visible : Visibility.Collapsed;
                InstallOllamaButton.Visibility = !installed ? Visibility.Visible : Visibility.Collapsed;
                ActionRow.Visibility           = Visibility.Visible;
            }
        }

        private async void StartOllama_Click(object sender, RoutedEventArgs e)
        {
            StartOllamaButton.IsEnabled = false;
            StatusText.Text = Locale.OllamaStartingServer;
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00));
            var proc = OllamaClient.TryStartServer();
            if (proc is null)
            {
                StatusText.Text = Locale.OllamaStartFailed;
                StartOllamaButton.IsEnabled = true;
                return;
            }
            // Give the server up to ~5 seconds to come online, then refresh status.
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(500);
                if (await _ollamaClient.IsAvailableAsync()) break;
            }
            await CheckStatusAsync();
            StartOllamaButton.IsEnabled = true;
        }

        private void InstallOllama_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = OllamaClient.DownloadUrl,
                    UseShellExecute = true,
                });
            }
            catch { /* user has no default browser — ignore */ }
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
