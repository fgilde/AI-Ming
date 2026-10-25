using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PowerAim.AI.Contracts;
using PowerAim.Class;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Extensions;
using Nextended.UI.Native;
using Nextended.UI.WPF.Extensions;

namespace PowerAim.UI.Visuality
{
    /// <summary>
    /// FOV guide overlay: layered circle + center ticks, DPI-aware placement.
    /// Capture size remains <see cref="SliderSettings.ActualFovSize"/> (square); this is visual only.
    /// </summary>
    public partial class FOV : Window
    {
        private readonly DispatcherTimer _mousePositionTimer;
        private const double CrossArmFraction = 0.08; // each tick = 8% of FOV radius-ish (diameter-based)
        private const double CrossMinPx = 6;
        private const double CrossMaxPx = 28;

        public static FOV? Instance { get; private set; }

        public FOV()
        {
            Instance = this;
            InitializeComponent();
            Loaded += OnLoaded;
            AppConfig.BindToDataContext(this);

            _mousePositionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 Hz — smoother than 30 ms
            };
            _mousePositionTimer.Tick += OnMousePositionTimerTick;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.MakeClickThrough();
            if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse)
                _mousePositionTimer.Start();

            AppConfig.Current.DropdownState.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(DropdownState.DetectionAreaType)) return;
                if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse)
                    _mousePositionTimer.Start();
                else
                    _mousePositionTimer.Stop();
                PositionCircle();
            };

            // Keep ring size / cross in sync when FOV slider changes
            AppConfig.Current.SliderSettings.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(SliderSettings.ActualFovSize)
                    or nameof(SliderSettings.FOVSize)
                    or nameof(SliderSettings.DynamicFOVSize))
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateCrossGeometry();
                        PositionCircle();
                    }), DispatcherPriority.Render);
                }
            };

            UpdateCrossGeometry();
            PositionCircle();
        }

        private void OnMousePositionTimerTick(object? sender, EventArgs e)
        {
            if (AppConfig.Current.DropdownState.DetectionAreaType != DetectionAreaType.ClosestToMouse)
                return;
            var cursorPosition = NativeMethods.GetCursorPosition();
            PositionCircle(cursorPosition.X, cursorPosition.Y);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.HideForCaptureIfEnabled();
            if (AIManager.Instance?.ImageCapture != null)
                AIManager.Instance.ImageCapture.PropertyChanged += ImageCaptureOnPropertyChanged;
            UpdateCrossGeometry();
            PositionCircle();
        }

        private void ImageCaptureOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ICapture.CaptureArea)) return;
            if (AIManager.Instance?.ImageCapture?.CaptureArea is { } area)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.MoveTo(area);
                    PositionCircle();
                }), DispatcherPriority.Render);
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && AIManager.Instance?.ImageCapture?.CaptureArea is not null)
            {
                this.MoveTo(AIManager.Instance.ImageCapture.CaptureArea);
                UpdateCrossGeometry();
                PositionCircle();
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCrossGeometry();
            PositionCircle();
        }

        private void OnWindowLocationChanged(object? sender, EventArgs e) => PositionCircle();

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (AppConfig.Current.DropdownState.DetectionAreaType != DetectionAreaType.ClosestToMouse)
                return;
            var mousePosition = e.GetPosition(this);
            PositionCircle(mousePosition.X, mousePosition.Y);
        }

        /// <summary>Short center ticks scaled to FOV diameter.</summary>
        private void UpdateCrossGeometry()
        {
            if (FovCanvas == null || CrossH == null || CrossV == null) return;

            double size = FovCanvas.Width;
            if (double.IsNaN(size) || size <= 0)
                size = AppConfig.Current?.SliderSettings?.ActualFovSize ?? 0;
            if (size <= 0) return;

            double arm = Math.Clamp(size * CrossArmFraction, CrossMinPx, CrossMaxPx);
            double mid = size * 0.5;

            CrossH.X1 = mid - arm;
            CrossH.Y1 = mid;
            CrossH.X2 = mid + arm;
            CrossH.Y2 = mid;

            CrossV.X1 = mid;
            CrossV.Y1 = mid - arm;
            CrossV.X2 = mid;
            CrossV.Y2 = mid + arm;
        }

        private void PositionCircle(double? targetX = null, double? targetY = null)
        {
            if (FovCanvas == null) return;

            double size = FovCanvas.Width;
            if (double.IsNaN(size) || size <= 0)
                size = AppConfig.Current?.SliderSettings?.ActualFovSize ?? 0;
            if (size <= 0) return;

            double left;
            double top;

            if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse)
            {
                if (!targetX.HasValue || !targetY.HasValue)
                {
                    // Fallback: current cursor in screen space
                    var c = NativeMethods.GetCursorPosition();
                    targetX = c.X;
                    targetY = c.Y;
                }

                // target is screen pixels; window Left/Top are also screen-space for this overlay
                left = targetX.Value - this.Left - (size / 2);
                top = targetY.Value - this.Top - (size / 2);
            }
            else
            {
                left = (this.ActualWidth - size) / 2;
                top = (this.ActualHeight - size) / 2;
            }

            FovCanvas.Margin = new Thickness(left, top, 0, 0);
            UpdateCrossGeometry();
        }
    }
}