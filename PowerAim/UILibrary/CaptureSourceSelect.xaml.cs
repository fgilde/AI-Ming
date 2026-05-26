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
using PowerAim.AILogic;
using PowerAim.Config;
using Class;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using PowerAim.Models;
using Visuality;
using System.Threading;
using System.Windows.Media;
using PowerAim.Extensions;
using Nextended.Core.Helper;
using Nextended.Core.Types;
using Nextended.UI;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using PowerAim.Class.Native;
using System.Runtime.InteropServices;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using PowerAim;

namespace PowerAim.UILibrary
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
        
        public Brush ScreenForeground => CaptureSource.TargetType == CaptureTargetType.Screen ? Brushes.Green : Brushes.White;
        public Brush ProcessForeground => IsProcess ? IsValidProcess ? Brushes.Green : Brushes.Red : Brushes.White;

        public bool IsProcess => CaptureSource?.TargetType == CaptureTargetType.Process;
        public bool IsScreen => CaptureSource?.TargetType == CaptureTargetType.Screen;
        public bool IsValidProcess => IsProcess && (ProcessModel.FindProcessById(CaptureSource.ProcessOrScreenId ?? 0) ?? ProcessModel.FindProcessByTitle(CaptureSource.Title)) != null;

        public event EventHandler<CaptureSource> Selected;

        public ImageSource CapturePreview
        {
            get;
            private set => SetField(ref field, value);
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
                // Use the same reliable thumbnail path that the picker tiles use — CaptureSource.Capture()
                // sometimes returns null/garbage when invoked outside of an active inference loop.
                if (CaptureSource == null)
                {
                    CapturePreview = null!;
                    return;
                }

                BitmapSource? thumb = null;
                if (CaptureSource.TargetType == CaptureTargetType.Screen)
                {
                    var screens = Screen.AllScreens;
                    var idx = CaptureSource.ProcessOrScreenId ?? -1;
                    var monitor = (idx >= 0 && idx < screens.Length) ? screens[idx] : Screen.PrimaryScreen;
                    if (monitor != null) thumb = CaptureScreenThumbnail(monitor);
                }
                else if (CaptureSource.TargetType == CaptureTargetType.Process)
                {
                    var pid = CaptureSource.ProcessOrScreenId ?? 0;
                    var proc = pid > 0 ? ProcessModel.FindProcessById(pid) : ProcessModel.FindProcessByTitle(CaptureSource.Title);
                    if (proc != null && proc.MainWindowHandle != IntPtr.Zero)
                        thumb = CaptureWindowThumbnail(proc.MainWindowHandle);
                }

                if (thumb != null) CapturePreview = thumb;
                else
                {
                    // Fall back to the legacy path only if the reliable one yielded nothing.
                    var capture = CaptureSource.Capture();
                    if (capture != null) CapturePreview = capture.ToImageSource();
                }
            }
            catch
            {
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
                _processWatcher = new();
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


        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        private const uint PW_RENDERFULLCONTENT = 0x00000002;

        private Popup? _processPopup;

        private void ProcessBtnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            CloseProcessPopup();

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 6, 0, 0),
                MaxHeight = 560
            };
            card.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
            card.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
            card.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.3 };

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var header = new TextBlock
            {
                Text = Locale.SelectCaptureWindow,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(2, 0, 0, 8),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
            headerRow.Children.Add(header);

            var browseBtn = new Button
            {
                Content = Locale.BrowseAll,
                MinWidth = 110,
                Margin = new Thickness(0, 0, 2, 8)
            };
            browseBtn.SetResourceReference(FrameworkElement.StyleProperty, "FluentStandardButton");
            browseBtn.Click += (_, _) => { CloseProcessPopup(); OnSelect(); };
            Grid.SetColumn(browseBtn, 1);
            headerRow.Children.Add(browseBtn);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var processes = NativeAPIMethods.RecordableProcesses().Take(40).ToList();
            var rows = (processes.Count + 1) / 2;
            for (var r = 0; r < rows; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var i = 0; i < processes.Count; i++)
            {
                var process = processes[i];
                var isSelected = CaptureSource.TargetType == CaptureTargetType.Process && process.Id == CaptureSource.ProcessOrScreenId;
                var tile = BuildProcessTile(process, isSelected);
                tile.Click += (_, _) =>
                {
                    OnSelect(process);
                    CloseProcessPopup();
                };
                Grid.SetColumn(tile, i % 2);
                Grid.SetRow(tile, i / 2);
                grid.Children.Add(tile);
            }

            var scroll = new ScrollViewer
            {
                Content = grid,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 480
            };

            var outer = new StackPanel();
            outer.Children.Add(headerRow);
            outer.Children.Add(scroll);
            card.Child = outer;

            _processPopup = new Popup
            {
                Placement = PlacementMode.Bottom,
                PlacementTarget = btn,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = card,
                IsOpen = true
            };
            _processPopup.Closed += (_, _) => { _processPopup = null; PowerAim.Visuality.CaptureHighlightOverlay.HideOverlay(); };
        }

        private void CloseProcessPopup()
        {
            if (_processPopup != null)
            {
                _processPopup.IsOpen = false;
                _processPopup = null;
            }
        }

        private Button BuildProcessTile(Process process, bool isSelected)
        {
            const double TileWidth = 240;
            const double ThumbHeight = 135;

            var thumb = CaptureWindowThumbnail(process.MainWindowHandle);

            var image = new System.Windows.Controls.Image
            {
                Source = thumb,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            var imageHost = new Border
            {
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Height = ThumbHeight,
                Child = image
            };
            imageHost.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");

            // Fallback: show app icon if no thumbnail
            if (thumb == null)
            {
                var fallback = new TextBlock
                {
                    Text = "",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                    FontSize = 42,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                fallback.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextTertiary");
                imageHost.Child = fallback;
            }

            var titleRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameLabel = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(process.MainWindowTitle) ? process.ProcessName : process.MainWindowTitle,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
            Grid.SetColumn(nameLabel, 0);
            titleRow.Children.Add(nameLabel);

            if (isSelected)
            {
                var check = new TextBlock
                {
                    Text = "",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                    FontSize = 12,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                check.SetResourceReference(TextBlock.ForegroundProperty, "FluentAccent");
                Grid.SetColumn(check, 1);
                titleRow.Children.Add(check);
            }

            var procLabel = new TextBlock
            {
                Text = process.ProcessName,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            procLabel.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");

            var pidLabel = new TextBlock
            {
                Text = string.Format(Locale.PidFormat, process.Id),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Small"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            pidLabel.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextTertiary");

            var contentStack = new StackPanel();
            contentStack.Children.Add(imageHost);
            contentStack.Children.Add(titleRow);
            contentStack.Children.Add(procLabel);
            contentStack.Children.Add(pidLabel);

            var tile = new Button
            {
                Content = contentStack,
                Width = TileWidth,
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalContentAlignment = System.Windows.VerticalAlignment.Stretch
            };
            tile.SetResourceReference(FrameworkElement.StyleProperty, "FluentSubtleButton");
            tile.SetResourceReference(Button.BackgroundProperty, "FluentSurface3");
            tile.SetResourceReference(Button.BorderBrushProperty, isSelected ? "FluentAccent" : "FluentStroke");
            tile.SetResourceReference(Button.ForegroundProperty, "FluentTextPrimary");
            tile.ToolTip = process.MainWindowTitle;


            tile.MouseEnter += (_, _) => PowerAim.Visuality.CaptureHighlightOverlay.ShowFor(process.MainWindowHandle);
            tile.MouseLeave += (_, _) => PowerAim.Visuality.CaptureHighlightOverlay.HideOverlay();
            return tile;
        }

        private BitmapSource? CaptureWindowThumbnail(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;
            try
            {
                if (!GetWindowRect(hwnd, out var rect)) return null;
                int w = rect.right - rect.left;
                int h = rect.bottom - rect.top;
                if (w <= 50 || h <= 50) return null;

                using var bmp = new System.Drawing.Bitmap(w, h);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    var hdc = g.GetHdc();
                    try
                    {
                        if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
                            return null;
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                }
                var hbmp = bmp.GetHbitmap();
                try
                {
                    return Imaging.CreateBitmapSourceFromHBitmap(
                        hbmp,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(320, (int)(320 * (double)h / w)));
                }
                finally
                {
                    DeleteObject(hbmp);
                }
            }
            catch
            {
                return null;
            }
        }

        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

        private Popup? _monitorPopup;

        private void MonitorBtnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            CloseMonitorPopup();

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 6, 0, 0)
            };
            card.SetResourceReference(Border.BackgroundProperty, "FluentSurface2");
            card.SetResourceReference(Border.BorderBrushProperty, "FluentStroke");
            card.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.3 };

            var header = new TextBlock
            {
                Text = Locale.SelectCaptureMonitor,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(2, 0, 0, 8)
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var allScreens = Screen.AllScreens;
            var rows = (allScreens.Length + 1) / 2;
            for (var r = 0; r < rows; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var i = 0; i < allScreens.Length; i++)
            {
                var monitor = allScreens[i];
                var screenIndex = i;
                var isSelected = CaptureSource.TargetType == CaptureTargetType.Screen
                                 && ((CaptureSource.ProcessOrScreenId == null && Equals(monitor, Screen.PrimaryScreen))
                                     || CaptureSource.ProcessOrScreenId == screenIndex);
                var tile = BuildMonitorTile(monitor, isSelected);
                tile.Click += (_, _) =>
                {
                    OnSelect(monitor);
                    CloseMonitorPopup();
                };
                Grid.SetColumn(tile, i % 2);
                Grid.SetRow(tile, i / 2);
                grid.Children.Add(tile);
            }

            var outer = new StackPanel();
            outer.Children.Add(header);
            outer.Children.Add(grid);
            card.Child = outer;

            _monitorPopup = new Popup
            {
                Placement = PlacementMode.Bottom,
                PlacementTarget = btn,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = card,
                IsOpen = true
            };
            _monitorPopup.Closed += (_, _) => { _monitorPopup = null; PowerAim.Visuality.CaptureHighlightOverlay.HideOverlay(); };
        }

        private void CloseMonitorPopup()
        {
            if (_monitorPopup != null)
            {
                _monitorPopup.IsOpen = false;
                _monitorPopup = null;
            }
        }

        private Button BuildMonitorTile(Screen monitor, bool isSelected)
        {
            const double TileWidth = 240;
            const double ThumbHeight = 135; // 16:9

            var thumb = CaptureScreenThumbnail(monitor);

            // Thumbnail with centered aspect-correct image inside FluentSurface3 frame
            var image = new System.Windows.Controls.Image
            {
                Source = thumb,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            var imageHost = new Border
            {
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Height = ThumbHeight,
                Child = image
            };
            imageHost.SetResourceReference(Border.BackgroundProperty, "FluentSurface3");

            // Primary pill badge over the thumbnail
            var thumbHost = new Grid();
            thumbHost.Children.Add(imageHost);
            if (monitor.Primary)
            {
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(0, 8, 8, 0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top
                };
                badge.SetResourceReference(Border.BackgroundProperty, "FluentAccent");
                var badgeText = new TextBlock
                {
                    Text = Locale.Primary,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Small"),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                badgeText.SetResourceReference(TextBlock.ForegroundProperty, "FluentAccentForeground");
                badge.Child = badgeText;
                thumbHost.Children.Add(badge);
            }

            // Title row: monitor name + check icon if selected
            var titleRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameLabel = new TextBlock
            {
                Text = monitor.DeviceFriendlyName(),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextPrimary");
            Grid.SetColumn(nameLabel, 0);
            titleRow.Children.Add(nameLabel);

            if (isSelected)
            {
                var check = new TextBlock
                {
                    Text = "",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                    FontSize = 12,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                check.SetResourceReference(TextBlock.ForegroundProperty, "FluentAccent");
                Grid.SetColumn(check, 1);
                titleRow.Children.Add(check);
            }

            var resLabel = new TextBlock
            {
                Text = $"{monitor.Bounds.Width} × {monitor.Bounds.Height}",
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0)
            };
            resLabel.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextSecondary");

            var posLabel = new TextBlock
            {
                Text = string.Format(Locale.PositionFormat, monitor.Bounds.X, monitor.Bounds.Y),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Small"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            posLabel.SetResourceReference(TextBlock.ForegroundProperty, "FluentTextTertiary");

            var contentStack = new StackPanel();
            contentStack.Children.Add(thumbHost);
            contentStack.Children.Add(titleRow);
            contentStack.Children.Add(resLabel);
            contentStack.Children.Add(posLabel);

            var tile = new Button
            {
                Content = contentStack,
                Width = TileWidth,
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalContentAlignment = System.Windows.VerticalAlignment.Stretch
            };
            tile.SetResourceReference(FrameworkElement.StyleProperty, "FluentSubtleButton");
            tile.SetResourceReference(Button.BackgroundProperty, "FluentSurface3");
            tile.SetResourceReference(Button.BorderBrushProperty, isSelected ? "FluentAccent" : "FluentStroke");
            tile.SetResourceReference(Button.ForegroundProperty, "FluentTextPrimary");

            // Live highlight on the actual monitor while hovering this tile.
            tile.MouseEnter += (_, _) => PowerAim.Visuality.CaptureHighlightOverlay.ShowFor(monitor);
            tile.MouseLeave += (_, _) => PowerAim.Visuality.CaptureHighlightOverlay.HideOverlay();
            return tile;
        }

        private BitmapSource? CaptureScreenThumbnail(Screen monitor)
        {
            try
            {
                var bounds = monitor.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0) return null;
                using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, new System.Drawing.Size(bounds.Width, bounds.Height));
                }
                var hbmp = bmp.GetHbitmap();
                try
                {
                    return Imaging.CreateBitmapSourceFromHBitmap(
                        hbmp,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(320, (int)(320 * (double)bounds.Height / bounds.Width)));
                }
                finally
                {
                    DeleteObject(hbmp);
                }
            }
            catch
            {
                return null;
            }
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
