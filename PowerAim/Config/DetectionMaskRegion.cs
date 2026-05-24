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
    private string _name = "Mask";
    private double _x = 0.0;
    private double _y = 0.0;
    private double _width = 0.2;
    private double _height = 0.1;
    private bool _enabled = true;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    /// <summary>Normalized left edge (0..1).</summary>
    public double X
    {
        get => _x;
        set => SetField(ref _x, System.Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>Normalized top edge (0..1).</summary>
    public double Y
    {
        get => _y;
        set => SetField(ref _y, System.Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>Normalized width (0..1).</summary>
    public double Width
    {
        get => _width;
        set => SetField(ref _width, System.Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>Normalized height (0..1).</summary>
    public double Height
    {
        get => _height;
        set => SetField(ref _height, System.Math.Clamp(value, 0.0, 1.0));
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    /// <summary>
    ///     True if <paramref name="x"/> / <paramref name="y"/> (image-space pixels) fall inside the
    ///     region after expanding the normalized rect to <paramref name="imageSize"/>.
    /// </summary>
    public bool Contains(float x, float y, int imageSize)
    {
        if (!_enabled || imageSize <= 0) return false;
        double left = _x * imageSize;
        double top = _y * imageSize;
        double right = left + _width * imageSize;
        double bottom = top + _height * imageSize;
        return x >= left && x <= right && y >= top && y <= bottom;
    }
}
