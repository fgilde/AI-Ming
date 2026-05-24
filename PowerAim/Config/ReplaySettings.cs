namespace PowerAim.Config;

/// <summary>
///     Configuration for the in-memory replay buffer that <see cref="PowerAim.AILogic.ReplayBuffer"/>
///     uses. Defaults are tuned for ~3 seconds at 20 fps which gives the user enough context to
///     export a meaningful clip without hogging RAM.
/// </summary>
public class ReplaySettings : BaseSettings
{
    private bool _enabled = false;
    private int _bufferSeconds = 3;
    private int _jpegQuality = 70;
    private string _exportFolder = "";

    /// <summary>When false, <see cref="PowerAim.AILogic.ReplayBuffer.Push"/> is a no-op.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    /// <summary>Approximate number of seconds of history to retain.</summary>
    public int BufferSeconds
    {
        get => _bufferSeconds;
        set => SetField(ref _bufferSeconds, Math.Clamp(value, 1, 30));
    }

    /// <summary>JPEG quality (0..100). Lower = smaller buffer, more compression artefacts.</summary>
    public int JpegQuality
    {
        get => _jpegQuality;
        set => SetField(ref _jpegQuality, Math.Clamp(value, 10, 100));
    }

    /// <summary>
    ///     Optional override for the export folder. Empty = use
    ///     <c>%LocalAppData%/PowerAim/replays</c>.
    /// </summary>
    public string ExportFolder
    {
        get => _exportFolder;
        set => SetField(ref _exportFolder, value);
    }
}
