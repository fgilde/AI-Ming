using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Windows.Forms;
using PowerAim.AI.Recoil;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.Input;
using OpenCvSharp;
using Nextended.UI.Input;
using Size = OpenCvSharp.Size;

namespace PowerAim.AI.Actions;

/// <summary>
///     Pure vision anti-recoil with feed-forward neural anticipation + phase-correlation feedback.
///     Screen pixels only — no memory, no injection, no kernel.
///     <para>
///     Pipeline (per fire frame while LMB held):
///     1. Capture crosshair-centred patch.
///     2. <see cref="RecoilPredictor"/> anticipates next kick from burst index + residual history + class.
///     3. Phase-correlate against previous frame → raw screen drift (px).
///     4. EMA baseline = intentional aim; residual = recoil impulse (stochastic remainder).
///     5. Additive compensation: predicted kick + residual feedback.
///     6. Accumulator + fractional carry; micro-steps for smooth cancel.
///     </para>
/// </summary>
public class SmartNeuralAntiRecoilAction : BaseAction
{
    private const double MinIntervalMs = 12.0;
    private DateTime _lastRunUtc = DateTime.MinValue;
    private const int    PatchSize       = 192;
    private const int    HistoryLen      = 4;
    private const double BaselineAlpha   = 0.08;
    private const double VerticalGain    = 1.35;
    private const double HorizontalGain  = 0.55;
    private const double DeadzoneY       = 0.12;
    private const double DeadzoneX       = 0.20;
    private const int    MaxStepY        = 18;
    private const int    MaxStepX        = 8;
    private const int    MicroSteps      = 3;
    private const double MinConfidence   = 0.15;

    /// <summary>How much of the RNN/analytic prediction to apply before residual feedback.</summary>
    private const double PredictWeight   = 0.85;

    private double _accY;
    private double _accX;

    private readonly Queue<Mat> _grayHistory = new();
    private readonly List<(float rx, float ry)> _residualHistory = new();
    private Mat?   _hannWindow;
    private double _baselineY;
    private double _baselineX;
    private bool   _baselineSeeded;
    private bool   _firing;
    private int    _burstFrame;
    private long   _shotsAtBurstStart = -1;

    public override bool Active
    {
        get
        {
            if (!base.Active) return false;
            if (!AppConfig.Current.ToggleState.AntiRecoil) return false;
            if (!AppConfig.Current.ToggleState.SmartNeuralAntiRecoil) return false;

            var profile = AppConfig.Current.AntiRecoilSettings?.ActiveProfile;
            if (profile == null) return true;
            return profile.Mode == AntiRecoilMode.SmartNeural;
        }
    }

