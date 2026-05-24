using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerAim.Class.Native;
using PowerAim.Config;
using InputLogic;
using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace PowerAim.AILogic.Actions;

/// <summary>
///     <b>Experimental / BETA</b> image-based anti-recoil. Enabled only when
///     <see cref="AntiRecoilSettings.UseImageBasedAntiRecoil"/> is set; otherwise the legacy
///     pattern-based <see cref="AntiRecoilAction"/> handles things.
///     <para>
///     Built around the insight that <b>recoil</b> and <b>user aim</b> are different signals:
///     recoil is a sharp, short-lived impulse, whereas user aiming is smooth and sustained. An
///     EMA over the per-frame screen drift forms a <i>baseline</i> = the player's intentional
///     aim; the <i>residual</i> = the recoil impulse, which is what we counter.
///     </para>
///     <para>
///     Caveats: the image-pixel ↔ mouse-unit ratio depends on game FOV + sensitivity + Windows
///     pointer speed and isn't knowable without per-game calibration. The single strength slider
///     is a coarse knob — what feels right will vary between games.
///     </para>
/// </summary>
public class ImageBasedAntiRecoilAction : BaseAction
{
    private const int    PatchSize     = 192;
    private const double BaselineAlpha = 0.20;
    private const double KFactor       = 0.6;
    private const double Deadzone      = 0.6;
    private const int    MaxStepY      = 6;
    private const int    MaxStepX      = 3;

    private Mat?   _prevGray;
    private Mat?   _hannWindow;
    private double _baselineY = 0;
    private double _baselineX = 0;
    private bool   _baselineSeeded = false;

    public override bool Active =>
        base.Active &&
        AppConfig.Current.ToggleState.AntiRecoil &&
        AppConfig.Current.AntiRecoilSettings.UseImageBasedAntiRecoil &&
        // If pattern playback is armed, that path owns the recoil compensation entirely. Running
        // the BETA EMA-baseline on top would just fight the recorded strokes.
        !(AppConfig.Current.AntiRecoilSettings.UsePatternRecoil
          && !string.IsNullOrEmpty(AppConfig.Current.AntiRecoilSettings.ActivePatternName));

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active) { Reset(); return Task.CompletedTask; }
        if (!InputBindingManager.IsHoldingBinding(MouseButtons.Left)) { Reset(); return Task.CompletedTask; }

        var capture = ImageCapture;
        if (capture == null) return Task.CompletedTask;
        var area = capture.CaptureArea;
        if (area.Width < PatchSize + 4 || area.Height < PatchSize + 4) return Task.CompletedTask;

        var patchRect = new System.Drawing.Rectangle(
            area.X + (area.Width  - PatchSize) / 2,
            area.Y + (area.Height - PatchSize) / 2,
            PatchSize, PatchSize);

        Bitmap? bmp;
        try { bmp = capture.Capture(patchRect); }
        catch { return Task.CompletedTask; }
        if (bmp == null) return Task.CompletedTask;

        try
        {
            using var mat = BitmapToMat(bmp);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            using var grayFloat = new Mat();
            gray.ConvertTo(grayFloat, MatType.CV_32F);

            if (_prevGray == null)
            {
                _prevGray = grayFloat.Clone();
                _hannWindow ??= CreateHannWindow(PatchSize);
                _baselineSeeded = false;
                return Task.CompletedTask;
            }

            var shift = Cv2.PhaseCorrelate(_prevGray, grayFloat, _hannWindow!, out _);
            _prevGray.Dispose();
            _prevGray = grayFloat.Clone();

            if (!_baselineSeeded)
            {
                _baselineY = shift.Y;
                _baselineX = shift.X;
                _baselineSeeded = true;
                return Task.CompletedTask;
            }

            _baselineY = _baselineY * (1 - BaselineAlpha) + shift.Y * BaselineAlpha;
            _baselineX = _baselineX * (1 - BaselineAlpha) + shift.X * BaselineAlpha;

            double recoilY = shift.Y - _baselineY;
            double recoilX = shift.X - _baselineX;
            if (Math.Abs(recoilY) < Deadzone) recoilY = 0;
            if (Math.Abs(recoilX) < Deadzone) recoilX = 0;

            double strength = Math.Clamp(AppConfig.Current.AntiRecoilSettings.AutoStrength, 0, 1.5);
            if (strength <= 0) return Task.CompletedTask;

            double compY = recoilY * strength * KFactor;
            double compX = recoilX * strength * KFactor * 0.3;

            int my = (int)Math.Round(Math.Clamp(compY, -MaxStepY, MaxStepY));
            int mx = (int)Math.Round(Math.Clamp(compX, -MaxStepX, MaxStepX));

            if (my != 0 || mx != 0) MouseMove(mx, my);
        }
        finally
        {
            bmp.Dispose();
        }

        return Task.CompletedTask;
    }

    private static Mat CreateHannWindow(int size)
    {
        var w = new Mat();
        Cv2.CreateHanningWindow(w, new Size(size, size), MatType.CV_32F);
        return w;
    }

    private static Mat BitmapToMat(Bitmap bmp)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var mat = Mat.FromPixelData(bmp.Height, bmp.Width, MatType.CV_8UC3, data.Scan0, data.Stride);
            return mat.Clone();
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static void MouseMove(int dx, int dy)
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

    private void Reset()
    {
        _prevGray?.Dispose();
        _prevGray = null;
        _baselineY = 0;
        _baselineX = 0;
        _baselineSeeded = false;
    }

    public override Task OnPause()  { Reset(); return Task.CompletedTask; }
    public override Task OnResume() => Task.CompletedTask;

    public override void Dispose()
    {
        Reset();
        _hannWindow?.Dispose();
        _hannWindow = null;
    }
}
