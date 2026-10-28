using System.Drawing;

namespace PowerAim.AI.AutoPlay;

/// <summary>An enemy as the bot sees it: offset from the crosshair and box size, in capture-area pixels.</summary>
public sealed record EnemyView(float Dx, float Dy, float Width, float Height)
{
    public float Distance => MathF.Sqrt(Dx * Dx + Dy * Dy);
}

/// <summary>What the detector shows this frame, reduced to what AutoPlay decides on.</summary>
public readonly record struct AutoPlayPerception(int EnemyCount, EnemyView? Nearest, Size Area)
{
    /// <summary>
    ///     Picks the enemy nearest the crosshair (the capture-area centre). Boxes come in capture-area
    ///     pixels (<see cref="Prediction.TranslatedRectangle"/>).
    /// </summary>
    public static AutoPlayPerception Perceive(IReadOnlyList<Prediction> predictions, Size area)
    {
        float cx = area.Width / 2f, cy = area.Height / 2f;
        EnemyView? nearest = null;
        float best = float.MaxValue;
        foreach (var p in predictions)
        {
            var c = p.ScreenCenter;
            float dx = c.X - cx, dy = c.Y - cy, d = dx * dx + dy * dy;
            if (d >= best) continue;
            best = d;
            nearest = new EnemyView(dx, dy, p.TranslatedRectangle.Width, p.TranslatedRectangle.Height);
        }
        return new AutoPlayPerception(predictions.Count, nearest, area);
    }

    /// <summary>Enemy box height as a fraction of the capture height: a rough "how close is it".</summary>
    public float NearestSize => Nearest == null || Area.Height <= 0 ? 0 : Nearest.Height / Area.Height;
}
