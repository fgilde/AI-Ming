namespace PowerAim.Config;

/// <summary>
///     A rectangular region inside the detection box that <see cref="PowerAim.AILogic.PredictionFilter"/>
///     uses to discard detections whose centre falls inside it. Coordinates are stored in normalized
///     image-space (0..1) so the mask survives image-size / FOV changes without re-editing.
///     <para>
///     Typical usage: blanking out HUD elements (ammo, minimap, kill feed) that some YOLO models
///     hallucinate enemies into. Enabled is per-region so a profile can keep a library of masks
///     without forcing them all on at once.
///     </para>
/// </summary>
public class DetectionMaskRegion : BaseSettings
{
    public string Name
    {
        get;
        set => SetField(ref field, value);
    } = "Mask";

    /// <summary>Normalized left edge (0..1).</summary>
    public double X
    {
        get;
        set => SetField(ref field, System.Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>Normalized top edge (0..1).</summary>
    public double Y
    {
        get;
        set => SetField(ref field, System.Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>Normalized width (0..1).</summary>
    public double Width
    {
        get;
        set => SetField(ref field, System.Math.Clamp(value, 0.0, 1.0));
    } = 0.2;

    /// <summary>Normalized height (0..1).</summary>
    public double Height
    {
        get;
        set => SetField(ref field, System.Math.Clamp(value, 0.0, 1.0));
    } = 0.1;

    public bool Enabled
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>
    ///     True if <paramref name="x"/> / <paramref name="y"/> (image-space pixels) fall inside the
    ///     region after expanding the normalized rect to <paramref name="imageSize"/>.
    /// </summary>
    public bool Contains(float x, float y, int imageSize)
    {
        if (!Enabled || imageSize <= 0) return false;
        double left = X * imageSize;
        double top = Y * imageSize;
        double right = left + Width * imageSize;
        double bottom = top + Height * imageSize;
        return x >= left && x <= right && y >= top && y <= bottom;
    }
}
