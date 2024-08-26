using System.Windows.Media;

namespace Aimmy2.Config;

public class ColorState : BaseSettings<Color>
{

    public Color FOVColor { get => Get(Colors.Aqua); set => Set(value); }

    public Color DetectedPlayerColor { get => Get(Colors.Red); set => Set(value); }
}