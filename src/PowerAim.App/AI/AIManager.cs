using System.Drawing;
using System.Windows;
using PowerAim;
using PowerAim.AI;
using PowerAim.AI.Actions;
using PowerAim.AI.Contracts;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Models;
using Nextended.Core.Extensions;
using PowerAim.Other;
using PowerAim.UI.Visuality;


public class AIManager : IDisposable
{
    private static volatile AIManager? _instance;
    public static AIManager? Instance
    {
        get => _instance;
        private set => _instance = value;
    }
    private readonly IList<IAction> _actions;
    private bool _isAiLoopRunning;
    private readonly Thread _aiLoopThread;
    private bool _pausedNotified = false;
    private readonly FpsCapHelper _fpsCap = new();
    private CancellationTokenSource? _loopCts;
    private int _consecutiveLoopErrors;
    /// <summary>Give up (dispose the AI) only after this many frames failed back-to-back — a single
    /// transient error (e.g. DXGI device-lost on alt-tab) must not kill the whole session.</summary>
    private const int MaxConsecutiveLoopErrors = 10;
    public bool IsRunning => _isAiLoopRunning;
    public bool IsModelLoaded { get; private set; }

    public IPredictionLogic PredictionLogic { get; private set; }
    public ICapture ImageCapture { get; private set; }

    public AIManager(string modelPath) : this(new ScreenCapture(), new PredictionLogic(modelPath), BaseAction.AllActions())
    { }

    public AIManager(string modelPath, CaptureSource target) : this(CreateScreenCapture(target), new PredictionLogic(modelPath), BaseAction.AllActions())
    { }

    /// <summary>
    ///     Build an AIManager without freezing the caller's (UI) thread: the potentially slow inference
    ///     session — a TensorRT engine build can take 30 s–2 min the first time — is created on a
    ///     background thread. The screen capture and the action list are built back on the caller's
    ///     thread (they may touch WPF), so only the ONNX session is offloaded.
    /// </summary>
    public static async Task<AIManager> CreateAsync(string modelPath, CaptureSource target)
    {
        IPredictionLogic predictionLogic = await Task.Run(() => (IPredictionLogic)new PredictionLogic(modelPath));
        return new AIManager(CreateScreenCapture(target), predictionLogic, BaseAction.AllActions());
    }

