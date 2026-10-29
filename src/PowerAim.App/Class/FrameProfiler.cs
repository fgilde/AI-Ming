using System.Globalization;

namespace PowerAim.Class;

public enum FrameStage
{
    Capture,
    /// <summary>Resize, tensor, model run and post-processing.</summary>
    Inference,
    /// <summary>Aim, triggers, anti-recoil and the other per-frame actions.</summary>
    Actions,
    Total,
}

/// <summary>Per-stage frame times over the last <c>capacity</c> frames, with percentiles and slow-frame detection.</summary>
public sealed class FrameProfiler
{
    private const int MinBaselineFrames = 30;
    private readonly double[][] _ring;
    private readonly int _capacity;
    private int _count, _next;
    private readonly object _gate = new();

    public FrameProfiler(int capacity = 600)
    {
        _capacity = Math.Max(1, capacity);
        _ring = Enumerable.Range(0, 4).Select(_ => new double[_capacity]).ToArray();
    }

    /// <summary>Record one frame; true when its total is over twice the median of the frames before it.</summary>
    public bool Add(double captureMs, double inferenceMs, double actionsMs)
    {
        double total = captureMs + inferenceMs + actionsMs;
        lock (_gate)
        {
            bool slow = _count >= MinBaselineFrames && total > 2 * PercentileLocked(FrameStage.Total, 50);
            _ring[(int)FrameStage.Capture][_next] = captureMs;
            _ring[(int)FrameStage.Inference][_next] = inferenceMs;
            _ring[(int)FrameStage.Actions][_next] = actionsMs;
            _ring[(int)FrameStage.Total][_next] = total;
            _next = (_next + 1) % _capacity;
            _count = Math.Min(_count + 1, _capacity);
            return slow;
        }
    }

    public double Percentile(FrameStage stage, double p)
    {
        lock (_gate) return PercentileLocked(stage, p);
    }

    private double PercentileLocked(FrameStage stage, double p)
    {
        if (_count == 0) return 0;
        var values = _ring[(int)stage].AsSpan(0, _count).ToArray();
        Array.Sort(values);
        int index = (int)Math.Ceiling(p / 100.0 * _count) - 1;
        return values[Math.Clamp(index, 0, _count - 1)];
    }

    /// <summary>Median per stage plus the 95th percentile of the total, e.g. "cap 1.0 · inf 8.0 · act 2.0 · p95 11.0".</summary>
    public string Summary()
    {
        lock (_gate)
        {
            string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
            return $"cap {F(PercentileLocked(FrameStage.Capture, 50))} · inf {F(PercentileLocked(FrameStage.Inference, 50))} · " +
                   $"act {F(PercentileLocked(FrameStage.Actions, 50))} · p95 {F(PercentileLocked(FrameStage.Total, 95))}";
        }
    }
}
