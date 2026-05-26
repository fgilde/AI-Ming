namespace PowerAim.Config;

/// <summary>
///     Configuration for the in-memory replay buffer that <see cref="PowerAim.AILogic.ReplayBuffer"/>
///     uses. Defaults are tuned for ~3 seconds at 20 fps which gives the user enough context to
///     export a meaningful clip without hogging RAM.
/// </summary>
public class ReplaySettings : BaseSettings
{
    /// <summary>When false, <see cref="PowerAim.AILogic.ReplayBuffer.Push"/> is a no-op.</summary>
    public bool Enabled
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>Approximate number of seconds of history to retain.</summary>
    public int BufferSeconds
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 1, 30));
    } = 3;

    /// <summary>JPEG quality (0..100). Lower = smaller buffer, more compression artefacts.</summary>
    public int JpegQuality
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 10, 100));
    } = 70;

    /// <summary>
    ///     Optional override for the export folder. Empty = use
    ///     <c>%LocalAppData%/PowerAim/replays</c>.
    /// </summary>
    public string ExportFolder
    {
        get;
        set => SetField(ref field, value);
    } = "";
}
