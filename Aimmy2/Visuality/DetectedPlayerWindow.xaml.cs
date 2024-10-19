using System.ComponentModel;
using Aimmy2.Class;
using System.Windows;
using System.Windows.Interop;
using Aimmy2.Types;
using System.Windows.Controls;
using Aimmy2.AILogic;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.Class.Native;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for DetectedPlayerWindow.xaml
    /// </summary>
    public partial class DetectedPlayerWindow
    {
        public static DetectedPlayerWindow? Instance { get; private set; }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.MakeClickThrough();
        }

        public DetectedPlayerWindow()
        {
            Instance = this;
            InitializeComponent();
            AppConfig.BindToDataContext(this);
            Loaded += OnLoaded;
            Title = "";
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
                Dispatcher.Invoke(() =>
                {
                    this.MoveTo(AIManager.Instance.ImageCapture.CaptureArea, GetPadding());
                });
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public RelativeRect? HeadRelativeArea { get; set; }

        private void UpdateHeadArea()
        {
            if (HeadRelativeArea == null)
            {
                HeadAreaBorder.Visibility = Visibility.Collapsed;
                return;
            }

            double parentWidth = DetectedPlayerFocus.ActualWidth;
            double parentHeight = DetectedPlayerFocus.ActualHeight;

            double headAreaWidth = parentWidth * HeadRelativeArea.Value.WidthPercentage;
            double headAreaHeight = parentHeight * HeadRelativeArea.Value.HeightPercentage;
            double headAreaLeft = parentWidth * HeadRelativeArea.Value.LeftMarginPercentage;
            double headAreaTop = parentHeight * HeadRelativeArea.Value.TopMarginPercentage;

            HeadAreaBorder.Width = headAreaWidth;
            HeadAreaBorder.Height = headAreaHeight;
            Canvas.SetLeft(HeadAreaBorder, headAreaLeft);
            Canvas.SetTop(HeadAreaBorder, headAreaTop);

            HeadAreaBorder.Visibility = Visibility.Visible;
        }

        public void SetHeadRelativeArea(RelativeRect? relativeRect)
        {
            HeadRelativeArea = relativeRect;
            UpdateHeadArea();
        }

        private void DetectedPlayerWindow_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                this.MoveTo(AIManager.Instance.ImageCapture.CaptureArea, GetPadding());
            }
        }

        private Thickness? GetPadding()
        {
            return AppConfig.Current.CaptureSource.TargetType == CaptureTargetType.Process ? new Thickness(BorderThickness.Left +2, BorderThickness.Top - 2, BorderThickness.Right +2, BorderThickness.Bottom) : new Thickness(0);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateDetectedTracersPosition();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            UpdateDetectedTracersPosition();
        }

        private void UpdateDetectedTracersPosition()
        {
            double centerX = this.ActualWidth / 2;
            double bottomY = this.ActualHeight;

            DetectedTracers.X1 = centerX;
            DetectedTracers.X2 = centerX;
            DetectedTracers.Y1 = bottomY;
            DetectedTracers.Y2 = bottomY - 50;
        }

        public void DrawPredictionCanvas(Prediction? prediction)
        {
            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DrawPredictionCanvas(prediction));
                return;
            }
            if(prediction == null)             {
                DetectedPlayerConfidence.Opacity = 0;
                DetectedPlayerFocus.Opacity = 0;
                DetectedTracers.Opacity = 0;
                HeadAreaBorder.Visibility = Visibility.Collapsed;
                return;
            }
            PredictionHost.Visibility = Visibility.Collapsed;
            var lastDetectionBox = prediction.TranslatedRectangle;
            //var captureArea = ImageCapture.CaptureArea;
            var scaleFactor = AIManager.Instance.ImageCapture.Screen.GetScalingFactor();
            var scalingFactorX = scaleFactor.FactorX;
            var scalingFactorY = scaleFactor.FactorY;
            //var centerX = Convert.ToInt16((lastDetectionBox.X + captureArea.Left) / scalingFactorX) + (lastDetectionBox.Width / 2.0);
            //var centerY = Convert.ToInt16((lastDetectionBox.Y + captureArea.Top) / scalingFactorY);
            var centerX = Convert.ToInt16((lastDetectionBox.X) / scalingFactorX) + (lastDetectionBox.Width / 2.0);
            var centerY = Convert.ToInt16((lastDetectionBox.Y) / scalingFactorY);

            if (AppConfig.Current.ToggleState.ShowAIConfidence)
            {
                DetectedPlayerConfidence.Opacity = 1;
                DetectedPlayerConfidence.Content = $"{Math.Round((prediction.Confidence * 100), 2)}%";

                var labelEstimatedHalfWidth = DetectedPlayerConfidence.ActualWidth / 2.0;
                DetectedPlayerConfidence.Margin = new Thickness(centerX - labelEstimatedHalfWidth, centerY - DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
            }

            var showTracers = AppConfig.Current.ToggleState.ShowTracers;
            DetectedTracers.Opacity = showTracers ? 1 : 0;
            if (showTracers)
            {
                //_playerOverlay.DetectedTracers.X1 = captureArea.GetBottomCenter().X;
                //_playerOverlay.DetectedTracers.Y1 = captureArea.GetBottomCenter().Y;
                DetectedTracers.X2 = centerX;
                DetectedTracers.Y2 = centerY + lastDetectionBox.Height;
            }

            Canvas.Opacity = AppConfig.Current.SliderSettings.Opacity;

            DetectedPlayerFocus.Opacity = 1;
            DetectedPlayerFocus.Margin = new Thickness(centerX - (lastDetectionBox.Width / 2.0), centerY, 0, 0);
            DetectedPlayerFocus.Width = lastDetectionBox.Width;
            DetectedPlayerFocus.Height = lastDetectionBox.Height;

            var headRelativeRect = AppConfig.Current.Triggers.FirstOrDefault(t => t is { Enabled: true, ExecutionIntersectionCheck: TriggerCheck.HeadIntersectingCenter })?.ExecutionIntersectionArea ?? RelativeRect.Default;

            SetHeadRelativeArea(AppConfig.Current.ToggleState.ShowTriggerHeadArea ? headRelativeRect : null);
        }

        public void DrawPredictions(Prediction[] predictions, Rect toRect)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DrawPredictions(predictions, toRect));
                return;
            }

            DrawPredictionCanvas(null);
            PredictionHost.Visibility = Visibility.Visible;
            PredictionHost.DrawPredictions(predictions, toRect);
        }
    }
}
