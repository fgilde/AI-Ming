using System.IO;
using System.Windows;
using System.Windows.Controls;
using PowerAim.Config;
using Visuality;
using PowerAim;
using Core;
using PowerAim.Extensions;
using Nextended.Core.Extensions;

namespace Other
{
    internal class FileManager : IDisposable
    {
        public FileSystemWatcher? ModelFileWatcher;
        public FileSystemWatcher? ConfigFileWatcher;

        private ListBox ModelListBox;
        private Label SelectedModelNotifier;

        private ListBox ConfigListBox;
        private Label SelectedConfigNotifier;

        public bool InQuittingState = false;

        //private DetectedPlayerWindow DetectedPlayerOverlay;
        //private FOV FOVWindow;

        public static AIManager? AIManager;

        public FileManager(ListBox modelListBox, Label selectedModelNotifier, ListBox configListBox, Label selectedConfigNotifier)
        {
            ModelListBox = modelListBox;
            SelectedModelNotifier = selectedModelNotifier;

            ConfigListBox = configListBox;
            SelectedConfigNotifier = selectedConfigNotifier;

            ModelListBox.SelectionChanged += ModelListBox_SelectionChanged;
            ConfigListBox.SelectionChanged += ConfigListBox_SelectionChanged;

            CheckForRequiredFolders();
            InitializeFileWatchers();
            LoadModelsIntoListBox(null, null);
            LoadConfigsIntoListBox(null, null);
        }

        private void CheckForRequiredFolders()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] dirs = { "bin\\models", "bin\\images", "bin\\labels", "bin\\configs", "bin\\anti_recoil_configs" };

            try
            {
                foreach (string dir in dirs)
                {
                    string fullPath = Path.Combine(baseDir, dir);
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                PowerAim.Visuality.MessageDialog.Show($"Error creating a required directory: {ex}", icon: PowerAim.Visuality.MessageDialog.DialogIcon.Error);
                Application.Current.Shutdown();
            }
        }

        public static bool CurrentlyLoadingModel = false;

