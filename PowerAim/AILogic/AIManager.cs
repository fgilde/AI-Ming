using System.Drawing;
using System.Windows;
using PowerAim;
using PowerAim.AILogic;
using PowerAim.AILogic.Actions;
using PowerAim.AILogic.Contracts;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.Models;
using Nextended.Core.Extensions;
using PowerAim.Other;
using PowerAim.Visuality;


public class AIManager : IDisposable
{
    public static AIManager Instance { get; private set; }
    private readonly IList<IAction> _actions;
    private bool _isAiLoopRunning;
    private readonly Thread _aiLoopThread;
    private bool _pausedNotified = false;
    private readonly FpsCapHelper _fpsCap = new();
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

        NotifyLoaded(true);

        _isAiLoopRunning = true;
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
        var w = Application.Current.MainWindow as MainWindow;
        w.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (AppConfig.Current.ToggleState.GlobalActive)
                w.SetActive(loaded);
            w.CallPropertyChanged(nameof(w.IsModelLoaded));
        }));
    }

    private async void AiLoop()
    {
        while (_isAiLoopRunning)
        {
            // Effective active = user wants it on AND Auto-Pause says we're focused on a game.
            // AutoPauseManager is a query-only helper; pausing here gates every action equally,
            // exactly like flipping GlobalActive off.
            if (AppConfig.Current.ToggleState.GlobalActive && PowerAim.Class.AutoPauseManager.ShouldBeActive())
            {
                if (_pausedNotified)
                {
                    _pausedNotified = false;
                    await SetActionsState(false);
                    await ImageCapture.OnResume();
                }
                var area = ImageCapture.CaptureArea;

                var cursorPosition = NativeAPIMethods.GetCursorPosition();

                var targetX = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.X - area.Left : area.Width / 2;
                var targetY = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.Y - area.Top : area.Height / 2;

                // The captured region is driven by the FOV size (centered on the aim point), NOT by
                // the model input resolution. PredictionLogic downscales the captured patch to the
                // model input before inference, so the FOV can be larger than the model resolution
                // and the AI then "sees" a wider slice of the screen. Clamp to the screen so the
                // centered box always fits. At FOV == model input this is identical to the old box.
                int fov = (int)Math.Round(AppConfig.Current.SliderSettings.ActualFovSize);
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
                    PowerAim.AILogic.ReplayBuffer.Instance.Push(frame, predictions);

                    await Task.WhenAll(_actions.Select(a => a.Execute(predictions)));
                    _consecutiveLoopErrors = 0;

                    // Spin guard: an ultra-cheap frame (no model loaded → Predict returns instantly,
                    // or a cached capture) must not busy-loop the thread now that the unconditional
                    // per-frame delay is gone. Real frames (several ms) skip this entirely.
                    if (sw.Elapsed.TotalMilliseconds < 1.0) await Task.Delay(1);
                }
                catch (Exception e)
                {
                    // Transient failures (a dropped capture on alt-tab, a device-lost frame, one bad
                    // inference) must NOT kill the session — count them and only give up when the loop
                    // is genuinely broken (many failures back-to-back). Previously ANY exception
                    // disposed the whole AI silently.
                    Console.WriteLine(e);
                    if (++_consecutiveLoopErrors < MaxConsecutiveLoopErrors)
                    {
                        await Task.Delay(50); // brief back-off so a hard-failing frame can't spin
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
                await Task.Delay(15);
            }
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
        IsModelLoaded = false;
        _isAiLoopRunning = false;
        // Never join our own thread: Dispose can be reached from inside the loop (fatal-error path),
        // and a self-join would just burn a second and then self-interrupt.
        if (_aiLoopThread is { IsAlive: true } && !ReferenceEquals(Thread.CurrentThread, _aiLoopThread))
        {
            if (!_aiLoopThread.Join(TimeSpan.FromSeconds(1)))
            {
                _aiLoopThread.Interrupt();
            }
        }
        foreach (var action in _actions)
        {
            action.Dispose();
        }
        _actions.Clear();
        //PredictionLogic.Dispose();
        ImageCapture?.Dispose();
    }
}