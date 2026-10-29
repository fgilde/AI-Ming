using System.Windows;
using System.Windows.Media;
using PowerAim.AI;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Types;

namespace PowerAim.Other
{
    internal static partial class PredictionDrawer
    {
        public static void DrawPredictions(DrawingContext dc, IEnumerable<Prediction> predictions, Rect? targetArea = null)
        {
            foreach (var prediction in predictions)
                DrawPrediction(dc, prediction, targetArea);
            DrawAimPoint(dc, targetArea);
        }

        /// <summary>A cross on the aim point and a ring where it lands once the moves in flight have turned the view.</summary>
        private static void DrawAimPoint(DrawingContext dc, Rect? targetArea)
        {
            if (AppConfig.Current?.ToggleState?.ShowPredictedAim != true
                || !AimOverlay.TryGet(Environment.TickCount64, out var aim, out var predicted)) return;
            double dx = targetArea?.Left ?? 0, dy = targetArea?.Top ?? 0;
            var color = AppConfig.Current.ColorState.DetectedPlayerColor;
            DrawLine(dc, new Point(aim.X + dx - 6, aim.Y + dy), new Point(aim.X + dx + 6, aim.Y + dy), color, 2);
            DrawLine(dc, new Point(aim.X + dx, aim.Y + dy - 6), new Point(aim.X + dx, aim.Y + dy + 6), color, 2);
            dc.DrawEllipse(null, new Pen(Brushes.Orange, 2), new Point(predicted.X + dx, predicted.Y + dy), 5, 5);
        }

        public static void DrawPrediction(DrawingContext dc, Prediction prediction, Rect? targetArea)
        {
            var rect = prediction.TranslatedRectangle.ToRect();
            var config = AppConfig.Current;
            var color = config.ColorState.DetectedPlayerColor;
            var borderThickness = config.SliderSettings.BorderThickness;
            var cornerRadius = config.SliderSettings.CornerRadius;

            if (targetArea.HasValue)
            {
                rect.X += targetArea.Value.Left;
                rect.Y += targetArea.Value.Top;
            }

            // Draw the main rectangle with rounded corners
            DrawRoundedRectangle(dc, rect, cornerRadius, color, borderThickness);

            // Pose models: the skeleton inside the box.
            double dx = targetArea?.Left ?? 0, dy = targetArea?.Top ?? 0;
            foreach (var (a, b) in PoseSkeleton.Segments(prediction, 0.5f))
                DrawLine(dc, new Point(a.X + dx, a.Y + dy), new Point(b.X + dx, b.Y + dy), color, Math.Max(1, borderThickness / 2));

            // Draw AI confidence text if enabled
            if (config.ToggleState.ShowAIConfidence)
            {
                var confidenceText = $"{Math.Round((prediction.Confidence * 100), 2)}%";
                DrawText(dc, confidenceText, rect, config.SliderSettings.AIConfidenceFontSize, color);
            }

            // Draw tracers if enabled
            if (config.ToggleState.ShowTracers)
            {
                var centerX = rect.X + rect.Width / 2;
                var bottomY = rect.Y + rect.Height;

                Point tracerStart = targetArea?.GetBottomCenter().ToPoint() ?? System.Windows.Forms.Screen.PrimaryScreen!.Bounds.GetBottomCenter().ToPoint();
                Point tracerEnd = new Point(centerX, bottomY);

                DrawLine(dc, tracerStart, tracerEnd, color, 2);
            }

            // Draw trigger head area if enabled
            if (config.ToggleState.ShowTriggerHeadArea)
            {
                var headRelativeRect = AppConfig.Current.Triggers.FirstOrDefault(t => t is { Enabled: true, ExecutionIntersectionCheck: TriggerCheck.HeadIntersectingCenter })?.ExecutionIntersectionArea ?? RelativeRect.Default;

                double headAreaWidth = rect.Width * headRelativeRect.WidthPercentage;
                double headAreaHeight = rect.Height * headRelativeRect.HeightPercentage;
                double headAreaLeft = rect.X + rect.Width * headRelativeRect.LeftMarginPercentage;
                double headAreaTop = rect.Y + rect.Height * headRelativeRect.TopMarginPercentage;

                var headAreaRect = new Rect(headAreaLeft, headAreaTop, headAreaWidth, headAreaHeight);
                DrawRectangle(dc, headAreaRect, Colors.Green, borderThickness);
            }

            // Draw sizes if enabled
            if (config.ToggleState.ShowSizes)
            {
                DrawSizes(dc, rect, color);
            }
        }

