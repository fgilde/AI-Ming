using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using PowerAim.AILogic.Contracts;
using PowerAim.Config;
using Size = OpenCvSharp.Size;

namespace PowerAim.AILogic;

/// <summary>
///     One-shot pattern recorder. Captures a 192×192 patch at the centre of the FOV on a fixed
///     tick (~50 ms), phase-correlates each frame against the previous, and stores the cumulative
///     drift as a <see cref="RecoilPattern"/>.
///     <para>
///     The user is responsible for actually firing the weapon during the recording window — the
///     recorder does <i>not</i> press fire for them. This keeps the recorder game-agnostic.
///     </para>
/// </summary>
public static class RecoilPatternRecorder
{
    private const int PatchSize = 192;
    private const int SampleIntervalMs = 50;

    /// <summary>
    ///     Record a pattern of <paramref name="durationMs"/> milliseconds. Returns the populated
    ///     pattern on success, or a pattern with zero samples on failure.
    /// </summary>
    public static Task<RecoilPattern> RecordAsync(
        ICapture capture,
        int durationMs = 2000,
        string name = "Pattern",
        string weapon = "",
        IProgress<double>? progress = null,
        IProgress<RecoilSample>? sampleProgress = null,
        CancellationToken cancellation = default)
    {
        return Task.Run(() =>
        {
            var pattern = new RecoilPattern { Name = name, Weapon = weapon };
            if (capture == null) return pattern;
            var area = capture.CaptureArea;
            if (area.Width < PatchSize + 4 || area.Height < PatchSize + 4) return pattern;

            var patchRect = new System.Drawing.Rectangle(
                area.X + (area.Width  - PatchSize) / 2,
                area.Y + (area.Height - PatchSize) / 2,
                PatchSize, PatchSize);

            using var hann = new Mat();
            Cv2.CreateHanningWindow(hann, new Size(PatchSize, PatchSize), MatType.CV_32F);

            Mat? prevGray = null;
            double cumulativeX = 0;
            double cumulativeY = 0;

            try
            {
                var start = DateTime.UtcNow;
                while (true)
                {
                    if (cancellation.IsCancellationRequested) break;
                    int elapsed = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                    if (elapsed >= durationMs) break;

                    progress?.Report(Math.Clamp((double)elapsed / durationMs, 0, 1));

                    // Do NOT dispose the bitmap returned from ICapture — the capture backend
                    // caches it and may return the same instance again on WaitTimeout. See note
                    // in SensitivityCalibrator for the full reasoning.
                    Bitmap? bmp = SafeCapture(capture, patchRect);
                    if (bmp == null)
                    {
                        Thread.Sleep(SampleIntervalMs);
                        continue;
                    }
                    using var grayFloat = ToFloatGray(bmp);

                    if (prevGray != null)
                    {
                        var shift = Cv2.PhaseCorrelate(prevGray, grayFloat, hann, out _);
                        // shift.X / shift.Y describe how the second frame is offset from the first.
                        // If the gun kicked up, the view climbed — content moved DOWN relative to
                        // the previous frame → shift.Y > 0. We invert so the stored delta represents
                        // the compensation the player needs to push back: positive Y = move down.
                        cumulativeX += shift.X;
                        cumulativeY += shift.Y;

                        var sample = new RecoilSample
                        {
                            TimeMs = elapsed,
                            DeltaX = cumulativeX,
                            DeltaY = cumulativeY
                        };
                        pattern.Samples.Add(sample);
                        sampleProgress?.Report(sample);

                        prevGray.Dispose();
                        prevGray = grayFloat.Clone();
                    }
                    else
                    {
                        prevGray = grayFloat.Clone();
                    }

                    Thread.Sleep(SampleIntervalMs);
                }
            }
            finally
            {
                prevGray?.Dispose();
            }

            progress?.Report(1.0);
            return pattern;
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
}
