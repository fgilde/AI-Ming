﻿using System.IO;
using System.Windows;
using System.Windows.Controls;
using Aimmy2.Config;
using Visuality;
using Aimmy2;
using Core;
using Aimmy2.Extensions;
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
                MessageBox.Show($"Error creating a required directory: {ex}");
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