        private static void DrawRoundedRectangle(DrawingContext dc, Rect rect, double cornerRadius, Color color, double borderThickness)
        {
            var pen = new Pen(new SolidColorBrush(color), borderThickness);
            var brush = Brushes.Transparent;

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(rect.X + cornerRadius, rect.Y), true, true);

                // Top line and top-right corner
                context.LineTo(new Point(rect.X + rect.Width - cornerRadius, rect.Y), true, false);
                context.ArcTo(new Point(rect.X + rect.Width, rect.Y + cornerRadius), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true, false);

                // Right line and bottom-right corner
                context.LineTo(new Point(rect.X + rect.Width, rect.Y + rect.Height - cornerRadius), true, false);
                context.ArcTo(new Point(rect.X + rect.Width - cornerRadius, rect.Y + rect.Height), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true, false);

                // Bottom line and bottom-left corner
                context.LineTo(new Point(rect.X + cornerRadius, rect.Y + rect.Height), true, false);
                context.ArcTo(new Point(rect.X, rect.Y + rect.Height - cornerRadius), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true, false);

                // Left line and top-left corner
                context.LineTo(new Point(rect.X, rect.Y + cornerRadius), true, false);
                context.ArcTo(new Point(rect.X + cornerRadius, rect.Y), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true, false);
            }

            dc.DrawGeometry(brush, pen, geometry);
        }

        private static void DrawText(DrawingContext dc, string text, Rect rect, double fontSize, Color color)
        {
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                new SolidColorBrush(color),
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            var textWidth = formattedText.Width;
            var textHeight = formattedText.Height;
            var x = rect.X + (rect.Width - textWidth) / 2;
            var y = rect.Y - textHeight - 2;

            dc.DrawText(formattedText, new Point(x, y));
        }

        private static void DrawLine(DrawingContext dc, Point start, Point end, Color color, double thickness)
        {
            var pen = new Pen(new SolidColorBrush(color), thickness);
            dc.DrawLine(pen, start, end);
        }

        private static void DrawRectangle(DrawingContext dc, Rect rect, Color color, double thickness)
        {
            var pen = new Pen(new SolidColorBrush(color), thickness);
            dc.DrawRectangle(null, pen, rect);
        }

        private static void DrawSizes(DrawingContext dc, Rect rect, Color color)
        {
            var fontSize = 10.0;
            var brush = new SolidColorBrush(color);

            // Draw width text
            string widthText = $"{rect.Width:F1}";
            var widthFormattedText = new FormattedText(
                widthText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                brush,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            var widthTextPosition = new Point(rect.X + (rect.Width - widthFormattedText.Width) / 2, rect.Y + rect.Height + 2);
            dc.DrawText(widthFormattedText, widthTextPosition);

            // Draw height text rotated -90 degrees
            string heightText = $"{rect.Height:F1}";
            var heightFormattedText = new FormattedText(
                heightText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                brush,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            var heightTextPosition = new Point(rect.X + rect.Width + 2 + heightFormattedText.Height, rect.Y + rect.Height);
            dc.PushTransform(new TranslateTransform(heightTextPosition.X, heightTextPosition.Y));
            dc.PushTransform(new RotateTransform(-90));

            dc.DrawText(heightFormattedText, new Point(0, 0));

            dc.Pop(); // RotateTransform
            dc.Pop(); // TranslateTransform
        }

        // Extension methods to convert between types
        private static Color ToMediaColor(this System.Drawing.Color color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private static Point ToPoint(this System.Drawing.PointF point)
        {
            return new Point(point.X, point.Y);
        }
    }
}
