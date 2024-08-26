using System.Windows.Media;

namespace Aimmy2.Config;

public class ColorState : BaseSettings<Color>
{

    public Color FOVColor { get => Get(); set => Set(value); }

    public Color DetectedPlayerColor { get => Get(); set => Set(value); }
}