        private async void ModelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelListBox.SelectedItem == null) return;

            string selectedModel = ModelListBox.SelectedItem.ToString()!;

            string modelPath = Path.Combine("bin/models", selectedModel);

            await LoadModel(selectedModel, modelPath);
        }

        public async Task LoadModel(string selectedModel, string modelPath)
        {
            // Check if the model is already selected or currently loading
            if (CurrentlyLoadingModel) return;

            CurrentlyLoadingModel = true;
            AppConfig.Current.LastLoadedModel = selectedModel;
            AppConfig.Current.OnPropertyChanged(nameof(AppConfig.Current.LastLoadedModel));

            // Store original values and disable them temporarily
            var toggleKeys = new[] { "Aim Assist", "Constant AI Tracking", "Auto Trigger", "Show Detected Player", "Show AI Confidence", "Show Tracers" };
            var originalToggleStates = toggleKeys.ToDictionary(key => key, key => AppConfig.Current.ToggleState[key]);
            foreach (var key in toggleKeys)
            {
                AppConfig.Current.ToggleState[key] = false;
            }

            // Let the AI finish up
            await Task.Delay(150);


            AIManager?.Dispose();
            AIManager = new AIManager(modelPath, AppConfig.Current.CaptureSource);


            // TODO: Remove reflection
            // Restore original values
            foreach (var keyValuePair in originalToggleStates)
            {
                AppConfig.Current.ToggleState[keyValuePair.Key] = keyValuePair.Value;
            }

            string content = Locale.LoadedModel.FormatWith(selectedModel);
            SelectedModelNotifier.Content = content;
            new NoticeBar(content, 2000).Show();
        }

        private void ConfigListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigListBox.SelectedItem == null) return;
            string selectedConfig = ConfigListBox.SelectedItem.ToString()!;

            string configPath = Path.Combine(Path.GetDirectoryName(AppConfig.DefaultConfigPath), selectedConfig);
            MainWindow.Instance.LoadConfig(configPath);
            SelectedConfigNotifier.Content = Locale.LoadedConfig.FormatWith(selectedConfig);
        }

        public void InitializeFileWatchers()
        {
            ModelFileWatcher = new FileSystemWatcher();
            ConfigFileWatcher = new FileSystemWatcher();

            InitializeWatcher(ref ModelFileWatcher, "bin/models", "*.onnx");
            InitializeWatcher(ref ConfigFileWatcher, "bin/configs", "*.cfg");
        }

        private void InitializeWatcher(ref FileSystemWatcher watcher, string path, string filter)
        {
            watcher.Path = path;
            watcher.Filter = filter;
            watcher.EnableRaisingEvents = true;

            if (filter == "*.onnx")
            {
                watcher.Changed -= LoadModelsIntoListBox;
                watcher.Created -= LoadModelsIntoListBox;
                watcher.Deleted -= LoadModelsIntoListBox;
                watcher.Renamed -= LoadModelsIntoListBox;

                watcher.Changed += LoadModelsIntoListBox;
                watcher.Created += LoadModelsIntoListBox;
                watcher.Deleted += LoadModelsIntoListBox;
                watcher.Renamed += LoadModelsIntoListBox;
            }
            else if (filter == "*.cfg")
            {
                watcher.Changed -= LoadConfigsIntoListBox;
                watcher.Created -= LoadConfigsIntoListBox;
                watcher.Deleted -= LoadConfigsIntoListBox;
                watcher.Renamed -= LoadConfigsIntoListBox;

                watcher.Changed += LoadConfigsIntoListBox;
                watcher.Created += LoadConfigsIntoListBox;
                watcher.Deleted += LoadConfigsIntoListBox;
                watcher.Renamed += LoadConfigsIntoListBox;
            }
        }

        public void LoadModelsIntoListBox(object? sender, FileSystemEventArgs? e)
        {
            if (!InQuittingState)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string[] onnxFiles = Directory.GetFiles("bin/models", "*.onnx");
                    ModelListBox.Items.Clear();

                    foreach (string filePath in onnxFiles)
                    {
                        ModelListBox.Items.Add(Path.GetFileName(filePath));
                    }

                    if (ModelListBox.Items.Count > 0)
                    {
                        string? lastLoadedModel = AppConfig.Current.LastLoadedModel;
                        if (lastLoadedModel != "N/A" && !ModelListBox.Items.Contains(lastLoadedModel)) { ModelListBox.SelectedItem = lastLoadedModel; }
                        SelectedModelNotifier.Content = Locale.LoadedModel.FormatWith(lastLoadedModel);
                    }
                    ModelListBox.EnsureRenderedAndInitialized();
                    MainWindow.Instance.FillMenus();
                });
            }
        }

        public void LoadConfigsIntoListBox(object? sender, FileSystemEventArgs? e)
        {
            if (!InQuittingState)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string[] configFiles = Directory.GetFiles("bin/configs", "*.cfg");
                    ConfigListBox.Items.Clear();

                    foreach (string filePath in configFiles)
                    {
                        ConfigListBox.Items.Add(Path.GetFileName(filePath));
                    }

                    if (ConfigListBox.Items.Count > 0)
                    {
                        string? lastLoadedConfig = AppConfig.Current.LastLoadedConfig;
                        if (lastLoadedConfig != "N/A" && !ConfigListBox.Items.Contains(lastLoadedConfig)) { ConfigListBox.SelectedItem = lastLoadedConfig; }

                        SelectedConfigNotifier.Content = "Loaded Config: " + lastLoadedConfig;
                    }
                    ConfigListBox.EnsureRenderedAndInitialized();
                    MainWindow.Instance.FillMenus();
                });
            }
        }

        public static async Task<HashSet<string>> RetrieveAndAddFiles(string repoLink, string localPath, HashSet<string> allFiles)
        {
            try
            {
                GithubManager githubManager = new();

                var files = await githubManager.FetchGithubFilesAsync(repoLink);

                foreach (var file in files)
                {
                    if (file == null) continue;

                    if (!allFiles.Contains(file) && !File.Exists(Path.Combine(localPath, file)))
                    {
                        allFiles.Add(file);
                    }
                }

                githubManager.Dispose();

                return allFiles;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        /// <summary>
        /// Fetches the same logical directory (e.g. <c>"models"</c>) from multiple GitHub repos
        /// in parallel and merges the results by filename. When two repos expose the same filename
        /// the <b>fork wins</b> — the fork is the <em>first</em> entry in <paramref name="repos"/>.
        /// (Previously the newer commit date won, but that cost one GitHub commits-API call per
        /// duplicate file and routinely tripped the anonymous rate limit; fork-wins needs none.)
        /// Files that already exist locally under <paramref name="localPath"/> are skipped (they
        /// are not surfaced as downloadable). The returned dictionary is the same instance as
        /// <paramref name="allFiles"/>, keyed by filename.
        /// </summary>
        public static async Task<Dictionary<string, GitHubFile>> RetrieveAndMergeFromRepos(
            IReadOnlyList<(string owner, string repo, string subPath)> repos,
            string localPath,
            Dictionary<string, GitHubFile> allFiles)
        {
            if (repos == null || repos.Count == 0) return allFiles;

            using var githubManager = new GithubManager();

            // Parallel fetch of all listings. We tag each result with its source index so we
            // can later identify which entry is the "fork" (index 0) for tie-break purposes.
            var listingTasks = repos
                .Select((r, idx) => FetchSafeAsync(githubManager, r.owner, r.repo, r.subPath, idx))
                .ToArray();
            var listings = await Task.WhenAll(listingTasks);

            // Group by filename across all repos.
            var grouped = listings
                .SelectMany(l => l.Files.Select(f => (Index: l.Index, File: f)))
                .GroupBy(x => x.File.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                var fileName = group.Key;

                // Skip anything we already have locally.
                if (File.Exists(Path.Combine(localPath, fileName))) continue;

                // Fork wins on duplicate filenames (the fork is index 0 — the curated source).
                // This deliberately drops the previous per-duplicate commits-API lookup
                // (GetLatestCommitDateAsync): with models present in both repos it fired one extra
                // GitHub API call PER duplicate file, which was the main cause of hitting the
                // anonymous 60-req/hour rate limit. Fork-wins needs zero extra calls.
                var winner = group.OrderBy(x => x.Index).First();
                allFiles[fileName] = winner.File;
            }

            return allFiles;
        }

        private static async Task<(int Index, IReadOnlyList<GitHubFile> Files)> FetchSafeAsync(
            GithubManager githubManager, string owner, string repo, string subPath, int index)
        {
            try
            {
                var files = await githubManager.FetchGithubFilesDetailedAsync(owner, repo, subPath);
                return (index, files.ToList());
            }
            catch
            {
                // A single repo failure must not break the merge — fall back to an empty list
                // for that source. Other repos still contribute their files.
                return (index, Array.Empty<GitHubFile>());
            }
        }

        public void Dispose()
        {
            InQuittingState = true;
            if (ConfigFileWatcher != null)
            {
                ConfigFileWatcher.EnableRaisingEvents = false;
                ConfigFileWatcher.Changed -= LoadModelsIntoListBox;
                ConfigFileWatcher.Created -= LoadModelsIntoListBox;
                ConfigFileWatcher.Deleted -= LoadModelsIntoListBox;
                ConfigFileWatcher.Renamed -= LoadModelsIntoListBox;
            }

            if (ModelFileWatcher != null)
            {
                ModelFileWatcher.EnableRaisingEvents = false;
                ModelFileWatcher.Changed -= LoadConfigsIntoListBox;
                ModelFileWatcher.Created -= LoadConfigsIntoListBox;
                ModelFileWatcher.Deleted -= LoadConfigsIntoListBox;
                ModelFileWatcher.Renamed -= LoadConfigsIntoListBox;
            }

            ConfigFileWatcher?.Dispose();
            ModelFileWatcher?.Dispose();
        }
    }
}
