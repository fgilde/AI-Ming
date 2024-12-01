using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Aimmy2.Config;

public class ColorState : BaseSettings<Color>
{

    public Color FOVColor { get => Get(Colors.Aqua); set => Set(value); }

    public Color CapturedAreaBorderColor
    {
        get => Get(Colors.DarkOrange);
        set
        {
            Set(value);
            OnPropertyChanged(nameof(ActiveCapturedAreaBorderBrush));
        }
    }

    public Color DetectedPlayerColor { get => Get(Colors.Red); set => Set(value); }

    [Newtonsoft.Json.JsonIgnore]
    [JsonIgnore]
    public Brush ActiveCapturedAreaBorderBrush => AppConfig.Current.ToggleState.ShowCapturedArea ? new SolidColorBrush(CapturedAreaBorderColor) : Brushes.Transparent;
}