    public override Task Execute(Prediction[] predictions) =>
        Task.Run(() => ExecuteAsync(predictions.ToArray()));

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active)
        {
            Reset();
            return Task.CompletedTask;
        }

        bool holding = InputBindingManager.IsHoldingBinding(MouseButtons.Left);
        if (!holding)
        {
            if (_firing) Reset();
            return Task.CompletedTask;
        }

        if (!_firing)
        {
            _firing = true;
            _burstFrame = 0;
            _shotsAtBurstStart = SessionStats.Instance.ShotsFired;
            _residualHistory.Clear();
            RecoilPredictor.Instance.EnsureLoaded();
        }
        else
        {
            // Prefer session shot counter when it advances; else advance per execute tick.
            long shots = SessionStats.Instance.ShotsFired;
            if (shots > _shotsAtBurstStart)
                _burstFrame = (int)(shots - _shotsAtBurstStart);
            else
            {
                // No external shot counter: advance locally and keep SessionStats in sync.
                _burstFrame++;
                SessionStats.Instance.IncrementShots();
                _shotsAtBurstStart = SessionStats.Instance.ShotsFired - _burstFrame;
            }
        }

        string weaponClass = ResolveWeaponClass(predictions);

        var capture = ImageCapture;
        if (capture == null) return Task.CompletedTask;

        var area = capture.CaptureArea;
        if (area.Width < PatchSize + 4 || area.Height < PatchSize + 4)
            return Task.CompletedTask;

        var patchRect = new System.Drawing.Rectangle(
            area.X + (area.Width  - PatchSize) / 2,
            area.Y + (area.Height - PatchSize) / 2,
            PatchSize, PatchSize);

        // Throttle OpenCV work + second screen capture so the aim path stays responsive.
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastRunUtc).TotalMilliseconds < MinIntervalMs)
            return Task.CompletedTask;
        _lastRunUtc = nowUtc;

        // Prefer a copy of the AI loop's latest frame: a second DXGI acquire would consume desktop
        // frames the loop then has to wait for (mouse hitch). Own capture only before the first frame.
        Bitmap? bmp;
        try { bmp = capture.CopyLastCaptureCenter(PatchSize) ?? capture.Capture(patchRect); }
        catch { return Task.CompletedTask; }
        if (bmp == null) return Task.CompletedTask;

        try
        {
            using var mat = BitmapToMat(bmp);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            using var grayFloat = new Mat();
            gray.ConvertTo(grayFloat, MatType.CV_32F);

            _hannWindow ??= CreateHannWindow(PatchSize);

            // --- Feed-forward prediction (anticipates next kick) ---
            Vector2 predicted = RecoilPredictor.Instance.Predict(
                _burstFrame, _residualHistory, weaponClass);

            if (_grayHistory.Count == 0)
            {
                _grayHistory.Enqueue(grayFloat.Clone());
                // Still apply pure prediction on first frame of burst
                ApplyCompensation(predicted.X * PredictWeight, predicted.Y * PredictWeight, strength: GetStrength(), confidence: 1.0);
                return Task.CompletedTask;
            }

            var prev = _grayHistory.Peek();
            var shift = Cv2.PhaseCorrelate(prev, grayFloat, _hannWindow!, out double response);
            double confidence = Math.Clamp(response, 0.0, 1.0);

            _grayHistory.Enqueue(grayFloat.Clone());
            while (_grayHistory.Count > HistoryLen)
                _grayHistory.Dequeue().Dispose();

            if (!_baselineSeeded)
            {
                _baselineY = shift.Y;
                _baselineX = shift.X;
                _baselineSeeded = true;
                ApplyCompensation(predicted.X * PredictWeight, predicted.Y * PredictWeight, GetStrength(), confidence);
                return Task.CompletedTask;
            }

            _baselineY = _baselineY * (1.0 - BaselineAlpha) + shift.Y * BaselineAlpha;
            _baselineX = _baselineX * (1.0 - BaselineAlpha) + shift.X * BaselineAlpha;

            double residualY = shift.Y - _baselineY;
            double residualX = shift.X - _baselineX;

            if (Math.Abs(residualY) < DeadzoneY) residualY = 0;
            if (Math.Abs(residualX) < DeadzoneX) residualX = 0;

            _residualHistory.Add(((float)residualX, (float)residualY));
            while (_residualHistory.Count > HistoryLen)
                _residualHistory.RemoveAt(0);

            double strength = GetStrength();
            double confThresh = GetConfThresh();
            if (confidence < confThresh && Math.Abs(predicted.Y) < 0.01 && Math.Abs(predicted.X) < 0.01)
                return Task.CompletedTask;

            double gate = 0.65 + 0.35 * Math.Max(confidence, 0.5);

            // Additive: RNN handles deterministic pattern; residual closes the stochastic gap.
            double targetY = predicted.Y * PredictWeight + residualY * (1.0 - PredictWeight * 0.5);
            double targetX = predicted.X * PredictWeight + residualX * (1.0 - PredictWeight * 0.5);

            ApplyCompensation(targetX, targetY, strength * gate, confidence);
        }
        finally
        {
            bmp.Dispose();
        }

        return Task.CompletedTask;
    }

    private void ApplyCompensation(double targetX, double targetY, double strength, double confidence)
    {
        if (strength <= 0) return;

        double compY = targetY * VerticalGain   * strength;
        double compX = targetX * HorizontalGain * strength;

        _accY += compY;
        _accX += compX;

        int moveY = (int)Math.Truncate(_accY);
        int moveX = (int)Math.Truncate(_accX);
        _accY -= moveY;
        _accX -= moveX;

        moveY = Math.Clamp(moveY, -MaxStepY, MaxStepY);
        moveX = Math.Clamp(moveX, -MaxStepX, MaxStepX);

        if (moveY == 0 && moveX == 0) return;
        ApplyMicroMove(moveX, moveY);
    }

    private double GetStrength()
    {
        var profile = AppConfig.Current.AntiRecoilSettings?.ActiveProfile;
        if (profile != null && profile.Mode == AntiRecoilMode.SmartNeural)
            return Math.Clamp(profile.NeuralStrength, 0.0, 2.0);
        return 1.15;
    }

    private double GetConfThresh()
    {
        var profile = AppConfig.Current.AntiRecoilSettings?.ActiveProfile;
        if (profile != null && profile.Mode == AntiRecoilMode.SmartNeural)
            return Math.Clamp(profile.NeuralConfidenceThreshold, 0.0, 1.0);
        return MinConfidence;
    }

    private static string ResolveWeaponClass(Prediction[] predictions)
    {
        if (predictions == null || predictions.Length == 0) return "";
        Prediction? best = null;
        float bestConf = -1;
        foreach (var p in predictions)
        {
            if (p == null) continue;
            if (p.Confidence > bestConf)
            {
                bestConf = p.Confidence;
                best = p;
            }
        }
        return best?.ClassName ?? "";
    }

    private static void ApplyMicroMove(int totalX, int totalY)
    {
        if (MicroSteps <= 1)
        {
            InputSender.Move(totalX, totalY);
            return;
        }

        int sx = totalX / MicroSteps;
        int sy = totalY / MicroSteps;
        int rx = totalX - sx * MicroSteps;
        int ry = totalY - sy * MicroSteps;

        for (int i = 0; i < MicroSteps; i++)
        {
            int dx = sx + (i == MicroSteps - 1 ? rx : 0);
            int dy = sy + (i == MicroSteps - 1 ? ry : 0);
            if (dx != 0 || dy != 0)
                InputSender.Move(dx, dy);
        }
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

    private void Reset()
    {
        while (_grayHistory.Count > 0)
            _grayHistory.Dequeue().Dispose();
        _residualHistory.Clear();
        _baselineY = 0;
        _baselineX = 0;
        _baselineSeeded = false;
        _accY = 0;
        _accX = 0;
        _firing = false;
        _burstFrame = 0;
        _shotsAtBurstStart = -1;
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
