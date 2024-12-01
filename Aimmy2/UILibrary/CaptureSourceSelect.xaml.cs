using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Forms;
using Accord.Math;
using Aimmy2.AILogic;
using Aimmy2.Config;
using Class;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Aimmy2.Models;
using Visuality;
using System.Threading;
using System.Windows.Media;
using Aimmy2.Extensions;
using Nextended.Core.Helper;
using Nextended.Core.Types;
using Nextended.UI;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Aimmy2.Class.Native;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ACredit.xaml
    /// </summary>
    public partial class CaptureSourceSelect : INotifyPropertyChanged, IDisposable
    {
        private ProcessWatcher? _processWatcher;
        public CaptureSource CaptureSource
        {
            get => (CaptureSource)GetValue(CaptureSourceProperty);
            set => SetValue(CaptureSourceProperty, value);
        }

        public static readonly DependencyProperty CaptureSourceProperty =
            DependencyProperty.Register(nameof(CaptureSource), typeof(CaptureSource), typeof(CaptureSourceSelect), new PropertyMetadata(AppConfig.Current.CaptureSource, SourceChanged));

        private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var css = d as CaptureSourceSelect;
            css?.CheckProcess();
        }
        
        private ImageSource _capturePreview;

        public Brush ScreenForeground => CaptureSource.TargetType == CaptureTargetType.Screen ? Brushes.Green : Brushes.White;
        public Brush ProcessForeground => IsProcess ? IsValidProcess ? Brushes.Green : Brushes.Red : Brushes.White;

        public bool IsProcess => CaptureSource?.TargetType == CaptureTargetType.Process;
        public bool IsScreen => CaptureSource?.TargetType == CaptureTargetType.Screen;
        public bool IsValidProcess => IsProcess && (ProcessModel.FindProcessById(CaptureSource.ProcessOrScreenId ?? 0) ?? ProcessModel.FindProcessByTitle(CaptureSource.Title)) != null;

        public event EventHandler<CaptureSource> Selected;

        public ImageSource CapturePreview
        {
            get => _capturePreview;
            private set => SetField(ref _capturePreview, value);
        }

        public CaptureSourceSelect()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CheckProcess();
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.PropertyChanged += async (sender, args) =>
                {
                    if (args.PropertyName == nameof(MainWindow.IsModelLoaded) && !MainWindow.Instance.IsModelLoaded)
                    {
                        await Task.Delay(600);
                        Dispatcher.Invoke(() =>
                        {
                            OnPropertyChanged(nameof(ProcessForeground));
                            OnPropertyChanged(nameof(ScreenForeground));
                            CheckProcess();
                        });
                    }
                };
            }
        }

        private void UpdatePreview()
        {
            try
            {
                var capture = CaptureSource?.Capture();
                CapturePreview = capture.ToImageSource();
            }
            catch{
                Console.WriteLine("Error updating preview");
            }
        }

        private void CheckProcess()
        {
            if(IsProcess && !IsValidProcess)
            {
                WaitForProcess(CaptureSource.Title);
            }
            else
            {
                _processWatcher?.Stop();
                _processWatcher = null;
            }
        }

        private void WaitForProcess(string title)
        {
            if(_processWatcher == null)
            {
                _processWatcher = new ProcessWatcher();
                _processWatcher.NewProcessesStarted += OnNewProcessesStarted;
                _processWatcher.Start();
            }
        }

        private void OnNewProcessesStarted(object? sender, List<SmallProcessInfo> e)
        {
            Dispatcher.Invoke(() =>
            {
                var pm = ProcessModel.FindProcessByTitle(CaptureSource.Title);
                if (pm != null)
                {
                    OnSelect(pm);
                }
            });
        }


        private void ProcessBtnClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;

            btn.ContextMenu = new ContextMenu();
            btn.ContextMenu.Placement = PlacementMode.Bottom;
            btn.ContextMenu.PlacementTarget = btn;
            var primary = new MenuItem { Header = "Select Application..." };
            primary.Click += (o, args) => OnSelect();
            btn.ContextMenu.Items.Add(primary);
            btn.ContextMenu.Items.Add(new Separator());
            foreach (var process in NativeAPIMethods.RecordableProcesses())
            {
                var menuItem = new MenuItem() { Header = process.MainWindowTitle };
                menuItem.IsCheckable = true;
                menuItem.IsChecked = CaptureSource.TargetType == CaptureTargetType.Process && (process.Id == CaptureSource.ProcessOrScreenId);
                menuItem.Click += (o, args) => OnSelect(process);
                btn.ContextMenu.Items.Add(menuItem);
            }
            btn.ContextMenu.IsOpen = true;

        }

        private void MonitorBtnClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;

            btn.ContextMenu = new ContextMenu();
            btn.ContextMenu.Placement = PlacementMode.Bottom;
            btn.ContextMenu.PlacementTarget = btn;
            var primary = new MenuItem { Header = "Use Primary Monitor" };
            primary.Click += (o, args) => OnSelect(Screen.PrimaryScreen);
            btn.ContextMenu.Items.Add(primary);
            btn.ContextMenu.Items.Add(new Separator());
            foreach (var monitor in Screen.AllScreens)
            {
                var menuItem = new MenuItem { Header = monitor.DeviceFriendlyName() };
                menuItem.IsCheckable = true;
                menuItem.IsChecked = CaptureSource.TargetType == CaptureTargetType.Screen && ((CaptureSource.ProcessOrScreenId == null && Equals(monitor, Screen.PrimaryScreen)) || CaptureSource.ProcessOrScreenId == Screen.AllScreens.IndexOf(monitor));
                menuItem.Click += (o, args) => OnSelect(monitor);
                btn.ContextMenu.Items.Add(menuItem);
            }

            btn.ContextMenu.IsOpen = true;

        }


        private async void OnSelect()
        {
            var processDialog = new ProcessPickerDialog();
            if (processDialog.ShowDialog() == true)
            {
                var selectedProcess = processDialog.SelectedProcess;
                if (selectedProcess != null)
                {
                    OnSelect(selectedProcess);
                }
            }
        }

        private void OnSelect(Process process)
        {
            _processWatcher?.Stop();
            _processWatcher = null;
            CaptureSource = AILogic.CaptureSource.Process(process);
            AppConfig.Current.CaptureSource = CaptureSource;
            Selected?.Invoke(this, CaptureSource);
            OnPropertyChanged(nameof(ProcessForeground));
            OnPropertyChanged(nameof(ScreenForeground));
        }

        private void OnSelect(Screen monitor)
        {
            _processWatcher?.Stop();
            _processWatcher = null;
            CaptureSource = AILogic.CaptureSource.Screen(monitor);
            AppConfig.Current.CaptureSource = CaptureSource;
            Selected?.Invoke(this, CaptureSource);
            OnPropertyChanged(nameof(ProcessForeground));
            OnPropertyChanged(nameof(ScreenForeground));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void ToolTip_OnOpened(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        public void Dispose()
        {
            _processWatcher?.Dispose();
        }
    }

}
