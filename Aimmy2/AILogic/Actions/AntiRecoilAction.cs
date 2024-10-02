using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Aimmy2.Class.Native;
using System.Windows.Input;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using InputLogic;
using OpenCvSharp;
using Point = System.Drawing.Point;

namespace Aimmy2.AILogic.Actions;

public class AntiRecoilAction : BaseAction
{
    private Point? lastCrosshairPosition = null;
    private bool isShooting = false;
    private Mat crosshairTemplate = null;
    private int frameCounter = 0;
    private const int updateInterval = 10; // Update the template every 10 frames
    private const int movementThreshold = 3; // Threshold for mouse movement

    protected override bool Active => false; // base.Active && true;

    public override async Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active)
            return;

        var currentCapture = this.AIManager.ImageCapture.LastCapture;

        if (InputBindingManager.IsHoldingBindingFor(MouseButtons.Left, null))
        {
            if (!isShooting)
            {
                isShooting = true;
                lastCrosshairPosition = ExtractCrosshairPosition(currentCapture);
                Debug.WriteLine("Crosshair template initialized." + lastCrosshairPosition);
            }

            frameCounter++;
            if (frameCounter % updateInterval == 0)
            {
                lastCrosshairPosition = ExtractCrosshairPosition(currentCapture); // Update template
                Debug.WriteLine("Crosshair template updated." + lastCrosshairPosition);
            }

            Point currentCrosshairPosition = FindCrosshairPosition(currentCapture);

            // Apply smoothing
            Point smoothedPosition = ApplySmoothing(currentCrosshairPosition, lastCrosshairPosition.Value);

            int deltaX = smoothedPosition.X - lastCrosshairPosition?.X ?? 0;
            int deltaY = smoothedPosition.Y - lastCrosshairPosition?.Y ?? 0;

            if (Math.Abs(deltaX) > movementThreshold || Math.Abs(deltaY) > movementThreshold)
            {
                MouseMove(-deltaX, -deltaY);
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

    private Point ExtractCrosshairPosition(Bitmap capture)
    {
        int centerX = capture.Width / 2;
        int centerY = capture.Height / 2;
        int crosshairSize = 50;

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

            double confidenceThreshold = 0.9;
            if (maxVal >= confidenceThreshold)
            {
                return new Point(maxLoc.X, maxLoc.Y);
            }
            else
            {
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
        if (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3) // Threshold for meaningful movement
        {
            Debug.WriteLine($"Mouse moved: deltaX={deltaX}, deltaY={deltaY}");

            var inputs = new MINPUT[1];
            inputs[0].type = (uint)MInputType.INPUT_MOUSE;
            inputs[0].U.mi = new MOUSEINPUT
            {
                dx = deltaX,
                dy = deltaY,
                dwFlags = (uint)InputEventFlags.MOUSEEVENTF_MOVE
            };

            NativeAPIMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
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
}

