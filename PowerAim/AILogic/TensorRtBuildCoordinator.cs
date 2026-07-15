namespace PowerAim.AILogic;

/// <summary>
///     Bridges the low-level ONNX session factory — which is the only place that knows a TensorRT engine
///     build is starting or finishing — to the UI, WITHOUT the factory taking a dependency on WPF. The UI
///     (MainWindow) subscribes so it can show a progress dialog while the (potentially minutes-long, and
///     completely silent) first-time engine build runs.
///     <para>Both events fire on the build's background thread; subscribers must marshal to the UI.</para>
/// </summary>
public static class TensorRtBuildCoordinator
{
    /// <summary>Raised right before a TensorRT session build begins. Argument: the model file name.</summary>
    public static event Action<string>? BuildStarted;

    /// <summary>Raised when a TensorRT session build ends — whether it succeeded or threw.</summary>
    public static event Action? BuildFinished;

    public static void NotifyStarted(string modelName)
    {
        try { BuildStarted?.Invoke(modelName); } catch { /* a UI hiccup must never break model loading */ }
    }

    public static void NotifyFinished()
    {
        try { BuildFinished?.Invoke(); } catch { /* same — never let UI notification break the load */ }
    }
}
