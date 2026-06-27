using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;
using PowerAim.AILogic.Contracts;
using PowerAim.Class.Native;
using Size = OpenCvSharp.Size;

namespace PowerAim.AILogic;

/// <summary>
///     One-shot calibration that figures out how many *screen pixels* a unit of raw mouse input
///     produces inside the currently-running game. Used by the sensitivity-calibration wizard to
///     recommend a sensible <c>MouseSensitivity</c> damping value.
///     <para>
///     Algorithm: capture a 192×192 patch at the centre of the FOV, send a horizontal mouse
///     impulse via <c>SendInput</c>, capture again, and run <see cref="Cv2.PhaseCorrelate"/> to
///     measure the actual screen drift. The mean of N rounds (alternating directions to keep the
///     view near its starting yaw) yields the pixels-per-input ratio.
///     </para>
///     The class is stateless from the outside — call <see cref="CalibrateAsync"/> and inspect the
///     returned <see cref="CalibrationResult"/>.
/// </summary>
public static class SensitivityCalibrator
{
    private const int PatchSize = 192;

    /// <summary>
    ///     Run a calibration sweep against the screen patch supplied by <paramref name="capture"/>.
    /// </summary>
    /// <param name="capture">Active capture backend (typically <c>AIManager.Instance.ImageCapture</c>).</param>
    /// <param name="moveAmount">Magnitude of each mouse impulse in raw input units. 200 is a reasonable default.</param>
    /// <param name="rounds">Number of paired left/right impulses. 6 strikes a balance between noise and time.</param>
    /// <param name="cancellation">Cooperative cancellation.</param>
    public static Task<CalibrationResult> CalibrateAsync(
        ICapture capture,
        int moveAmount = 200,
        int rounds = 6,
        CancellationToken cancellation = default)
    {
        return Task.Run(() =>
        {
            if (capture == null) return CalibrationResult.Failed("No capture source available.");
            var area = capture.CaptureArea;
            if (area.Width < PatchSize + 4 || area.Height < PatchSize + 4)
                return CalibrationResult.Failed($"Capture area too small ({area.Width}x{area.Height}).");

            var patchRect = new System.Drawing.Rectangle(
                area.X + (area.Width  - PatchSize) / 2,
                area.Y + (area.Height - PatchSize) / 2,
                PatchSize, PatchSize);

            using var hann = new Mat();
            Cv2.CreateHanningWindow(hann, new Size(PatchSize, PatchSize), MatType.CV_32F);

            // Phase correlation can only resolve a shift up to ~half the patch; a fixed impulse that
            // is right for one game over- or under-shoots another (e.g. 200 units on a ratio≈0.67 game
            // drifts ~133 px > the 96 px ceiling → every sample wrapped → "no motion"). So ADAPT the
            // impulse: too big → halve, too small → double, until the drift lands in the usable band.
            const double floorPx = 0.5;
            double ceilingPx = PatchSize / 2.0;        // 96
            var samples = new List<double>();          // px-per-input-unit ratios (impulse-independent)
            int amount = Math.Clamp(moveAmount, 8, 4000);
            int directionSign = +1;
            int attempts = 0;
            int maxAttempts = Math.Max(rounds * 4, 20);

            while (samples.Count < rounds && attempts < maxAttempts)
            {
                attempts++;
                if (cancellation.IsCancellationRequested) return CalibrationResult.Cancelled();

                // Don't dispose bitmaps returned from ICapture — DxgiScreenCapture caches the last
                // frame and returns the same instance on WaitTimeout (common when aiming at a static
                // wall). Disposing would invalidate the cached object.
                Bitmap? before = SafeCapture(capture, patchRect);
                if (before == null) return CalibrationResult.Failed("Failed to capture before-frame.");
                using var beforeMat = ToFloatGray(before);

                MoveMouse(directionSign * amount, 0);
                // Let the game render — short pause is enough; phase correlation tolerates stragglers.
                Thread.Sleep(140);

                Bitmap? after = SafeCapture(capture, patchRect);
                if (after == null) return CalibrationResult.Failed("Failed to capture after-frame.");
                using var afterMat = ToFloatGray(after);

                var shift = Cv2.PhaseCorrelate(beforeMat, afterMat, hann, out _);
                double measured = Math.Abs(shift.X);   // we drive X; content shifts the opposite way
                directionSign = -directionSign;        // alternate so the view stays near its start yaw

                if (measured > ceilingPx)
                {
                    // Overshot the patch (wraps) — use a smaller impulse next time.
                    if (amount > 8) amount = Math.Max(8, amount / 2);
                    continue;
                }
                if (measured < floorPx)
                {
                    // Barely moved — bigger impulse. If even a large one yields nothing the game
                    // isn't receiving the input (not focused) or the view is at a look limit.
                    if (amount < 4000) amount = Math.Min(4000, amount * 2);
                    continue;
                }

                samples.Add(measured / amount);
            }

            if (samples.Count < 2)
                return CalibrationResult.Failed(
                    "Couldn't detect usable screen motion. Make sure the GAME WINDOW is focused (click into it during the countdown), aim at a textured surface near eye level, and don't hold the view against a look up/down limit.");

            // Trim mean: drop the highest and lowest sample to suppress single-frame jitter.
            samples.Sort();
            if (samples.Count >= 5)
            {
                samples.RemoveAt(0);
                samples.RemoveAt(samples.Count - 1);
            }
            double ratio = samples.Average();           // screen pixels per input unit
            return CalibrationResult.Success(ratio, ratio * amount, amount, samples.Count);
        }, cancellation);
    }

