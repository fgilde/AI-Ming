using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerAim.AILogic;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Types;
using Color = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace PowerAim.Other
{
    internal static partial class PredictionDrawer
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public static void DrawPredictions(IEnumerable<Prediction> predictions, Rectangle? targetArea = null)
        {
            IntPtr desktopDC = GetDC(IntPtr.Zero);
            using (Graphics graphics = Graphics.FromHdc(desktopDC))
            {
                DrawPredictions(graphics, predictions, targetArea);
            }
            ReleaseDC(IntPtr.Zero, desktopDC);
        }

        public static void DrawPredictions(Form window, IEnumerable<Prediction> predictions, Rectangle? targetArea = null)
        {
            if (window.InvokeRequired)
            {
                window.Invoke(() => DrawPredictions(window, predictions, targetArea));
                return;
            }
            var hwnd = window.Handle;
            IntPtr hdc = GetDC(hwnd);

            using (Graphics graphics = Graphics.FromHdc(hdc))
            {
                DrawPredictions(graphics, predictions, targetArea);
            }

            ReleaseDC(hwnd, hdc);
        }

        public static void DrawPredictions(Graphics graphics, IEnumerable<Prediction> predictions, Rectangle? targetArea = null)
        {
            foreach (var prediction in predictions)
                DrawPrediction(graphics, prediction, targetArea);
        }


        private static void Draw(Graphics graphics, RectangleF rect, float cornerRadius, MediaColor color, float borderThickness) => Draw(graphics, rect, cornerRadius, color.ToDrawingColor(), borderThickness);
        private static void Draw(Graphics graphics, RectangleF rect, float cornerRadius, Color color, float borderThickness) => Draw(graphics, new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), cornerRadius, color, borderThickness);

        private static void Draw(Graphics graphics, Rectangle rect, float cornerRadius, MediaColor color, float borderThickness) => Draw(graphics, rect, cornerRadius, color.ToDrawingColor(), borderThickness);
        private static void Draw(Graphics graphics, Rectangle rect, float cornerRadius, Color color, float borderThickness)
        {
            var pen = new Pen(color, borderThickness);
            var graphicsPath = new System.Drawing.Drawing2D.GraphicsPath();
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            cornerRadius = Math.Max(cornerRadius, 0.1f);

            graphicsPath.AddArc(rect.X, rect.Y, cornerRadius, cornerRadius, 180, 90);
            graphicsPath.AddArc(rect.X + rect.Width - cornerRadius, rect.Y, cornerRadius, cornerRadius, 270, 90);
            graphicsPath.AddArc(rect.X + rect.Width - cornerRadius, rect.Y + rect.Height - cornerRadius, cornerRadius, cornerRadius, 0, 90);
            graphicsPath.AddArc(rect.X, rect.Y + rect.Height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
            graphicsPath.CloseFigure();

            graphics.DrawPath(pen, graphicsPath);
            pen.Dispose();
            graphicsPath.Dispose();
        }

        private static void DrawText(Graphics graphics, string text, RectangleF rect, Font font, Color color) => DrawText(graphics, text, new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), font, color);
        private static void DrawText(Graphics graphics, string text, RectangleF rect, Font font, MediaColor color) => DrawText(graphics, text, new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), font, color);
        private static void DrawText(Graphics graphics, string text, Rectangle rect, Font font, MediaColor color) => DrawText(graphics, text, rect, font, color.ToDrawingColor());
        private static void DrawText(Graphics graphics, string text, Rectangle rect, Font font, Color color)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var brush = new SolidBrush(color);
            var textSize = graphics.MeasureString(text, font);
            graphics.DrawString(text, font, brush, rect.X + (rect.Width - textSize.Width) / 2, rect.Y - textSize.Height - 2);
            brush.Dispose();
        }

        private static void Draw(Graphics graphics, PointF start, PointF end, System.Windows.Media.Color color, float thickness)
        {
            var pen = new Pen(color.ToDrawingColor(), thickness);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.DrawLine(pen, start, end);
            pen.Dispose();
        }

        private static void Draw(Graphics graphics, RectangleF rect, Color color, float thickness)
        {
            var pen = new Pen(color, thickness);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            pen.Dispose();
        }
        private static Color ToDrawingColor(this MediaColor color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private static void DrawPrediction(Graphics graphics, Prediction prediction, Rectangle? targetArea)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = prediction.TranslatedRectangle;
            var config = AppConfig.Current;
            var color = config.ColorState.DetectedPlayerColor;
            var borderThickness = config.SliderSettings.BorderThickness;
            var font = new Font("Consolas", config.SliderSettings.AIConfidenceFontSize);

            if (targetArea.HasValue)
            {
                rect.X += targetArea.Value.Left;
                rect.Y += targetArea.Value.Top;
            }

            Draw(graphics, rect, (float)config.SliderSettings.CornerRadius, color, (float)borderThickness);

            if (config.ToggleState.ShowAIConfidence)
            {
                var confidenceText = $"{Math.Round((prediction.Confidence * 100), 2)}%";
                DrawText(graphics, confidenceText, rect, font, color);
            }

            if (config.ToggleState.ShowTracers)
            {
                var centerX = rect.X + rect.Width / 2;
                var bottomY = rect.Y + rect.Height;

                PointF tracerStart = targetArea?.GetBottomCenter() ?? Screen.PrimaryScreen!.Bounds.GetBottomCenter();

                Draw(graphics, tracerStart, new PointF(centerX, bottomY), color, 2);
            }

            if (config.ToggleState.ShowTriggerHeadArea)
            {
                var headRelativeRect = AppConfig.Current.Triggers.FirstOrDefault(t => t is { Enabled: true, ExecutionIntersectionCheck: TriggerCheck.HeadIntersectingCenter })?.ExecutionIntersectionArea ?? RelativeRect.Default;

                float headAreaWidth = rect.Width * headRelativeRect.WidthPercentage;
                float headAreaHeight = rect.Height * headRelativeRect.HeightPercentage;
                float headAreaLeft = rect.X + rect.Width * headRelativeRect.LeftMarginPercentage;
                float headAreaTop = rect.Y + rect.Height * headRelativeRect.TopMarginPercentage;

                var headAreaRect = new RectangleF(headAreaLeft, headAreaTop, headAreaWidth, headAreaHeight);
                Draw(graphics, headAreaRect, Color.Green, (float)borderThickness);
            }

            if (config.ToggleState.ShowSizes)
            {
                string widthText = $"{rect.Width:F1}";
                var widthRect = new Rectangle((int)rect.X, (int)(rect.Y + rect.Height + 2), (int)rect.Width, font.Height);
                var font2 = new Font("Consolas", 10);
                DrawText(graphics, widthText, widthRect, font2, color);


                string heightText = $"{rect.Height:F1}";
                var heightRect = new Rectangle((int)(rect.X + rect.Width + 2), (int)rect.Y, font.Height, (int)rect.Height);

                graphics.TranslateTransform(heightRect.X, heightRect.Y + heightRect.Height);
                graphics.RotateTransform(-90);
                DrawText(graphics, heightText, new Rectangle(0, 0, heightRect.Height, heightRect.Width), font2, color);

                graphics.ResetTransform();
            }

            font.Dispose();
        }



        private static void cDrawPrediction(Graphics graphics, Prediction prediction, Rectangle? targetArea)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = prediction.TranslatedRectangle;
            var config = AppConfig.Current;
            var color = config.ColorState.DetectedPlayerColor;
            var borderThickness = config.SliderSettings.BorderThickness;
            var font = new Font("Consolas", config.SliderSettings.AIConfidenceFontSize);

            if (targetArea.HasValue)
            {
                rect.X += targetArea.Value.Left;
                rect.Y += targetArea.Value.Top;
            }

            Draw(graphics, rect, (float)config.SliderSettings.CornerRadius, color, (float)borderThickness);

            if (config.ToggleState.ShowAIConfidence)
            {
                var confidenceText = $"{Math.Round((prediction.Confidence * 100), 2)}%";
                DrawText(graphics, confidenceText, rect, font, color);
            }

            if (config.ToggleState.ShowTracers)
            {
                var centerX = rect.X + rect.Width / 2;
                var bottomY = rect.Y + rect.Height;

                PointF tracerStart = targetArea?.GetBottomCenter() ?? Screen.PrimaryScreen!.Bounds.GetBottomCenter();

                Draw(graphics, tracerStart, new PointF(centerX, bottomY), color, 2);
            }

            if (config.ToggleState.ShowTriggerHeadArea)
            {
                var headRelativeRect = AppConfig.Current.Triggers.FirstOrDefault(t => t is { Enabled: true, ExecutionIntersectionCheck: TriggerCheck.HeadIntersectingCenter })?.ExecutionIntersectionArea ?? RelativeRect.Default;

                float headAreaWidth = rect.Width * headRelativeRect.WidthPercentage;
                float headAreaHeight = rect.Height * headRelativeRect.HeightPercentage;
                float headAreaLeft = rect.X + rect.Width * headRelativeRect.LeftMarginPercentage;
                float headAreaTop = rect.Y + rect.Height * headRelativeRect.TopMarginPercentage;

                var headAreaRect = new RectangleF(headAreaLeft, headAreaTop, headAreaWidth, headAreaHeight);
                Draw(graphics, headAreaRect, Color.Green, (float)borderThickness);
            }

            font.Dispose();
        }
    }
}
