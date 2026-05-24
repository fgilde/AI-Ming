using System.Collections.ObjectModel;

namespace PowerAim.Config;

/// <summary>
///     A single sample of a recorded recoil pattern. Stored in chronological order on a
///     <see cref="RecoilPattern"/>. <see cref="DeltaX"/> / <see cref="DeltaY"/> are the screen-pixel
///     drift measured by phase correlation between two captured frames — positive Y means the view
///     drifted upward (gun kicked up) and the playback action should compensate with positive Y
///     mouse input to push the aim back down.
/// </summary>
public class RecoilSample
{
    public int TimeMs { get; set; }
    public double DeltaX { get; set; }
    public double DeltaY { get; set; }
}

/// <summary>
///     A named library entry holding the per-frame drift of a weapon's recoil. Recorded by
///     <c>RecoilPatternRecorder</c>, replayed by <c>RecoilPatternPlaybackAction</c>.
/// </summary>
public class RecoilPattern : BaseSettings
{
    private string _name = "Pattern";
    private string _weapon = "";
    private ObservableCollection<RecoilSample> _samples = new();

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    /// <summary>Free-text weapon label (e.g. "AK-47", "Vandal"). Purely informational.</summary>
    public string Weapon
    {
        get => _weapon;
        set => SetField(ref _weapon, value);
    }

    public ObservableCollection<RecoilSample> Samples
    {
        get => _samples;
        set => SetField(ref _samples, value);
    }

    /// <summary>Total recorded duration in ms. Used for the UI display.</summary>
    public int DurationMs => _samples.Count == 0 ? 0 : _samples[^1].TimeMs;
}
