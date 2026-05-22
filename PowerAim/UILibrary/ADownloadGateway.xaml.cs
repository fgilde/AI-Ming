using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Visuality;

namespace PowerAim.UILibrary
{
    /// <summary>
    /// Interaction logic for ADownloadGateway.xaml
    /// </summary>
    public partial class ADownloadGateway : UserControl
    {
        private static readonly HttpClient httpClient = new();

        private readonly string? _downloadUrl;

        public ADownloadGateway(string Name, string Path, string? downloadUrl = null)
        {
            InitializeComponent();
            Title.Content = Name;
            _downloadUrl = downloadUrl;

            DownloadButton.Click += async (s, e) =>
            {
                if ((string)DownloadButton.Content == "\xE895") return;

                DownloadButton.Content = "\xE895";
                SetupHttpClientHeaders();

                bool downloadResult;
                if (!string.IsNullOrEmpty(_downloadUrl))
                {
                    // Use the resolved URL from the merge (may point at upstream repo).
                    downloadResult = await DownloadFileAsync(new Uri(_downloadUrl), Path, Name);
                }
                else
                {
                    // Legacy path — fall back to the fork's main branch.
                    downloadResult = await DownloadAsync(Name, Path);
                }

                if (downloadResult)
                {
                    new NoticeBar("The download has been completed.", 4000).Show();
                    RemoveFromParent();
                }
                else
                {
                    DownloadButton.Content = "\xE896"; // Consider resetting this in both cases for consistency
                }
            };
        }

        /// <summary>
        /// Legacy static fallback that downloads from the fork's main branch. Kept for callers
        /// (e.g. context-menu shortcuts in MainWindow) that don't have a resolved
        /// <c>download_url</c> at hand.
        /// </summary>
        internal static async Task<bool> DownloadAsync(string Name, string Path)
        {
            var downloadUri = new Uri($"https://github.com/{ApplicationConstants.RepoOwner}/{ApplicationConstants.RepoName}/raw/main/{Path}/{Name}");
            var downloadResult = await DownloadFileAsync(downloadUri, Path, Name);
            return downloadResult;
        }

        private static void SetupHttpClientHeaders()
        {
            if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "PowerAim");
            }
            if (!httpClient.DefaultRequestHeaders.Contains("Accept"))
            {
                httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            }
        }

        private static async Task<bool> DownloadFileAsync(Uri uri, string path, string name)
        {
            var response = await httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                new NoticeBar($"Download Failed, {response.StatusCode}, {response.ReasonPhrase}", 4000).Show();
                return false;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var filePath = Path.Combine("bin", path, name);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)); // just in case
            await File.WriteAllBytesAsync(filePath, content);
            return true;
        }

        private void RemoveFromParent() // lol
        {
            if (Parent is StackPanel stackPanel)
            {
                Application.Current.Dispatcher.Invoke(() => stackPanel.Children.Remove(this));
            }
        }
    }
}
