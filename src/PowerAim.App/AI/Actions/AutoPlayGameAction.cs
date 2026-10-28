using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using PowerAim.AI.AutoPlay;
using PowerAim.AI.Decisions;
using PowerAim.Config;
using PowerAim.Core.Jev;

namespace PowerAim.AI.Actions;

/// <summary>
///     Single entry in the AutoPlay activity ring buffer surfaced in the Debug Overlay.
///     <see cref="Category"/> is one of "Intent", "Tactical", "OCR", "Combat", "Stuck".
/// </summary>
public sealed record AutoPlayLogEntry(DateTime At, string Category, string Detail);

/// <summary>
///     AutoPlay, wired together. Every frame: <see cref="AutoPlayPerception"/> reads the detections, the
///     <see cref="AutoPlayReflex"/> decides what to hold and where to look, the <see cref="AutoPlayActuator"/>
///     presses the profile's keys, <see cref="AutoPlayTactics"/> fires abilities. In the background the
///     <see cref="AutoPlayStrategist"/> asks the profile's decision model (Jev, Ollama, learned, …) for the
///     plan the reflex follows. Aim and AutoTrigger stay independent features; when they run alongside,
///     AutoPlay hands them the mouse / the shoot keys (<see cref="AutoPlayCoordination"/>).
/// </summary>
public class AutoPlayGameAction : BaseAction
{
    private const int ActivityLogCapacity = 20;
    private static readonly TimeSpan StuckSampleInterval = TimeSpan.FromMilliseconds(250);

    private static readonly ConcurrentQueue<AutoPlayLogEntry> _activityLog = new();
    private static AutoPlayStrategist? _shownStrategist;

    /// <summary>Snapshot of the most recent decisions/actions, oldest first. Thread-safe.</summary>
    public static IReadOnlyList<AutoPlayLogEntry> RecentEntries => _activityLog.ToArray();

    /// <summary>What is steering the bot right now. Null until AutoPlay has run once.</summary>
    public static AutoPlayDecisionSnapshot? CurrentDecision => _shownStrategist?.Current;

    private readonly Random _rng = new();
    private readonly AutoPlayReflex _reflex;
    private readonly AutoPlayActuator _actuator;
    private readonly AutoPlayTactics _tactics;
    private readonly StuckDetector _stuck = new();
    private readonly AutoPlayStrategist _strategist;

    private AutoPlayProfile? _cachedProfile;
    private volatile AutoPlayActionMap? _map;
    private bool _mapDirty = true;
    private volatile DecisionSituation? _situation;
    private DateTime _nextStuckSample = DateTime.MinValue;
    private bool _moving;

    public AutoPlayGameAction()
    {
        _reflex = new AutoPlayReflex(_rng) { LearnedDirection = LearnedDirection };
        _reflex.Event += AddLogEntry;
        _actuator = new AutoPlayActuator(rng: _rng);
        _tactics = new AutoPlayTactics(_rng) { LearnedPreference = () => LearnedPreferred(_reflex.StateLabel(true)) };
        _strategist = new AutoPlayStrategist(GetActiveProfile, BuildRequest);
        _strategist.IntentChanged += label => AddLogEntry("Intent", label);
        _shownStrategist = _strategist;
        AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChanged;
    }

    public override bool Active => base.Active && AppConfig.Current.ToggleState.AutoPlay;

    public override Task OnPause() { ReleaseEverything(); _strategist.Stop(); return base.OnPause(); }
    public override Task OnResume() { if (Active) _strategist.Start(); return base.OnResume(); }

    public override void Dispose()
    {
        ReleaseEverything();
        _strategist.Dispose();
        UnsubscribeFromProfile();
        AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChanged;
        base.Dispose();
    }