    internal static ICapture CreateScreenCapture(CaptureSource target)
    {
        try
        {
            return target.TargetType switch
            {
                CaptureTargetType.Screen => target.ProcessOrScreenId.HasValue ? new ScreenCapture(target.ProcessOrScreenId.Value) : new ScreenCapture(),
                CaptureTargetType.Process => new ProcessCapture(ProcessModel.FindProcessById(target.ProcessOrScreenId ?? 0) ?? ProcessModel.FindProcessByTitle(target.Title)),
                _ => throw new ArgumentException("Unsupported RecordTargetType"),
            };
        }
        catch (Exception e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => new NoticeBar(string.Format(Locale.ErrorFormat, e.Message), 5000).Show()));
            throw;
        }
    }

    public AIManager(ICapture screenCapture, IPredictionLogic predictionLogic, IList<IAction> actions)
    {
        Instance = this;
        ImageCapture = screenCapture;
        PredictionLogic = predictionLogic;
        _actions = actions.Apply(a =>
        {
            a.AIManager = this;
        }).ToList();

        // Reflect the ACTUAL load result — false when every provider (incl. the DirectML fallback)
        // failed, so the UI shows the empty-state / error instead of pretending a model is ready.
        NotifyLoaded(predictionLogic.IsLoaded);
        AppDebugLog.Info("AIManager", $"Created IsModelLoaded={IsModelLoaded}");

        _isAiLoopRunning = true;
        _loopCts = new CancellationTokenSource();
        _ = SetActionsState(false);
        _aiLoopThread = new Thread(AiLoop)
        {
            Priority = ThreadPriority.AboveNormal,
            IsBackground = true
        };
        _aiLoopThread.Start();
    }

    private void NotifyLoaded(bool loaded)
    {
        IsModelLoaded = loaded;
        var w = Application.Current?.MainWindow as MainWindow;
        if (w == null) return;
        w.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (AppConfig.Current?.ToggleState?.GlobalActive == true)
                w.SetActive(loaded);
            w.CallPropertyChanged(nameof(w.IsModelLoaded));
        }));
    }

    private async void AiLoop()
    {
        // async void: any exception that escapes this method becomes UnhandledException and can
        // terminate the process. TaskCanceledException/OperationCanceledException are expected when
        // Dispose cancels _loopCts during a model switch — swallow them and exit cleanly.
        try
        {
        while (_isAiLoopRunning)
        {
            // Effective active = user wants it on AND Auto-Pause says we're focused on a game.
            // AutoPauseManager is a query-only helper; pausing here gates every action equally,
            // exactly like flipping GlobalActive off.
            var cfg = AppConfig.Current;
            if (cfg?.ToggleState == null)
            {
                await Task.Delay(50, _loopCts?.Token ?? CancellationToken.None);
                continue;
            }
            if (cfg.ToggleState.GlobalActive && PowerAim.Class.AutoPauseManager.ShouldBeActive())
            {
                if (_pausedNotified)
                {
                    _pausedNotified = false;
                    await SetActionsState(false);
                    await ImageCapture.OnResume();
                }
                var area = ImageCapture.CaptureArea;

                var cursorPosition = NativeAPIMethods.GetCursorPosition();

                var targetX = cfg.DropdownState?.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.X - area.Left : area.Width / 2;
                var targetY = cfg.DropdownState?.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.Y - area.Top : area.Height / 2;

                // The captured region is driven by the FOV size (centered on the aim point), NOT by
                // the model input resolution. PredictionLogic downscales the captured patch to the
                // model input before inference, so the FOV can be larger than the model resolution
                // and the AI then "sees" a wider slice of the screen. Clamp to the screen so the
                // centered box always fits. At FOV == model input this is identical to the old box.
                int fov = (int)Math.Round(cfg.SliderSettings?.ActualFovSize ?? 640);
                int maxCapture = Math.Max(16, Math.Min(area.Width, area.Height));
                int captureSize = Math.Clamp(fov, 16, maxCapture);
                Rectangle detectionBox = new(targetX - captureSize / 2, targetY - captureSize / 2, captureSize, captureSize);
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var frame = ImageCapture.Capture(detectionBox);
                    var predictions = (await PredictionLogic.Predict(frame, detectionBox)).ToArray();
                    sw.Stop();

                    // Telemetry: feed the Stats / Debug-Overlay layer. Cheap, lock-free counters.
                    var stats = PowerAim.Class.SessionStats.Instance;
                    stats.LastInferenceMs = sw.Elapsed.TotalMilliseconds;
                    stats.LastDetectionCount = predictions.Length;
                    stats.AddDetections(predictions.Length);
                    stats.IncrementFrames();
                    // Detection-flash cue + any other per-frame UI signal. Cheap when no listeners.
                    DetectionEventBus.Report(predictions);
                    // Keep the latest/best target readable from outside the loop (custom-tool vars).
                    DetectionState.Set(predictions);
                    // Rolling FPS: smoothed via simple EMA so the overlay number stops jittering.
                    var instMs = sw.Elapsed.TotalMilliseconds;
                    if (instMs > 0.01)
                    {
                        var instFps = 1000.0 / instMs;
                        stats.InstantFps = stats.InstantFps * 0.85 + instFps * 0.15;
                    }

                    // Push into the rolling replay buffer. The buffer self-gates on
                    // ReplaySettings.Enabled so this is a cheap no-op when disabled.
                    PowerAim.AI.ReplayBuffer.Instance.Push(frame, predictions);

                    await Task.WhenAll(_actions.Select(a => a.Execute(predictions)));
                    _consecutiveLoopErrors = 0;

                    // Spin guard: an ultra-cheap frame (no model loaded → Predict returns instantly,
                    // or a cached capture) must not busy-loop the thread now that the unconditional
                    // per-frame delay is gone. Real frames (several ms) skip this entirely.
                    if (sw.Elapsed.TotalMilliseconds < 1.0) await Task.Delay(1, _loopCts?.Token ?? CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    // Dispose cancelled the CTS mid-frame — exit the loop cleanly.
                    break;
                }
                catch (Exception e)
                {
                    // Transient failures (a dropped capture on alt-tab, a device-lost frame, one bad
                    // inference) must NOT kill the session — count them and only give up when the loop
                    // is genuinely broken (many failures back-to-back). Previously ANY exception
                    // disposed the whole AI silently.
                    Console.WriteLine(e);
                    AppDebugLog.Exception("AILoop", e, $"consecutive={_consecutiveLoopErrors + 1}");
                    if (++_consecutiveLoopErrors < MaxConsecutiveLoopErrors)
                    {
                        try { await Task.Delay(50, _loopCts?.Token ?? CancellationToken.None); }
                        catch (OperationCanceledException) { break; }
                        continue;
                    }

                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        new NoticeBar(string.Format(Locale.ErrorFormat, e.Message), 6000).Show()));
                    Instance?.Dispose();
                    FileManager.AIManager = null;
                    MainWindow.Instance?.OnPropertyChanged(nameof(IsModelLoaded));
                }

                // Optional inference FPS cap — when SliderSettings.MaxInferenceFPS > 0 the helper
                // sleeps just long enough to keep the loop at-or-below the configured rate. When
                // the cap is 0 (the default) this is a no-op that just resets the stopwatch, so
                // the existing 1ms Task.Delay below still governs idle pacing.
                await _fpsCap.WaitForNextFrameAsync(AppConfig.Current.SliderSettings.MaxInferenceFPS);
            }
            else
            {
                if (!_pausedNotified)
                {
                    _pausedNotified = true;
                    await SetActionsState(true);
                    await ImageCapture.OnPause();
                    _fpsCap.Reset();
                }
                // Idle pacing belongs to the paused branch only. In the active branch the inference
                // itself (plus the optional FPS cap) paces the loop — the old unconditional
                // Task.Delay(1) cost 1-15ms EVERY frame and capped the inference FPS.
                await Task.Delay(15, _loopCts?.Token ?? CancellationToken.None);
            }
        }
        }
        catch (OperationCanceledException)
        {
            // Expected on Dispose / model switch — do not treat as fatal.
            AppDebugLog.Debug("AILoop", "Cancelled (model switch or shutdown)");
        }
        catch (Exception ex)
        {
            AppDebugLog.Exception("AILoop", ex, "unhandled in AiLoop");
        }
    }

    private async Task SetActionsState(bool paused)
    {
        try
        {
            await Task.WhenAll(_actions.Select(a => paused ? a?.OnPause() : a?.OnResume()).ToArray());
        }
        catch (Exception)
        {
        }
    }


    public void Dispose()
    {
        AppDebugLog.Info("AIManager", "Dispose begin");
        IsModelLoaded = false;
        _isAiLoopRunning = false;
        try { _loopCts?.Cancel(); } catch { /* ignore */ }
        // Never join our own thread: Dispose can be reached from inside the loop (fatal-error path).
        // Do NOT Thread.Interrupt — unsafe with async state machines / ORT; rely on CTS + flag.
        // Wait long enough for an in-flight inference frame to finish before disposing the session.
        if (_aiLoopThread is { IsAlive: true } && !ReferenceEquals(Thread.CurrentThread, _aiLoopThread))
        {
            if (!_aiLoopThread.Join(TimeSpan.FromSeconds(3)))
                System.Diagnostics.Debug.WriteLine("[AIManager] AI loop did not exit within 3s; abandoning join.");
        }
        try { _loopCts?.Dispose(); } catch { /* ignore */ }
        _loopCts = null;
        foreach (var action in _actions)
        {
            try { action.Dispose(); } catch { /* ignore */ }
        }
        _actions.Clear();
        // PredictionLogic last among consumers: session gate waits for any remaining Run.
        try { PredictionLogic?.Dispose(); } catch { /* ignore */ }
        try { ImageCapture?.Dispose(); } catch { /* ignore */ }
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }
}