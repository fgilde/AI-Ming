using System.ComponentModel;
using Aimmy2.Class;
using Class;
using System.Windows;
using System.Windows.Interop;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.AILogic.Contracts;
using System.Windows.Threading;
using Aimmy2.Class.Native;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for FOV.xaml
    /// </summary>
    public partial class FOV : Window
    {
        private DispatcherTimer _mousePositionTimer;

        public static FOV? Instance { get; private set; }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.MakeClickThrough();
            if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse)
            {
                _mousePositionTimer.Start();
            }
            AppConfig.Current.DropdownState.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(DropdownState.DetectionAreaType))
                {
                    if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse) 
                        _mousePositionTimer.Start();
                    else
                        _mousePositionTimer.Stop();
                    PositionCircle();
                }
            };
            PositionCircle();
        }

        public FOV()
        {
            Instance = this;
            InitializeComponent();
            Loaded += OnLoaded;
            AppConfig.BindToDataContext(this);
           
            _mousePositionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30) // Update alle 30 ms
            };
            _mousePositionTimer.Tick += OnMousePositionTimerTick;
        }
        private void OnMousePositionTimerTick(object sender, EventArgs e)
        {
            if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse)
            {
                var cursorPosition = NativeAPIMethods.GetCursorPosition();
                PositionCircle(cursorPosition.X, cursorPosition.Y);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.HideForCapture();
            AIManager.Instance.ImageCapture.PropertyChanged += ImageCaptureOnPropertyChanged;
        }

        private void ImageCaptureOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ICapture.CaptureArea))
            {
                this.MoveTo(AIManager.Instance.ImageCapture.CaptureArea);
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && AIManager.Instance?.ImageCapture?.CaptureArea != null)
            {
                this.MoveTo(AIManager.Instance.ImageCapture.CaptureArea);
            }
        }
        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            PositionCircle();
        }

        private void OnWindowLocationChanged(object sender, EventArgs e)
        {
            PositionCircle();
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse)
            {
                var mousePosition = e.GetPosition(this);
                PositionCircle(mousePosition.X, mousePosition.Y);
            }
        }

        private void PositionCircle(double? targetX = null, double? targetY = null)
        {
            if (AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse)
            {
                if (targetX.HasValue && targetY.HasValue)
                {
                    var relativeX = targetX.Value - this.Left;
                    var relativeY = targetY.Value - this.Top;

                    Circle.Margin = new Thickness(
                        relativeX - (Circle.Width / 2),
                        relativeY - (Circle.Height / 2),
                        0, 0);
                }
            }
            else
            {
                Circle.Margin = new Thickness(
                    (this.ActualWidth - Circle.Width) / 2,
                    (this.ActualHeight - Circle.Height) / 2,
                    0, 0);
            }
        }
    }
}