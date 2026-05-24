namespace PowerAim.Config;

/// <summary>
///     Configuration for the AutoPlay learning subsystem. Capturing the user's playstyle is opt-in
///     (it requires keyboard hooks), and the resulting model is just a JSON file the user can
///     inspect or copy between machines.
/// </summary>
public class AutoPlayLearningSettings : BaseSettings
{
    private bool _recording = false;
    private bool _applyModel = false;
    private double _biasStrength = 0.5;
    private string _modelPath = "";
    private int _sampleIntervalMs = 150;

    /// <summary>While true, the recorder samples on every AI tick and accumulates state→action counts.</summary>
    public bool Recording
    {
        get => _recording;
        set => SetField(ref _recording, value);
    }

    /// <summary>When true, AutoPlay's action selector biases toward the recorded user preference.</summary>
    public bool ApplyModel
    {
        get => _applyModel;
        set => SetField(ref _applyModel, value);
    }

    /// <summary>
    ///     Weight blended into the AutoPlay selector. 0 = ignore the model, 1 = let it dominate.
    /// </summary>
    public double BiasStrength
    {
        get => _biasStrength;
        set => SetField(ref _biasStrength, Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    ///     Where the model is persisted. Empty = default
    ///     (<c>%LocalAppData%/PowerAim/autoplay_model.json</c>).
    /// </summary>
    public string ModelPath
    {
        get => _modelPath;
        set => SetField(ref _modelPath, value);
    }

    /// <summary>Minimum delay between recorded samples (ms). 150 ms ≈ 6 samples / sec.</summary>
    public int SampleIntervalMs
    {
        get => _sampleIntervalMs;
        set => SetField(ref _sampleIntervalMs, Math.Clamp(value, 50, 1000));
    }
}
