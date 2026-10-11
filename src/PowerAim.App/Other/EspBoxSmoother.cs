using System.Drawing;
using PowerAim.AI;

namespace PowerAim.Other;

/// <summary>
/// Temporal box smoother for ESP overlays. Associates detections frame-to-frame
/// by centre proximity and applies EMA to box edges so GDI/WPF boxes stop jittering.
/// Identity is soft (no full tracker required) — works with raw YOLO frames and
/// with StrongSORT tracks.
/// </summary>
internal static class EspBoxSmoother
{
    private sealed class Slot
    {
        public float X, Y, W, H;
        public int Age;
        public int Missed;
        public float Confidence;
        public int ClassId;
        public string ClassName = "Enemy";
        public bool Locked;
    }

    private static readonly List<Slot> _slots = new();
    private static readonly object _gate = new();

    /// <summary>EMA blend for position/size. Higher = snappier, lower = smoother.</summary>
    public static float Alpha { get; set; } = 0.45f;

    /// <summary>Match radius as fraction of max(box size, 40px).</summary>
    public static float MatchScale { get; set; } = 1.25f;

    public static int MaxMissed { get; set; } = 8;

    /// <summary>
    /// Smooth a set of detections. Returns rectangles in the same coordinate space
    /// as <see cref="Prediction.TranslatedRectangle"/> (or Rectangle if translated empty).
    /// </summary>
    public static IReadOnlyList<(RectangleF Box, float Confidence, int ClassId, string ClassName, bool Locked)>
        Smooth(IEnumerable<Prediction> predictions, int? lockedClassHint = null, bool useScreenSpace = true)
    {
        var dets = predictions?.ToList() ?? new List<Prediction>();
        lock (_gate)
        {
            foreach (var s in _slots) s.Missed++;

            var used = new HashSet<int>();
            foreach (var p in dets)
            {
                // Screen space → TranslatedRectangle (already includes capture origin).
                // Capture/form-local → model Rectangle (drawers may add capture origin once).
                RectangleF src;
                if (useScreenSpace)
                    src = p.TranslatedRectangle.Width > 1 ? p.TranslatedRectangle : p.Rectangle;
                else
                    src = p.Rectangle.Width > 1 ? p.Rectangle : p.TranslatedRectangle;
                if (src.Width <= 1 || src.Height <= 1) continue;

                float cx = src.X + src.Width * 0.5f;
                float cy = src.Y + src.Height * 0.5f;

                int best = -1;
                float bestD = float.PositiveInfinity;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (used.Contains(i)) continue;
                    var s = _slots[i];
                    float scx = s.X + s.W * 0.5f;
                    float scy = s.Y + s.H * 0.5f;
                    float d = MathF.Sqrt((cx - scx) * (cx - scx) + (cy - scy) * (cy - scy));
                    float scale = Math.Max(40f, Math.Max(s.W, s.H)) * MatchScale;
                    if (d < scale && d < bestD) { bestD = d; best = i; }
                }

                if (best >= 0)
                {
                    used.Add(best);
                    var s = _slots[best];
                    float a = Alpha;
                    s.X = s.X * (1 - a) + src.X * a;
                    s.Y = s.Y * (1 - a) + src.Y * a;
                    s.W = s.W * (1 - a) + src.Width * a;
                    s.H = s.H * (1 - a) + src.Height * a;
                    s.Confidence = p.Confidence;
                    s.ClassId = p.ClassId;
                    s.ClassName = p.ClassName ?? s.ClassName;
                    s.Missed = 0;
                    s.Age++;
                }
                else
                {
                    _slots.Add(new Slot
                    {
                        X = src.X,
                        Y = src.Y,
                        W = src.Width,
                        H = src.Height,
                        Confidence = p.Confidence,
                        ClassId = p.ClassId,
                        ClassName = p.ClassName ?? "Enemy",
                        Age = 1,
                        Missed = 0
                    });
                }
            }

            _slots.RemoveAll(s => s.Missed > MaxMissed);

            var result = new List<(RectangleF, float, int, string, bool)>(_slots.Count);
            foreach (var s in _slots)
            {
                if (s.Age < 1) continue;
                result.Add((new RectangleF(s.X, s.Y, s.W, s.H), s.Confidence, s.ClassId, s.ClassName, s.Locked));
            }
            return result;
        }
    }

    public static void MarkLocked(float worldX, float worldY, float radius = 80f)
    {
        lock (_gate)
        {
            foreach (var s in _slots)
            {
                float cx = s.X + s.W * 0.5f;
                float cy = s.Y + s.H * 0.5f;
                float d = MathF.Sqrt((worldX - cx) * (worldX - cx) + (worldY - cy) * (worldY - cy));
                s.Locked = d <= radius;
            }
        }
    }

    public static void Clear()
    {
        lock (_gate) _slots.Clear();
    }
}