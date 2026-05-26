using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     Configuration for the OCR HUD-reader. Hosts the user-defined regions plus global toggles
///     (engine enable, polling interval). The engine itself reads <see cref="Enabled"/> +
///     <see cref="Regions"/> directly from <see cref="AppConfig.Current"/>.
/// </summary>
public class OcrSettings : BaseSettings
{
    public bool Enabled
    {
        get;
        set => SetField(ref field, value);
    }

    public int IntervalMs
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 100, 5000));
    } = 500;

    /// <summary>
    ///     Optional override for the Tesseract data folder. Empty = use the default
    ///     (<c>%LocalAppData%/PowerAim/tessdata</c>).
    /// </summary>
    public string TessdataPath
    {
        get;
        set => SetField(ref field, value);
    } = "";

    public ObservableCollection<OcrRegion> Regions
    {
        get;
        set => SetField(ref field, value);
    } = new();
}