    private void OnToggleStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ToggleState.AutoPlay)) return;
        if (AppConfig.Current.ToggleState.AutoPlay) _strategist.Start();
        else { ReleaseEverything(); _strategist.Stop(); }
    }

    // ============================================================================ TICK ====

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active) { ReleaseEverything(); _strategist.Stop(); return Task.CompletedTask; }

        var profile = GetActiveProfile();
        // No profile (e.g. the game lost focus with per-process profiles): let go of everything we hold.
        if (profile == null) { ReleaseEverything(); return Task.CompletedTask; }
        EnsureSubscribed(profile);
        if (_mapDirty || _map == null)
        {
            // Profile switched or its actions edited: release first, the old map's keys may not be in the new one.
            ReleaseEverything();
            _map = AutoPlayActionMap.Build(profile);
            _mapDirty = false;
        }
        var map = _map;
        _strategist.Start();

        var now = DateTime.Now;
        var seen = AutoPlayPerception.Perceive(predictions, ImageCapture.CaptureArea.Size);
        var (ammo, health) = HudReader.Read();
        bool stuck = seen.Nearest == null && SampleStuck(now);

        var ctx = new ReflexContext(
            AimHandledExternally: AppConfig.Current.ToggleState.AimAssist,
            FireHandledExternally: AutoPlayCoordination.TriggerOwnsFire(AppConfig.Current.Triggers, map.Get(ControlRole.Shoot)),
            Ammo: ammo, Health: health, Stuck: stuck);

        var intent = _strategist.Intent;
        var cmd = _reflex.Tick(seen, intent, ctx, now);
        _actuator.Apply(cmd, map, profile, now);
        _moving = cmd.Hold.Overlaps(MoveRoles);

        _tactics.LearnedBias = AppConfig.Current.AutoPlayLearningSettings?.ApplyModel == true
            ? Math.Clamp(AppConfig.Current.AutoPlayLearningSettings.BiasStrength, 0, 1) : 0;
        foreach (var action in _tactics.Next(_reflex.Effective(intent, now), map, cmd.WantsTactical, cmd.InCombat, _actuator.IsHeld, now))
        {
            _actuator.Tap(action, now);
            AddLogEntry("Tactical", action.Name ?? "?");
        }

        _situation = new DecisionSituation(
            string.IsNullOrWhiteSpace(profile.GameContext) ? "a first-person shooter" : profile.GameContext,
            seen, (now - _reflex.LastSeenEnemy).TotalSeconds, ammo, health, stuck, _strategist.RecentDecisions);
        return Task.CompletedTask;
    }

    private static readonly HashSet<ControlRole> MoveRoles =
        [ControlRole.MoveForward, ControlRole.MoveBackward, ControlRole.MoveLeft, ControlRole.MoveRight];

    /// <summary>Thumbnail of the view centre a few times per second; stuck = moving but nothing changes.</summary>
    private bool SampleStuck(DateTime now)
    {
        if (now < _nextStuckSample) return _stuck.IsStuck;
        _nextStuckSample = now + StuckSampleInterval;
        try
        {
            using var frame = ImageCapture.CopyLastCaptureCenter(128);
            if (frame == null) return _stuck.IsStuck;
            return _stuck.Update(StuckDetector.Thumbnail(frame), _moving, now);
        }
        catch { return _stuck.IsStuck; }
    }

    // ============================================================================ DECISION REQUEST ====

    private DecisionRequest BuildRequest(AutoPlayProfile profile)
    {
        var map = _map ?? AutoPlayActionMap.Build(profile);
        var abilities = map.Strategic
            .Select(a => new JevAbility(a.Name!, a.Description, a.ActionType.ToString(), map.RoleKey(a)))
            .ToList();
        var situation = _situation ?? new DecisionSituation(profile.GameContext ?? "", default, 0, null, null, false, []);
        return new DecisionRequest(profile, situation, abilities, map.ReflexControls.ToList(), CaptureFrameSafely);
    }

    private Bitmap? CaptureFrameSafely()
    {
        try
        {
            var capture = ImageCapture;
            var area = capture?.CaptureArea ?? Rectangle.Empty;
            return area.Width < 8 || area.Height < 8 ? null : capture!.Capture(area);
        }
        catch { return null; }
    }

    // ============================================================================ LEARNING TABLE ====

    /// <summary>The legacy learning table's habit for <paramref name="state"/>, when "apply model" is on.</summary>
    private static string? LearnedPreferred(string state)
        => AppConfig.Current?.AutoPlayLearningSettings?.ApplyModel == true ? AutoPlayLearningModel.Instance.Preferred(state) : null;

    private string? LearnedDirection(string state)
    {
        var settings = AppConfig.Current?.AutoPlayLearningSettings;
        if (settings == null || !settings.ApplyModel) return null;
        if (_rng.NextDouble() >= Math.Clamp(settings.BiasStrength, 0, 1)) return null;
        return AutoPlayLearningModel.Instance.Preferred(state) switch
        {
            "move_forward" or "jump" => "forward",
            "move_backward" => "back",
            "move_left" => "left",
            "move_right" => "right",
            _ => null,
        };
    }

    // ============================================================================ PROFILE ====

    private static AutoPlayProfile? GetActiveProfile() => AppConfig.Current.AutoPlayProfiles.FirstOrDefault(p => p.IsActive);

    private void EnsureSubscribed(AutoPlayProfile profile)
    {
        if (ReferenceEquals(_cachedProfile, profile)) return;
        UnsubscribeFromProfile();
        _cachedProfile = profile;
        profile.Actions.CollectionChanged += OnActionsCollectionChanged;
        foreach (var a in profile.Actions) a.PropertyChanged += OnActionPropertyChanged;
        _mapDirty = true;
    }

    private void UnsubscribeFromProfile()
    {
        if (_cachedProfile == null) return;
        _cachedProfile.Actions.CollectionChanged -= OnActionsCollectionChanged;
        foreach (var a in _cachedProfile.Actions) a.PropertyChanged -= OnActionPropertyChanged;
        _cachedProfile = null;
    }

    private void OnActionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (AutoPlayAction a in e.OldItems) a.PropertyChanged -= OnActionPropertyChanged;
        if (e.NewItems != null) foreach (AutoPlayAction a in e.NewItems) a.PropertyChanged += OnActionPropertyChanged;
        _mapDirty = true;
    }

    private void OnActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AutoPlayAction.Name) or nameof(AutoPlayAction.Keys) or nameof(AutoPlayAction.ActionType))
            _mapDirty = true;
    }

    // ============================================================================ HELPERS ====

    private void ReleaseEverything()
    {
        _actuator.ReleaseAll();
        _reflex.Reset();
        _stuck.Reset();
    }

    private static void AddLogEntry(string category, string detail)
    {
        _activityLog.Enqueue(new AutoPlayLogEntry(DateTime.Now, category, detail));
        while (_activityLog.Count > ActivityLogCapacity && _activityLog.TryDequeue(out _)) { /* trim */ }
        System.Diagnostics.Debug.WriteLine($"[AutoPlay {DateTime.Now:HH:mm:ss.fff}] {category}: {detail}");
    }
}
