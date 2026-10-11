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
        /// <summary>
        /// WPF / DrawingContext path (OverlayDrawingMethod.DrawingContextVisualHost
        /// and WpfWindowCanvas consumers). Uses the same EspBoxSmoother as GDI so
        /// all four overlay methods share stable boxes.
        /// </summary>
        public static void DrawPredictions(DrawingContext dc, IEnumerable<Prediction> predictions, Rect? targetArea = null)
        {
            // PredictionVisualHost is a child of the capture-aligned DetectedPlayerWindow.
            // Boxes must be in capture-local / model space — never add CaptureArea origin again.
            var smoothed = EspBoxSmoother.Smooth(predictions, useScreenSpace: false);
            foreach (var (box, conf, classId, className, locked) in smoothed)
                DrawSmoothedBox(dc, box, conf, className, locked, targetArea: null);
        }

        private static void DrawSmoothedBox(
            DrawingContext dc,
            System.Drawing.RectangleF box,
            float confidence,
            string className,
            bool locked,
            Rect? targetArea)
        {
            var rect = new Rect(box.X, box.Y, box.Width, box.Height);
            var config = AppConfig.Current;
            var color = locked
                ? Color.FromArgb(255, 255, 64, 64)
                : config.ColorState.DetectedPlayerColor;
            var borderThickness = config.SliderSettings.BorderThickness;
            if (locked) borderThickness = Math.Max(borderThickness, borderThickness + 1);
            var cornerRadius = config.SliderSettings.CornerRadius;

            if (targetArea.HasValue)
            {
                rect.X += targetArea.Value.Left;
                rect.Y += targetArea.Value.Top;
            }

            DrawRoundedRectangle(dc, rect, cornerRadius, color, borderThickness);

            if (config.ToggleState.ShowAIConfidence)
            {
                var confidenceText = $"{Math.Round(confidence * 100, 1)}%";
                if (!string.IsNullOrEmpty(className) && className != "Enemy")
                    confidenceText = $"{className} {confidenceText}";
                DrawText(dc, confidenceText, rect, config.SliderSettings.AIConfidenceFontSize, color);
            }

            if (config.ToggleState.ShowTracers)
            {
                var centerX = rect.X + rect.Width / 2;
                var bottomY = rect.Y + rect.Height;
                Point tracerStart = targetArea?.GetBottomCenter().ToPoint()
                    ?? System.Windows.Forms.Screen.PrimaryScreen!.Bounds.GetBottomCenter().ToPoint();
                DrawLine(dc, tracerStart, new Point(centerX, bottomY), color, 2);
            }

            if (config.ToggleState.ShowTriggerHeadArea)
            {
                var headRelativeRect = AppConfig.Current.Triggers.FirstOrDefault(t =>
                    t is { Enabled: true, ExecutionIntersectionCheck: TriggerCheck.HeadIntersectingCenter })
                    ?.ExecutionIntersectionArea ?? RelativeRect.Default;

                double headAreaWidth = rect.Width * headRelativeRect.WidthPercentage;
                double headAreaHeight = rect.Height * headRelativeRect.HeightPercentage;
                double headAreaLeft = rect.X + rect.Width * headRelativeRect.LeftMarginPercentage;
                double headAreaTop = rect.Y + rect.Height * headRelativeRect.TopMarginPercentage;

                DrawRectangle(dc,
                    new Rect(headAreaLeft, headAreaTop, headAreaWidth, headAreaHeight),
                    Colors.Green, borderThickness);
            }

            if (config.ToggleState.ShowSizes)
                DrawSizes(dc, rect, color);
        }

        /// <summary>Single-prediction draw (kept for any direct callers).</summary>
        public static void DrawPrediction(DrawingContext dc, Prediction prediction, Rect? targetArea)
        {
            DrawPredictions(dc, new[] { prediction }, targetArea);
        }

        private static void DrawRoundedRectangle(DrawingContext dc, Rect rect, double cornerRadius, Color color, double borderThickness)
        {
            var pen = new Pen(new SolidColorBrush(color), borderThickness);
            var brush = Brushes.Transparent;
            cornerRadius = Math.Max(cornerRadius, 0.1);

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(rect.X + cornerRadius, rect.Y), true, true);
                context.LineTo(new Point(rect.X + rect.Width - cornerRadius, rect.Y), true, false);
                context.ArcTo(new Point(rect.X + rect.Width, rect.Y + cornerRadius), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(rect.X + rect.Width, rect.Y + rect.Height - cornerRadius), true, false);
                context.ArcTo(new Point(rect.X + rect.Width - cornerRadius, rect.Y + rect.Height), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(rect.X + cornerRadius, rect.Y + rect.Height), true, false);
                context.ArcTo(new Point(rect.X, rect.Y + rect.Height - cornerRadius), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(rect.X, rect.Y + cornerRadius), true, false);
                context.ArcTo(new Point(rect.X + cornerRadius, rect.Y), new Size(cornerRadius, cornerRadius), 0, false, SweepDirection.Clockwise, true, false);
            }

            dc.DrawGeometry(brush, pen, geometry);
        }

        private static void DrawText(DrawingContext dc, string text, Rect rect, double fontSize, Color color)
        {
            double ppd = 1.0;
            try
            {
                if (Application.Current?.MainWindow != null)
                    ppd = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;
            }
            catch { /* design-time / no window */ }

            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                new SolidColorBrush(color),
                ppd);

            var x = rect.X + (rect.Width - formattedText.Width) / 2;
            var y = rect.Y - formattedText.Height - 2;
            dc.DrawText(formattedText, new Point(x, y));
        }

        private static void DrawLine(DrawingContext dc, Point start, Point end, Color color, double thickness)
        {
            dc.DrawLine(new Pen(new SolidColorBrush(color), thickness), start, end);
        }

        private static void DrawRectangle(DrawingContext dc, Rect rect, Color color, double thickness)
        {
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(color), thickness), rect);
        }

        private static void DrawSizes(DrawingContext dc, Rect rect, Color color)
        {
            const double fontSize = 10.0;
            var brush = new SolidColorBrush(color);
            double ppd = 1.0;
            try
            {
                if (Application.Current?.MainWindow != null)
                    ppd = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;
            }
            catch { }

            var widthFormattedText = new FormattedText(
                $"{rect.Width:F1}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                brush,
                ppd);
            dc.DrawText(widthFormattedText,
                new Point(rect.X + (rect.Width - widthFormattedText.Width) / 2, rect.Y + rect.Height + 2));

            var heightFormattedText = new FormattedText(
                $"{rect.Height:F1}",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                brush,
                ppd);
            var heightTextPosition = new Point(rect.X + rect.Width + 2 + heightFormattedText.Height, rect.Y + rect.Height);
            dc.PushTransform(new TranslateTransform(heightTextPosition.X, heightTextPosition.Y));
            dc.PushTransform(new RotateTransform(-90));
            dc.DrawText(heightFormattedText, new Point(0, 0));
            dc.Pop();
            dc.Pop();
        }

        private static Point ToPoint(this System.Drawing.PointF point) => new(point.X, point.Y);
    }
}