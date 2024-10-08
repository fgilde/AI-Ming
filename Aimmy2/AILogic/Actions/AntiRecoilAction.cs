using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Aimmy2.Class.Native;
using Gma.System.MouseKeyHook;
using InputLogic;
using OpenCvSharp;
using Point = System.Drawing.Point;

namespace Aimmy2.AILogic.Actions
{
    public class AntiRecoilAction : BaseAction
    {
        bool active = false;

        private Point? lastCrosshairPosition = null;
        private bool isShooting = false;
        private Mat crosshairTemplate = null;
        private int frameCounter = 0;
        private const int updateInterval = 10; // Update the template every 10 frames
        private const int movementThreshold = 3; // Threshold for mouse movement
        private double dynamicThreshold = 0.9; // Dynamic confidence threshold for matching
        private Point[] recentPositions = new Point[5]; // Buffer for averaging positions

        public override bool Active => active && base.Active;

        private bool userMovedMouse = false;
        private bool mouseMoveAttached = false;
        private bool autoMoved;

        public override async Task ExecuteAsync(Prediction[] predictions)
        {
            if (!Active)
                return;

            if (!mouseMoveAttached)
            {
                InputBindingManager.Instance.OnMouseMoveExt += InstanceOnOnMouseMoveExt;
                mouseMoveAttached = true;
            }

            var currentCapture = ImageCapture.Capture(ImageCapture.CaptureArea);

            if (InputBindingManager.IsHoldingBinding(MouseButtons.Left))
            {
                frameCounter++;
                if (/*frameCounter % updateInterval == 0 ||*/ !isShooting || userMovedMouse)
                {
                    isShooting = true;
                    lastCrosshairPosition = ExtractCrosshairPosition(currentCapture); // Update template
                    Debug.WriteLine("Crosshair template updated." + lastCrosshairPosition);
                }

                Point currentCrosshairPosition = FindCrosshairPosition(currentCapture);

                // Buffer positions to average across frames
                AddToRecentPositions(currentCrosshairPosition);
                Point averagedPosition = GetAveragePosition();

                Point smoothedPosition = ApplySmoothing(averagedPosition, lastCrosshairPosition.Value);

                int deltaX = smoothedPosition.X - lastCrosshairPosition?.X ?? 0;
                int deltaY = smoothedPosition.Y - lastCrosshairPosition?.Y ?? 0;

                if (Math.Abs(deltaX) > movementThreshold || Math.Abs(deltaY) > movementThreshold)
                {
                    MouseMove(-deltaX, -deltaY); // Move only when there's significant difference
                }

                lastCrosshairPosition = smoothedPosition;
            }
            else
            {
                if (isShooting)
                {
                    isShooting = false;
                    lastCrosshairPosition = null;
                    crosshairTemplate?.Dispose();
                    crosshairTemplate = null;
                    frameCounter = 0;
                }
            }

            await Task.CompletedTask;
        }

        // Buffer positions for averaging
        private void AddToRecentPositions(Point newPosition)
        {
            for (int i = recentPositions.Length - 1; i > 0; i--)
            {
                recentPositions[i] = recentPositions[i - 1];
            }
            recentPositions[0] = newPosition;
        }

        private Point GetAveragePosition()
        {
            int totalX = 0;
            int totalY = 0;
            int count = 0;

            foreach (var pos in recentPositions)
            {
                if (pos != null)
                {
                    totalX += pos.X;
                    totalY += pos.Y;
                    count++;
                }
            }

            if (count == 0)
                return new Point(0, 0);

            return new Point(totalX / count, totalY / count);
        }

        private void InstanceOnOnMouseMoveExt(MouseEventExtArgs obj)
        {
            if (!autoMoved && obj.X != lastX && obj.Y != lastY)
            {
                lastX = obj.X;
                lastY = obj.Y;
                userMovedMouse = true;
            }
            else
            {
                userMovedMouse = false;
            }
        }

        private Point ExtractCrosshairPosition(Bitmap capture)
        {
            int centerX = capture.Width / 2;
            int centerY = capture.Height / 2;
            int crosshairSize = 300;

            Rectangle crosshairArea = new Rectangle(centerX - crosshairSize / 2, centerY - crosshairSize / 2, crosshairSize, crosshairSize);
            Bitmap crosshairBitmap = capture.Clone(crosshairArea, capture.PixelFormat);
            crosshairTemplate = BitmapToMat(crosshairBitmap);

            return new Point(centerX, centerY);
        }

        private Point FindCrosshairPosition(Bitmap currentCapture)
        {
            if (crosshairTemplate == null)
            {
                throw new InvalidOperationException("Crosshair template has not been initialized.");
            }

            using (var captureMat = BitmapToMat(currentCapture))
            {
                Mat result = new Mat();
                Cv2.MatchTemplate(captureMat, crosshairTemplate, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out OpenCvSharp.Point maxLoc, out _);

                Debug.WriteLine($"Template match confidence: {maxVal}");
                if (maxVal >= dynamicThreshold)
                {
                    Debug.WriteLine($"Crosshair found at: {maxLoc.X}, {maxLoc.Y}");
                    return new Point(maxLoc.X, maxLoc.Y);
                }
                else
                {
                    Debug.WriteLine("Template match failed. Using last known position.");
                    return lastCrosshairPosition ?? new Point(0, 0);
                }
            }
        }

        private Point ApplySmoothing(Point currentCrosshairPosition, Point lastCrosshairPosition)
        {
            double alpha = 0.5;
            int smoothedX = (int)(alpha * currentCrosshairPosition.X + (1 - alpha) * lastCrosshairPosition.X);
            int smoothedY = (int)(alpha * currentCrosshairPosition.Y + (1 - alpha) * lastCrosshairPosition.Y);

            return new Point(smoothedX, smoothedY);
        }

        private Mat BitmapToMat(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                byte[] imageData = ms.ToArray();
                return Cv2.ImDecode(imageData, ImreadModes.Color);
            }
        }

        private void MouseMove(int deltaX, int deltaY)
        {
            autoMoved = true;
            //MouseManager.Move(deltaX, deltaY);
            var inputs = new MINPUT[1];
            inputs[0].type = (uint)MInputType.INPUT_MOUSE;
            inputs[0].U.mi = new MOUSEINPUT
            {
                dx = deltaX, // Relative movement
                dy = deltaY, // Relative movement
                dwFlags = (uint)(InputEventFlags.MOUSEEVENTF_MOVE | InputEventFlags.MOUSEEVENTF_MOVE_NOCOALESCE)
            };

            NativeAPIMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

            autoMoved = false;
        }

        public override Task OnPause()
        {
            return Task.CompletedTask;
        }

        public override Task OnResume()
        {
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            crosshairTemplate?.Dispose();
        }

        private int lastX = 0;
        private int lastY = 0;
        private int ScreenWidth = 5120;
        private int ScreenHeight = 1440;
    }
}
