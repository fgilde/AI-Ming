using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     Configuration for the OCR HUD-reader. Hosts the user-defined regions plus global toggles
///     (engine enable, polling interval). The engine itself reads <see cref="Enabled"/> +
///     <see cref="Regions"/> directly from <see cref="AppConfig.Current"/>.
/// </summary>
public class OcrSettings : BaseSettings
{
    private bool _enabled = false;
    private int _intervalMs = 500;
    private string _tessdataPath = "";
    private ObservableCollection<OcrRegion> _regions = new();

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public int IntervalMs
    {
        get => _intervalMs;
        set => SetField(ref _intervalMs, Math.Clamp(value, 100, 5000));
    }

    /// <summary>
    ///     Optional override for the Tesseract data folder. Empty = use the default
    ///     (<c>%LocalAppData%/PowerAim/tessdata</c>).
    /// </summary>
    public string TessdataPath
    {
        get => _tessdataPath;
        set => SetField(ref _tessdataPath, value);
    }

    public ObservableCollection<OcrRegion> Regions
    {
        get => _regions;
        set => SetField(ref _regions, value);
    }
}
