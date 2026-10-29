namespace PowerAim.Config;

/// <summary>
///     Per-class aim rule of an <see cref="AimProfile"/>: whether the class is aimed at at all, which
///     class wins when several are visible, the minimum confidence and where inside its box to aim.
/// </summary>
public class ClassTargetSpec
{
    public int ClassId { get; set; }

    /// <summary>Class name from the model, for display only.</summary>
    public string Name { get; set; } = "";

    /// <summary>Lower = preferred. When several classes are visible only the best one is aimed at; -1 = no priority (fallback).</summary>
    public int Priority { get; set; } = -1;

    /// <summary>Never aim at this class (teammates, friendly markers).</summary>
    public bool Ignored { get; set; }

    /// <summary>Minimum confidence for this class; 0 = the global threshold only.</summary>
    public float MinConfidence { get; set; }

    /// <summary>Aim at <see cref="AnchorX"/>/<see cref="AnchorY"/> instead of the profile's aim region.</summary>
    public bool UseAnchor { get; set; }

    /// <summary>Aim point inside the box as a 0..1 fraction (0.5/0.5 = centre, 0.5/0.25 = head height).</summary>
    public float AnchorX { get; set; } = 0.5f;

    public float AnchorY { get; set; } = 0.5f;

    /// <summary>Extra shift of the anchor in model pixels (e.g. a marker drawn above the body).</summary>
    public int FlatOffsetX { get; set; }

    public int FlatOffsetY { get; set; }
}

/// <summary>Which body part a pose model's keypoints aim at.</summary>
public enum PoseAimTarget
{
    Off,
    Head,
    Chest,
}
