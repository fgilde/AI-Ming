using System.ComponentModel;
using PowerAim.Class;
using System.Windows;
using System.Windows.Interop;
using PowerAim.Types;
using System.Windows.Controls;
using PowerAim.AI;
using PowerAim.AI.Contracts;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Class.Native;
using Nextended.UI.WPF.Extensions;

namespace PowerAim.UI.Visuality
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
            this.HideForCaptureIfEnabled();
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
            if (HeadRelativeArea is null)
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
            if(prediction is null)             {
                DetectedPlayerConfidence.Opacity = 0;
                DetectedPlayerFocus.Opacity = 0;
                DetectedTracers.Opacity = 0;
                HeadAreaBorder.Visibility = Visibility.Collapsed;
                return;
            }
            PredictionHost.Visibility = Visibility.Collapsed;
            // TranslatedRectangle is relative to the capture area, and this window sits on the capture
            // area — so it already is window-local (no origin offset).
            var lastDetectionBox = prediction.TranslatedRectangle;
            float localX = lastDetectionBox.X;
            float localY = lastDetectionBox.Y;
            // Fallback to model-space rectangle if translation was never filled.
            if (lastDetectionBox.Width <= 1 && prediction.Rectangle.Width > 1)
            {
                localX = prediction.Rectangle.X;
                localY = prediction.Rectangle.Y;
                lastDetectionBox = prediction.Rectangle;
            }
            var scaleFactor = AIManager.Instance.ImageCapture.Screen.GetScalingFactor();
            var scalingFactorX = scaleFactor.FactorX;
            var scalingFactorY = scaleFactor.FactorY;
            var centerX = (localX / scalingFactorX) + (lastDetectionBox.Width / 2.0);
            var centerY = (localY / scalingFactorY);

            if (AppConfig.Current.ToggleState.ShowAIConfidence)
            {
                DetectedPlayerConfidence.Opacity = 1;
                DetectedPlayerConfidence.Content = $"{Math.Round((prediction.Confidence * 100), 2)}%";

                var labelEstimatedHalfWidth = DetectedPlayerConfidence.ActualWidth / 2.0;
                DetectedPlayerConfidence.Margin = new(centerX - labelEstimatedHalfWidth, centerY - DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
            }

            var showTracers = AppConfig.Current.ToggleState.ShowTracers;
            DetectedTracers.Opacity = showTracers ? 1 : 0;
            if (showTracers)
            {
                DetectedTracers.X2 = centerX;
                DetectedTracers.Y2 = centerY + lastDetectionBox.Height;
            }

            DetectedPlayerFocus.Opacity = 1;
            var boxW = lastDetectionBox.Width / scalingFactorX;
            var boxH = lastDetectionBox.Height / scalingFactorY;
            DetectedPlayerFocus.Margin = new(centerX - (boxW / 2.0), centerY, 0, 0);
            DetectedPlayerFocus.Width = boxW;
            DetectedPlayerFocus.Height = boxH;

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