using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Core;


namespace Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : BaseDialog
    {
        private string _status;
        private bool _isInstallerMode;
        private string _version;
        private string? _installDirectory;
        private string _subTitle = "Please wait...";
        private bool _installing;
        private bool _canClose = true;
        private string _repoOwner =Constants.RepoOwner;
        private string _repoName = Constants.RepoName;
        private GitHubRelease _selectedRelease;
        private bool _containsCudaRelease;

        #region Properties
        public Visibility InstallerVisibility => IsInstallerMode ? Visibility.Visible : Visibility.Collapsed;
        public bool IsInstallerMode
        {
            get => _isInstallerMode;
            set
            {
                if (SetField(ref _isInstallerMode, value))
                {
                    OnPropertyChanged(nameof(InstallerVisibility));
                }
            }
        }

        public bool ContainsCudaRelease
        {
            get => _containsCudaRelease;
            set => SetField(ref _containsCudaRelease, value);
        }

        public ObservableCollection<GitHubRelease> Releases { get; set; } = new();

        public GitHubRelease SelectedRelease
        {
            get => _selectedRelease;
            set
            {
                if (SetField(ref _selectedRelease, value))
                {
                    CheckCudaAvailability();
                }
            }
        }

        public bool CanClose
        {
            get => _canClose;
            set => SetField(ref _canClose, value);
        }

        public bool Installing
        {
            get => _installing;
            set => SetField(ref _installing, value);
        }

        public string SubTitle
        {
            get => _subTitle;
            set => SetField(ref _subTitle, value);
        }

        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        public string Version
        {
            get => _version;
            set => SetField(ref _version, value);
        }

        public string InstallDirectory
        {
            get => _installDirectory ?? Path.GetDirectoryName(Environment.ProcessPath);
            set => SetField(ref _installDirectory, value);
        }

        #endregion


        public MainWindow()
        {
            Title = "AI-M";
            InitializeComponent();
            
            DataContext = this;
            Task.Delay(400).ContinueWith(_ => Execute());
        }
        private void CheckCudaAvailability()
        {
            ContainsCudaRelease = SelectedRelease?.Assets.Any(a => a.Name.Contains("_cuda.zip")) == true;
        }

        private async Task Execute()
        {
            Status = "Search executable...";
            await Task.Delay(100);
            var exe = ExecutableManager.FindExecutable();
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                IsInstallerMode = true;
                SubTitle = "Install";
                Status = string.Empty;
                LoadReleases();
            }
            else
            {
                Version = ExecutableManager.LoadResourceTable(exe, s => Status = s)?.FileVersion;
                await Task.Delay(200);
                await RenameExe(exe);
            }
        }

        private async void LoadReleases()
        {
            await ReleaseManager.LoadReleasesAsync(Releases);
            SelectedRelease = Releases.FirstOrDefault();
        }


        private async Task RenameExe(string exe)
        {
            await ExecutableManager.RenameExecutable(exe, newName =>
            {
                Status = $"Shuffle name to {newName}";
            });

            await Dispatcher.Invoke(() =>
            {
                Close();
                System.Windows.Application.Current.Shutdown();
                return Task.CompletedTask;
            });
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Minimize_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            var useCuda = ContainsCudaRelease && CudaCheckBox.IsChecked == true;
            Installing = true;
            CanClose = false;
            FolderSelect.Visibility = Visibility.Collapsed;
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;
            Status = "Installing (Check and create Directory)...";
            var dir = InstallDirectory;
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                await Task.Delay(500);
                Status = "Checking for latest version...";
                if (string.IsNullOrEmpty(SelectedRelease?.DownloadUrl))
                {
                    Status = "Can not install";
                    return;
                }
                var installer = new UpdateManager(dir);
                installer.SetRelease(SelectedRelease, useCuda);
                ProgressBar.IsIndeterminate = false;

                await installer.DoUpdate(new Progress<double>(p =>
                {
                    ProgressBar.Value = p;
                    Status = $"Downloading... {p:0.00}%";
                }));

            }
            catch (Exception exception)
            {
                Status = $"Error: {exception.Message}";
                return;
            }
            finally
            {
                Installing = false;
            }
        }

        private void SelectDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            dlg.InitialDirectory = InstallDirectory;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                InstallDirectory = dlg.SelectedPath;
            }
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            e.Cancel = !CanClose;
        }
    }
}