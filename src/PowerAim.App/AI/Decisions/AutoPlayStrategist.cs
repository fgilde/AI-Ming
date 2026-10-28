using PowerAim.AI.AutoPlay;
using PowerAim.Config;

namespace PowerAim.AI.Decisions;

/// <summary>What is steering the bot right now, for the AutoPlay decision panel.</summary>
public sealed record AutoPlayDecisionSnapshot(
    string Source,
    string Mode,
    string? Direction,
    string? Action,
    double? Confidence,
    IReadOnlyDictionary<string, double>? ModeProbabilities,
    double? Danger,
    double? LatencyMs,
    DateTime At,
    string? Error,
    IReadOnlyList<string>? Actions = null);

/// <summary>
///     Runs the profile's decision model in the background and hands the latest <see cref="AutoPlayIntent"/>
///     to the reflex. Model switches (profile edits, list edits) are picked up live; failures keep the
///     previous intent and show up in <see cref="Current"/> instead of silently changing behaviour.
/// </summary>
public sealed class AutoPlayStrategist : IDisposable
{
    private readonly Func<AutoPlayProfile?> _profile;
    private readonly Func<AutoPlayProfile, DecisionRequest> _request;
    private readonly Func<DecisionModelDefinition, IDecisionModel?> _create;
    private readonly object _lock = new();
    private readonly Queue<string> _recent = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private AutoPlayIntent _intent = AutoPlayIntent.Default;
    private IDecisionModel? _model;
    private string? _modelSignature;
    private bool _down; // model unreachable or threw: the loop backs off

    public AutoPlayStrategist(Func<AutoPlayProfile?> profile, Func<AutoPlayProfile, DecisionRequest> request,
        Func<DecisionModelDefinition, IDecisionModel?>? create = null)
    {
        _profile = profile;
        _request = request;
        _create = create ?? DecisionModelFactory.Create;
    }

    /// <summary>Latest decision for the panel. Reference assignment — readers always see a whole snapshot.</summary>
    public AutoPlayDecisionSnapshot? Current { get; private set; }

    /// <summary>Fired when the intent changes label (mode/direction), for the activity log.</summary>
    public event Action<string>? IntentChanged;

    public AutoPlayIntent Intent { get { lock (_lock) return _intent; } }

    /// <summary>The last few intent labels, oldest first (fed back to models for variety).</summary>
    public IReadOnlyList<string> RecentDecisions { get { lock (_lock) return _recent.ToArray(); } }

    public bool IsRunning => _loop is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning && _cts is { IsCancellationRequested: false }) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(() => LoopAsync(token));
    }

    /// <summary>Cancels the loop. An answer still in flight is dropped (checked before it is committed).</summary>
    public void Stop()
    {
        if (_cts == null || _cts.IsCancellationRequested) return; // already stopped
        try { _cts.Cancel(); } catch { /* already disposed */ }
        _loop = null;
        lock (_lock) _intent = AutoPlayIntent.Default;
        Current = new AutoPlayDecisionSnapshot("Heuristic", "default", null, null, null, null, null, null, DateTime.Now, null);
    }

    /// <summary>One decision step; the loop calls this, tests call it directly.</summary>
    public async Task StepAsync(CancellationToken ct)
    {
        var profile = _profile();
        var definition = profile == null ? null : DecisionModelRegistry.Resolve(profile, AppConfig.Current?.DecisionModels ?? []);
        if (profile == null || definition == null) return;

        var model = ModelFor(definition);
        _down = false;
        if (model == null)
        {
            // Heuristic: no strategic layer. Drop stale hints so the reflex runs on its own.
            lock (_lock) _intent = AutoPlayIntent.Default;
            Current = new AutoPlayDecisionSnapshot("Heuristic", "default", null, null, null, null, null, null, DateTime.Now, null);
            return;
        }

        if (!await model.IsAvailableAsync(ct))
        {
            lock (_lock) _intent = AutoPlayIntent.Default;
            _down = true;
            Fail(model.Source, model.LastError ?? "not reachable");
            return;
        }

        AutoPlayIntent? intent;
        try { intent = await model.DecideAsync(_request(profile), ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) { _down = true; Fail(model.Source, e.Message); return; }
        // Some models can't be cancelled mid-request: an answer arriving after Stop must not steer the bot.
        ct.ThrowIfCancellationRequested();

        if (intent == null)
        {
            Fail(model.Source, model.LastError ?? "no confident answer — keeping the previous intent");
            return;
        }

        bool changed;
        lock (_lock)
        {
            _intent = intent;
            changed = _recent.Count == 0 || _recent.Last() != intent.Label;
            _recent.Enqueue(intent.Label);
            while (_recent.Count > 5) _recent.Dequeue();
        }
        Current = new AutoPlayDecisionSnapshot(intent.Source, intent.Mode, intent.Direction, intent.ActionHint,
            intent.Confidence, intent.ModeProbabilities, intent.Danger, model.LastLatencyMs, intent.At, null, intent.Actions);
        if (changed) IntentChanged?.Invoke(intent.Label);
    }

    private IDecisionModel? ModelFor(DecisionModelDefinition definition)
    {
        lock (_lock)
        {
            if (_modelSignature != definition.Signature)
            {
                _model = _create(definition);
                _modelSignature = definition.Signature;
            }
            return _model;
        }
    }

    private void Fail(string source, string error)
    {
        var prev = Current;
        Current = new AutoPlayDecisionSnapshot(source, prev?.Mode ?? "default", prev?.Direction, prev?.Action,
            null, null, null, null, DateTime.Now, error);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try { await StepAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception e) { _down = true; Fail("AutoPlay", e.Message); }

                double interval = _profile()?.DecisionInterval ?? 1;
                // Back off while a model is down instead of hammering it.
                await Task.Delay(TimeSpan.FromSeconds(_down ? Math.Max(2, interval) : Math.Max(0.1, interval)), ct);
            }
        }
        catch (OperationCanceledException) { /* stopped */ }
    }

    public void Dispose() => Stop();
}
