using System.IO;
using System.Windows;
using System.Windows.Controls;
using PowerAim.Config;
using PowerAim.Visuality;
using PowerAim;
using Core;
using PowerAim.Extensions;
using Nextended.Core.Extensions;

namespace PowerAim.Other
{
    internal class FileManager : IDisposable
    {
        public FileSystemWatcher? ModelFileWatcher;
        public FileSystemWatcher? ConfigFileWatcher;

        private readonly ListBox ModelListBox;
        private readonly Label SelectedModelNotifier;

        private readonly ListBox ConfigListBox;
        private readonly Label SelectedConfigNotifier;

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
            string[] dirs = [Constants.ModelsBasePath, Constants.ImagesBasePath, Constants.LabelsBasePath, Constants.ConfigBasePath, Constants.AntiRecoilConfigBasePath];

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

        // Load "generation": each LoadModel call bumps it; a load only applies its result if it's still the
        // latest — so a slower, superseded load (e.g. a still-building TensorRT engine after the user hit
        // "use CUDA") discards its result instead of clobbering the newer one.
        private static int _loadGeneration;
        private static readonly string[] LoadToggleKeys =
            { "Aim Assist", "Constant AI Tracking", "Auto Trigger", "Show Detected Player", "Show AI Confidence", "Show Tracers" };
        // Captured by the FIRST load of a burst, restored by the LAST (winning) one — so concurrent loads
        // don't capture each other's already-disabled toggles and then restore the wrong values.
        private static Dictionary<string, object>? _savedToggleStates;

        private async void ModelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelListBox.SelectedItem == null) return;

            string selectedModel = ModelListBox.SelectedItem.ToString()!;

            string modelPath = Path.Combine(Constants.ModelsBasePath, selectedModel);

