namespace PowerAim.AILogic;

/// <summary>
///     Tiny allocation-light pub/sub for "the detector just finished a frame and saw N targets".
///     Modeled on <see cref="PowerAim.InputLogic.InputEventBus"/>. The crosshair overlay
///     subscribes to drive the detection-flash effect; <see cref="AIManager"/> reports once per
///     inference tick. No gate — the event is fired unconditionally and listeners are expected
///     to be cheap or self-gate (the crosshair listener self-gates on
///     <see cref="Config.CrosshairSettings.DetectionFlashEnabled"/>).
/// </summary>
public static class DetectionEventBus
{
    /// <summary>Fires once per inference tick with the number of targets seen this frame.</summary>
    public static event Action<Prediction[]>? Detected;

    public static void Report(Prediction[] predictions)
    {
        try { Detected?.Invoke(predictions); }
        catch { /* a flaky listener must never break the detection loop */ }
    }
}