    private static Bitmap? SafeCapture(ICapture capture, System.Drawing.Rectangle r)
    {
        try { return capture.Capture(r); }
        catch { return null; }
    }

    private static Mat ToFloatGray(Bitmap bmp)
    {
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            using var src = Mat.FromPixelData(bmp.Height, bmp.Width, MatType.CV_8UC3, data.Scan0, data.Stride);
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            var floatMat = new Mat();
            gray.ConvertTo(floatMat, MatType.CV_32F);
            return floatMat;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static void MoveMouse(int dx, int dy)
    {
        var inputs = new MINPUT[1];
        inputs[0].type = (uint)MInputType.INPUT_MOUSE;
        inputs[0].U.mi = new MOUSEINPUT
        {
            dx = dx,
            dy = dy,
            dwFlags = (uint)(InputEventFlags.MOUSEEVENTF_MOVE | InputEventFlags.MOUSEEVENTF_MOVE_NOCOALESCE)
        };
        NativeAPIMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}

/// <summary>
///     Outcome of a single calibration run. <see cref="Ratio"/> = screen pixels moved per raw mouse
///     input unit. A ratio > 1 means the game is more sensitive than 1:1 — the AI's output should
///     be damped; a ratio &lt; 1 means the game is undersensitive — increase in-game sens.
/// </summary>
public class CalibrationResult
{
    public bool Ok { get; private init; }
    public string? ErrorMessage { get; private init; }
    public bool WasCancelled { get; private init; }

    /// <summary>Screen pixels per raw mouse input unit, averaged across rounds.</summary>
    public double Ratio { get; private init; }

    /// <summary>Mean measured shift in pixels (for the displayed sample summary).</summary>
    public double MeasuredPixels { get; private init; }

    /// <summary>Magnitude of the impulse used during calibration.</summary>
    public int MoveAmount { get; private init; }

    /// <summary>Number of samples that survived outlier rejection.</summary>
    public int SamplesUsed { get; private init; }

    /// <summary>
    ///     Recommended <c>MouseSensitivity</c> damping value, derived from the measured ratio. The
    ///     current MouseManager applies the value as <c>t = 1 - sensitivity</c> in a lerp from 0 to
    ///     the AI-requested delta, so a high in-game sens (ratio > 1) needs a high damping factor.
    /// </summary>
    public double SuggestedSensitivity
    {
        get
        {
            if (!Ok || Ratio <= 0) return 0;
            if (Ratio <= 1.05) return 0.0;
            double s = 1.0 - 1.0 / Ratio;
            return Math.Clamp(s, 0.0, 0.95);
        }
    }

    public static CalibrationResult Success(double ratio, double measured, int amount, int samples) =>
        new() { Ok = true, Ratio = ratio, MeasuredPixels = measured, MoveAmount = amount, SamplesUsed = samples };

    public static CalibrationResult Failed(string message) =>
        new() { Ok = false, ErrorMessage = message };

    public static CalibrationResult Cancelled() =>
        new() { Ok = false, WasCancelled = true, ErrorMessage = "Cancelled." };
}