            await LoadModel(selectedModel, modelPath);
        }

        public async Task LoadModel(string selectedModel, string modelPath, bool force = false)
        {
            // Normal re-entrancy guard. `force` (the "skip TensorRT, use CUDA" button) bypasses it to run a
            // concurrent load; the generation counter below then makes the NEWEST load win.
            if (CurrentlyLoadingModel && !force) return;
            int gen = System.Threading.Interlocked.Increment(ref _loadGeneration);

            CurrentlyLoadingModel = true;
            AppConfig.Current.LastLoadedModel = selectedModel;
            AppConfig.Current.OnPropertyChanged(nameof(AppConfig.Current.LastLoadedModel));

            // Capture the user's toggle states once per burst, then disable them during the load so nothing
            // fires while the model swaps. The winning (latest-generation) load restores them.
            _savedToggleStates ??= LoadToggleKeys.ToDictionary(key => key, key => AppConfig.Current.ToggleState[key]);
            foreach (var key in LoadToggleKeys)
            {
                AppConfig.Current.ToggleState[key] = false;
            }

            try
            {
                // Let the AI finish up
                await Task.Delay(150);

                // Build off the UI thread — a first-time TensorRT engine build is slow and would otherwise
                // freeze the window during model load.
                var mgr = await global::AIManager.CreateAsync(modelPath, AppConfig.Current.CaptureSource);

                if (gen != _loadGeneration)
                {
                    // A newer load superseded us (e.g. the user hit "use CUDA" while our TensorRT engine was
                    // still building). Discard our result rather than clobbering the newer one.
                    mgr?.Dispose();
                    return;
                }

                AIManager?.Dispose();   // drop the previously-active manager now that the new one is ready
                AIManager = mgr;

                // The session may have failed to load at the ONNX level (all providers fell through) —
                // CreateAsync still returns an AIManager, but IsModelLoaded is false. Surface the real
                // reason instead of a fake "loaded" toast.
                var loadError = AIManager?.PredictionLogic?.LoadError;
                if (!string.IsNullOrEmpty(loadError))
                {
                    SelectedModelNotifier.Content = Locale.ModelLoadFailed;
                    new NoticeBar(string.Format(Locale.ErrorStartingModelFormat, loadError), 6000).Show();
                }
                else
                {
                    string content = Locale.LoadedModel.FormatWith(selectedModel);
                    SelectedModelNotifier.Content = content;
                    new NoticeBar(content, 2000).Show();
                }
            }
            catch (Exception ex)
            {
                // A hard failure (capture source, session construction, etc.) must NOT leave the UI stuck
                // on "loading" forever — report it (only if we're still the latest load) and let finally
                // clear the loading state.
                if (gen == _loadGeneration)
                {
                    SelectedModelNotifier.Content = Locale.ModelLoadFailed;
                    new NoticeBar(string.Format(Locale.ErrorStartingModelFormat, ex.Message), 6000).Show();
                }
            }
            finally
            {
                // Only the latest generation finalizes — restore toggles, clear the loading flag, refresh
                // the UI. Superseded loads leave all of that to the winner (their finally is a no-op).
                if (gen == _loadGeneration)
                {
                    if (_savedToggleStates != null)
                    {
                        foreach (var kv in _savedToggleStates)
                            AppConfig.Current.ToggleState[kv.Key] = kv.Value;
                        _savedToggleStates = null;
                    }

                    CurrentlyLoadingModel = false;

                    // Raising IsModelLoaded also resolves the empty-state card's "loading…" look
                    // (OnPropertyChanged clears ModelLoadPending) and refreshes the status-strip text.
                    if (Application.Current?.MainWindow is MainWindow mw)
                        mw.CallPropertyChanged(nameof(mw.IsModelLoaded));
                }
            }
        }

        private void ConfigListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigListBox.SelectedItem == null) return;
            string selectedConfig = ConfigListBox.SelectedItem.ToString()!;

            string configPath = Path.Combine(Constants.ConfigBasePath, selectedConfig);
            MainWindow.Instance.LoadConfig(configPath);
            SelectedConfigNotifier.Content = Locale.LoadedConfig.FormatWith(selectedConfig);
        }

        public void InitializeFileWatchers()
        {
            ModelFileWatcher = CreateWatcher(Constants.ModelsBasePath, Constants.ModelFileFilter, LoadModelsIntoListBox);
            ConfigFileWatcher = CreateWatcher(Constants.ConfigBasePath, Constants.ConfigFileFilter, LoadConfigsIntoListBox);
        }

        /// <summary>
        ///     Builds a watcher that re-runs <paramref name="onChange"/> on any create/delete/change/rename
        ///     of matching files. Called once per watcher, so a plain subscribe (no defensive unsubscribe)
        ///     matches the previous behaviour.
        /// </summary>
        private static FileSystemWatcher CreateWatcher(string path, string filter, FileSystemEventHandler onChange)
        {
            var watcher = new FileSystemWatcher { Path = path, Filter = filter, EnableRaisingEvents = true };
            watcher.Changed += onChange;
            watcher.Created += onChange;
            watcher.Deleted += onChange;
            watcher.Renamed += (s, e) => onChange(s, e);
            return watcher;
        }

        public void LoadModelsIntoListBox(object? sender, FileSystemEventArgs? e) =>
            LoadFilesIntoListBox(ModelListBox, Constants.ModelsBasePath, Constants.ModelFileFilter,
                () => AppConfig.Current.LastLoadedModel,
                last => SelectedModelNotifier.Content = Locale.LoadedModel.FormatWith(last));

        public void LoadConfigsIntoListBox(object? sender, FileSystemEventArgs? e) =>
            LoadFilesIntoListBox(ConfigListBox, Constants.ConfigBasePath, Constants.ConfigFileFilter,
                () => AppConfig.Current.LastLoadedConfig,
                last => SelectedConfigNotifier.Content = "Loaded Config: " + last);

        /// <summary>
        ///     Shared list refresh used by both watchers: re-list the <paramref name="filter"/> files
        ///     under <paramref name="dir"/> into <paramref name="listBox"/>, restore the last-loaded
        ///     selection and update the notifier. No-op while quitting; marshals onto the UI thread.
        /// </summary>
        private void LoadFilesIntoListBox(ListBox listBox, string dir, string filter,
            Func<string?> getLastLoaded, Action<string?> updateNotifier)
        {
            if (InQuittingState) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                listBox.Items.Clear();
                foreach (string filePath in Directory.GetFiles(dir, filter))
                    listBox.Items.Add(Path.GetFileName(filePath));

                if (listBox.Items.Count > 0)
                {
                    string? lastLoaded = getLastLoaded();
                    if (lastLoaded != "N/A" && !listBox.Items.Contains(lastLoaded))
                        listBox.SelectedItem = lastLoaded;
                    updateNotifier(lastLoaded);
                }
                listBox.EnsureRenderedAndInitialized();
                MainWindow.Instance.FillMenus();
            });
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
            // Set first so any in-flight watcher callback short-circuits, then stop + dispose both
            // watchers. Disposing the watcher detaches its handlers, so no manual unsubscribe needed.
            InQuittingState = true;

            foreach (var watcher in new[] { ConfigFileWatcher, ModelFileWatcher })
            {
                if (watcher is null) continue;
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
    }
}
