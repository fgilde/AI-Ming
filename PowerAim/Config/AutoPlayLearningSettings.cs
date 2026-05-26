namespace PowerAim.Config;

/// <summary>
///     Configuration for the AutoPlay learning subsystem. Capturing the user's playstyle is opt-in
///     (it requires keyboard hooks), and the resulting model is just a JSON file the user can
///     inspect or copy between machines.
/// </summary>
public class AutoPlayLearningSettings : BaseSettings
{
    /// <summary>While true, the recorder samples on every AI tick and accumulates state→action counts.</summary>
    public bool Recording
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>When true, AutoPlay's action selector biases toward the recorded user preference.</summary>
    public bool ApplyModel
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    ///     Weight blended into the AutoPlay selector. 0 = ignore the model, 1 = let it dominate.
    /// </summary>
    public double BiasStrength
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 0.0, 1.0));
    } = 0.5;

    /// <summary>
    ///     Where the model is persisted. Empty = default
    ///     (<c>%LocalAppData%/PowerAim/autoplay_model.json</c>).
    /// </summary>
    public string ModelPath
    {
        get;
        set => SetField(ref field, value);
    } = "";

    /// <summary>Minimum delay between recorded samples (ms). 150 ms ≈ 6 samples / sec.</summary>
    public int SampleIntervalMs
    {
        get;
        set => SetField(ref field, Math.Clamp(value, 50, 1000));
    } = 150;
}
