using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using PowerAim.AILogic.Contracts;
using PowerAim.Config;
using PowerAim.InputLogic;
using InputLogic;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace PowerAim.AILogic.Actions;

/// <summary>
///     Heuristic AutoPlay driver with an optional Ollama "strategic layer" running in parallel.
///     <list type="bullet">
///       <item><b>Hot path</b> (every tick, &lt;1 ms): non-blocking. Picks combat / exploration
///             behaviour, applies inputs, manages aim + burst-fire.</item>
///       <item><b>Strategic layer</b> (background task, every ~3-5 s): when an Ollama vision
///             model is reachable, captures a frame, asks for a high-level intent, parses the
///             response. The hot path reads that intent and biases its decisions. Failure /
///             timeout in the strategic layer is fully tolerated — the heuristic keeps playing.</item>
///       <item><b>Pitch centering</b>: every pitch movement we issue is summed into an
///             accumulator. Periodically a small inverse correction is applied so the view
///             doesn't drift to ceiling or floor over time.</item>
///       <item><b>Profile-aware</b>: actions are resolved by semantic role (with substring
///             fallback). Any action that doesn't fit a role becomes a "tactical" candidate the
///             strategic layer (or random rotation) can pick from.</item>
///     </list>
/// </summary>
public class AutoPlayGameAction : BaseAction
{
    // ============================================================================ STATE ====

    private readonly record struct ScheduledRelease(AutoPlayAction Action, DateTime At);

    /// <summary>High-level intent produced by the Ollama strategic layer.</summary>
    private sealed record StrategicIntent(
        string Mode,            // "explore" | "retreat" | "engage" | "tactical" | "hold" | "default"
        string? Direction,      // "left" | "right" | "forward" | "backward" | null
        string? ActionHint,     // free-text name hint matching a profile action (e.g. "throw_grenade")
        DateTime At);

    private static StrategicIntent Default => new("default", null, null, DateTime.MinValue);

    // ------ Action state ----
    private readonly HashSet<AutoPlayAction> _heldActions = new();
    private readonly List<ScheduledRelease> _scheduledReleases = new();

    // ------ Profile cache ----
    private AutoPlayProfile? _cachedProfile;
    private AutoPlayAction? _aMoveFwd, _aMoveBack, _aMoveLeft, _aMoveRight;
    private AutoPlayAction? _aSprint, _aJump, _aShoot, _aAim, _aCrouch, _aReload;
    private readonly List<AutoPlayAction> _tacticalActions = new();
    private bool _mapDirty = true;

    // ------ Combat / aim state ----
    private DateTime _lastSeenEnemy = DateTime.MinValue;
    private DateTime _firstSightingThisStreak = DateTime.MinValue;
    private DateTime _burstStartedAt = DateTime.MinValue;
    private bool _burstOn = false;

    // ------ Exploration state ----
    private string _exploreDir = "forward";
    private DateTime _commitDirectionUntil = DateTime.MinValue;
    private int _ticksWithoutEnemy = 0;

    // ------ Yaw scan state ----
    private double _yawRemaining = 0, _yawPerTick = 0;
    private DateTime _nextYawDecision = DateTime.MinValue;
    private int _lastYawSign = 0;

    // ------ Pitch accumulator (centering) ----
    private double _pitchSum = 0;
    private DateTime _lastPitchCorrection = DateTime.MinValue;

    // ------ Tactical cooldown ----
    private DateTime _nextTacticalUse = DateTime.MinValue;

    // ------ OCR-driven cues ----
    private DateTime _lastReloadAt = DateTime.MinValue;
    private DateTime _healthRetreatUntil = DateTime.MinValue;

    // ------ Strategic layer ----
    private CancellationTokenSource? _strategicCts;
    private Task? _strategicTask;
    private OllamaClient? _ollama;
    private StrategicIntent _intent = Default;
    private readonly object _intentLock = new();

    private readonly Random _rng = new();

    // ------ Live context fed into the strategic prompt ----
    private volatile int _lastEnemyCount;                 // detections on the most recent frame
    private readonly Queue<string> _recentIntents = new(); // last few strategic modes (for variety)
    private readonly object _recentIntentsLock = new();

    // ============================================================================ CONSTANTS ====

    private const double AimReleaseDelayMs   = 700;
    private const double FireReleaseDelayMs  = 300;
    private const double FireSettlingMs      = 140;
    private const double BurstOnMs           = 220;
    private const double BurstOffMs          = 110;
    private const int    MaxYawStep          = 24;
    private const double PitchCorrectionPeriodSec = 4;
    private const double PitchCorrectionFraction  = 0.25;
    private const int    PitchCorrectionMaxStep   = 14;

    // ============================================================================ LIFECYCLE ====

    private static void Log(string msg)
        => System.Diagnostics.Debug.WriteLine($"[AutoPlay {DateTime.Now:HH:mm:ss.fff}] {msg}");

    public AutoPlayGameAction()
    {
        AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChanged;
        Log("AutoPlayGameAction initialized (heuristic + optional Ollama strategic layer)");
    }

    private void OnToggleStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppConfig.Current.ToggleState.AutoPlay)) return;
        if (AppConfig.Current.ToggleState.AutoPlay)
        {
            Log("AutoPlay enabled");
            StartStrategicLayer();
        }
        else
        {
            Log("AutoPlay disabled");
            ReleaseEverything();
            StopStrategicLayer();
        }
    }

    public override bool Active => base.Active && AppConfig.Current.ToggleState.AutoPlay;

    public override Task OnPause()  { ReleaseEverything(); StopStrategicLayer(); return base.OnPause(); }
    public override Task OnResume() { if (Active) StartStrategicLayer(); return base.OnResume(); }

    public override void Dispose()
    {
        ReleaseEverything();
        StopStrategicLayer();
        UnsubscribeFromProfile();
        AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChanged;
        base.Dispose();
    }

    // ============================================================================ MAIN TICK ====

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active) { ReleaseEverything(); return Task.CompletedTask; }

        var profile = GetActiveProfile();
        if (profile == null) return Task.CompletedTask;

        EnsureSubscribed(profile);
        if (_mapDirty) { RebuildActionMap(profile); _mapDirty = false; }

        // Live-respect the per-profile UseOllama flag. The "already running" guard inside makes
        // this cheap to call every tick, and it covers the toggle-on-during-run case (toggle-off
        // is handled inside the running loop itself).
        if (profile.UseOllama) StartStrategicLayer();

        _lastEnemyCount = predictions.Length; // surfaced to the strategic prompt

        ProcessScheduledReleases();

        var screen = Screen.PrimaryScreen;
        if (screen == null) return Task.CompletedTask;
        var centerX = screen.Bounds.Width / 2f;
        var centerY = screen.Bounds.Height / 2f;

        StrategicIntent intent;
        lock (_intentLock) intent = _intent;

        // OCR cues override the Ollama-driven intent: low-health triggers a short retreat window
        // that overrides whatever the strategic layer last decided.
        MaybeReactToOcrCues();
        if (DateTime.UtcNow < _healthRetreatUntil)
            intent = intent with { Mode = "retreat" };

        if (predictions.Length > 0)
        {
            if ((DateTime.Now - _lastSeenEnemy).TotalMilliseconds > 800)
                _firstSightingThisStreak = DateTime.Now;
            _lastSeenEnemy = DateTime.Now;
            _ticksWithoutEnemy = 0;
            HandleCombat(predictions, centerX, centerY, intent);
        }
        else
        {
            _ticksWithoutEnemy++;
            HandleAimStickiness();
            HandleExploration(intent);
            MaybeUseTacticalAction(inCombat: false, intent);
        }

        StepSmoothYaw();
        MaybeCorrectPitch();
        return Task.CompletedTask;
    }

    // ============================================================================ COMBAT ====

    private void HandleCombat(Prediction[] predictions, float centerX, float centerY, StrategicIntent intent)
    {
        // If the strategic layer says "retreat", treat enemies as a reason to back off rather
        // than engage. Otherwise the normal engage logic runs.
        bool retreatMode = intent.Mode == "retreat";

        Prediction closest = predictions[0];
        double bestDistSq = DistSq(closest, centerX, centerY);
        for (int i = 1; i < predictions.Length; i++)
        {
            var d = DistSq(predictions[i], centerX, centerY);
            if (d < bestDistSq) { bestDistSq = d; closest = predictions[i]; }
        }
        var dx = closest.CenterXTranslated - centerX;
        var dy = closest.CenterYTranslated - centerY;
        var dist = Math.Sqrt(bestDistSq);

        ActivateHold(_aAim);

        bool aimAssistOn = AppConfig.Current?.ToggleState?.AimAssist == true;
        if (!aimAssistOn)
        {
            int moveX = (int)Math.Clamp(dx * 0.20, -40, 40);
            int moveY = (int)Math.Clamp(dy * 0.18, -30, 30);
            if (moveX != 0 || moveY != 0)
                ApplyMouseMove(moveX, moveY);
        }

        // Burst-fire once the aim has had time to settle. In retreat mode we still shoot back —
        // makes the bot defend itself while it's pulling away.
        var aimSettled = (DateTime.Now - _firstSightingThisStreak).TotalMilliseconds >= FireSettlingMs;
        if (aimSettled)
        {
            bool readyToFire = aimAssistOn || (Math.Abs(dx) < 90 && Math.Abs(dy) < 90);
            if (readyToFire) UpdateBurstFire();
            else             Release(_aShoot);
        }

        Release(_aSprint);

        // Strafe to centerline.
        if (Math.Abs(dx) > 160)
        {
            if (dx < 0) { ActivateHold(_aMoveLeft); Release(_aMoveRight); }
            else        { ActivateHold(_aMoveRight); Release(_aMoveLeft); }
        }
        else
        {
            Release(_aMoveLeft);
            Release(_aMoveRight);
        }

        // Distance positioning. Retreat mode prefers backing off; engage mode pushes harder.
        if (retreatMode)
        {
            ActivateHold(_aMoveBack);
            Release(_aMoveFwd);
        }
        else
        {
            bool engagePush = intent.Mode == "engage";
            int closeThreshold  = engagePush ? 250 : 300;
            int retreatThreshold = engagePush ? 70  : 90;
            if (dist > closeThreshold)        { ActivateHold(_aMoveFwd); Release(_aMoveBack); }
            else if (dist < retreatThreshold) { ActivateHold(_aMoveBack); Release(_aMoveFwd); }
            else                              { Release(_aMoveFwd); Release(_aMoveBack); }
        }

        if (predictions.Length >= 2)
            MaybeUseTacticalAction(inCombat: true, intent);
    }

    private void UpdateBurstFire()
    {
        if (_aShoot == null) return;
        var now = DateTime.Now;

        if (!_burstOn && _heldActions.Contains(_aShoot))
        {
            if ((now - _burstStartedAt).TotalMilliseconds < BurstOffMs) return;
            _burstOn = true;
            _burstStartedAt = now;
            ActivateHold(_aShoot);
            return;
        }
        if (!_burstOn)
        {
            _burstOn = true;
            _burstStartedAt = now;
            ActivateHold(_aShoot);
            return;
        }

        if ((now - _burstStartedAt).TotalMilliseconds >= BurstOnMs)
        {
            Release(_aShoot);
            _burstOn = false;
            _burstStartedAt = now;
        }
    }

    private void HandleAimStickiness()
    {
        var msSinceEnemy = (DateTime.Now - _lastSeenEnemy).TotalMilliseconds;
        if (msSinceEnemy > FireReleaseDelayMs)
        {
            Release(_aShoot);
            _burstOn = false;
        }
        if (msSinceEnemy > AimReleaseDelayMs)
            Release(_aAim);
    }

    // ============================================================================ EXPLORATION ====

    private void HandleExploration(StrategicIntent intent)
    {
        var now = DateTime.Now;

        if (now > _commitDirectionUntil)
        {
            _exploreDir = PickExploreDirection(intent);
            _commitDirectionUntil = now.AddSeconds(_rng.NextDouble() * 3 + 4);
        }

        ActivateHold(_aSprint);

        var (fwd, lft, rgt, bck) = (
            _exploreDir.Contains("forward"),
            _exploreDir.Contains("left"),
            _exploreDir.Contains("right"),
            _exploreDir.Contains("back"));
        if (_exploreDir == "left")  { fwd = false; lft = true; }
        if (_exploreDir == "right") { fwd = false; rgt = true; }

        if (fwd) ActivateHold(_aMoveFwd); else Release(_aMoveFwd);
        if (bck) ActivateHold(_aMoveBack); else Release(_aMoveBack);
        if (lft) ActivateHold(_aMoveLeft); else Release(_aMoveLeft);
        if (rgt) ActivateHold(_aMoveRight); else Release(_aMoveRight);

        if (_rng.Next(400) < 1) Activate(_aJump);

        if (now > _nextYawDecision && Math.Abs(_yawRemaining) < 1)
            ScheduleNextYawScan(now);
    }

    /// <summary>
    ///     Picks a committed explore direction. When the strategic layer has provided a
    ///     direction hint, biases toward that; otherwise random walk with a forward bias.
    /// </summary>
    private string PickExploreDirection(StrategicIntent intent)
    {
        if (intent.Mode == "retreat") return "back";

        if (!string.IsNullOrEmpty(intent.Direction))
        {
            // 70% chance to honour the hint, 30% random for variety.
            if (_rng.NextDouble() < 0.7)
            {
                return intent.Direction switch
                {
                    "forward"  => "forward",
                    "backward" => "back",
                    "left"     => "forward_left",
                    "right"    => "forward_right",
                    _          => "forward"
                };
            }
        }

        // Learning-model bias: when ApplyModel is on and we have recorded the user's habit for
        // the current explore state, prefer their direction. BiasStrength acts as the probability
        // we accept the suggestion vs. fall through to the default random walk.
        var direction = TryLearnedExploreDirection();
        if (direction != null) return direction;

        string[] patterns =
        [
            "forward", "forward", "forward", "forward",
            "forward_left", "forward_right",
            "left", "right",
        ];
        return patterns[_rng.Next(patterns.Length)];
    }

    /// <summary>
    ///     Consult <see cref="AutoPlayLearningModel"/> with the current discretized state and map
    ///     the user's most-frequent movement action to one of our direction labels. Returns
    ///     <c>null</c> when learning is off, when no preference is recorded, or when the dice roll
    ///     against <see cref="AutoPlayLearningSettings.BiasStrength"/> says "use random instead".
    /// </summary>
    private string? TryLearnedExploreDirection()
    {
        var settings = AppConfig.Current?.AutoPlayLearningSettings;
        if (settings == null || !settings.ApplyModel) return null;

        string state = LearningModelStateForCurrentTick(inCombat: false);
        string? preferred = AutoPlayLearningModel.Instance.Preferred(state);
        if (string.IsNullOrEmpty(preferred)) return null;

        double bias = Math.Clamp(settings.BiasStrength, 0, 1);
        if (_rng.NextDouble() >= bias) return null;

        return preferred switch
        {
            "move_forward"  => "forward",
            "move_backward" => "back",
            "move_left"     => "left",
            "move_right"    => "right",
            "jump"          => "forward",   // jump implies user was advancing
            _               => null
        };
    }

    private void ScheduleNextYawScan(DateTime now)
    {
        _nextYawDecision = now.AddSeconds(_rng.NextDouble() * 2 + 1.5);

        // Explicit alternation with 20 % randomness — pure random produced visible left-bias
        // streaks even though it's mathematically balanced.
        int sign = (_lastYawSign == 0 || _rng.NextDouble() < 0.20)
            ? (_rng.Next(2) == 0 ? -1 : 1)
            : -_lastYawSign;
        _lastYawSign = sign;

        int magnitude = _ticksWithoutEnemy > 200 ? _rng.Next(220, 360) : _rng.Next(80, 180);
        int durationTicks = _rng.Next(8, 18);
        _yawRemaining = sign * magnitude;
        _yawPerTick   = _yawRemaining / durationTicks;
    }

    private void StepSmoothYaw()
    {
        if (Math.Abs(_yawRemaining) < 1) return;
        int step = (int)Math.Round(_yawPerTick);
        if (step == 0) step = Math.Sign(_yawRemaining);
        step = Math.Clamp(step, -MaxYawStep, MaxYawStep);
        ApplyMouseMove(step, 0);
        _yawRemaining -= step;
    }

    // ============================================================================ PITCH CENTERING ====

    /// <summary>
    ///     Every <see cref="PitchCorrectionPeriodSec"/> seconds, nudge the view back toward
    ///     "horizon" by undoing a fraction of the cumulative pitch we've induced. We only know
    ///     the pitch we've sent ourselves — the game's own recoil isn't tracked — but in
    ///     practice user-induced pitch dominates and this keeps the view from drifting to the
    ///     ceiling/floor over long sessions.
    /// </summary>
    private void MaybeCorrectPitch()
    {
        var now = DateTime.Now;
        if ((now - _lastPitchCorrection).TotalSeconds < PitchCorrectionPeriodSec) return;
        _lastPitchCorrection = now;

        if (Math.Abs(_pitchSum) < 6) return; // already close to neutral

        double correction = -_pitchSum * PitchCorrectionFraction;
        int correctY = (int)Math.Round(Math.Clamp(correction, -PitchCorrectionMaxStep, PitchCorrectionMaxStep));
        if (correctY != 0)
        {
            ApplyMouseMove(0, correctY);
        }
    }

    /// <summary>Wraps <see cref="MouseManager.Move"/> so the pitch accumulator stays accurate.</summary>
    private void ApplyMouseMove(int dx, int dy)
    {
        if (dx != 0 || dy != 0)
        {
            MouseManager.Move(dx, dy);
            _pitchSum += dy;
        }
    }

    // ============================================================================ TACTICAL ====

    private void MaybeUseTacticalAction(bool inCombat, StrategicIntent intent)
    {
        if (_tacticalActions.Count == 0) return;
        var now = DateTime.Now;
        if (now < _nextTacticalUse) return;

        // If the strategic layer asked for a specific action by name, prefer that — provided it
        // actually exists in the profile and isn't currently held.
        AutoPlayAction? action = null;
        if (!string.IsNullOrEmpty(intent.ActionHint))
        {
            action = _tacticalActions.FirstOrDefault(a =>
                !_heldActions.Contains(a) &&
                a.Name != null &&
                a.Name.Contains(intent.ActionHint, StringComparison.OrdinalIgnoreCase));
        }

        // Learning model bias: if the recorder has been used and ApplyModel is on, ask the model
        // what the user typically does in this state and pick a tactical action whose name matches
        // it (substring). Loss-free fallback to the random selector when no match.
        if (action == null && AppConfig.Current.AutoPlayLearningSettings?.ApplyModel == true)
        {
            string state = LearningModelStateForCurrentTick(inCombat);
            string? preferred = AutoPlayLearningModel.Instance.Preferred(state);
            if (!string.IsNullOrEmpty(preferred))
            {
                var match = _tacticalActions.FirstOrDefault(a =>
                    !_heldActions.Contains(a) && a.Name != null &&
                    a.Name.Contains(preferred, StringComparison.OrdinalIgnoreCase));
                // Probabilistic acceptance based on BiasStrength so the learned hint can be
                // partially overridden by exploration.
                double bias = AppConfig.Current.AutoPlayLearningSettings.BiasStrength;
                if (match != null && _rng.NextDouble() < bias) action = match;
            }
        }

        if (action == null)
        {
            var candidates = _tacticalActions.Where(a => !_heldActions.Contains(a)).ToList();
            if (candidates.Count == 0) { _nextTacticalUse = now.AddSeconds(2); return; }
            action = candidates[_rng.Next(candidates.Count)];
        }

        Activate(action);
        Log($"Tactical: {action.Name} (combat={inCombat}, hinted={intent.ActionHint != null})");

        _nextTacticalUse = now.AddSeconds(inCombat
            ? _rng.NextDouble() * 4 + 3
            : _rng.NextDouble() * 8 + 6);
    }

    /// <summary>
    ///     Coarse state label used to look the user's preference up in
    ///     <see cref="AutoPlayLearningModel"/>. Intentionally low-cardinality so even small
    ///     recording sessions produce a useful prior.
    /// </summary>
    private string LearningModelStateForCurrentTick(bool inCombat)
        => inCombat
            ? (_burstOn ? "combat_burst" : "combat_aim")
            : (_ticksWithoutEnemy < 30 ? "explore_recent" : "explore_idle");

    // ============================================================================ OCR CUES ====

    /// <summary>
    ///     Inspects the OCR HUD-reader output (<see cref="OcrService.Instance.Latest"/>) and reacts
    ///     to two well-known cues:
    ///     <list type="bullet">
    ///       <item><b>Ammo low</b> (region named ammo / mag / bullets, value &lt; <see cref="LowAmmoThreshold"/>):
    ///             triggers the profile's reload action, with a cooldown so we don't spam.</item>
    ///       <item><b>Health low</b> (region named health / hp / armor, value &lt; <see cref="LowHealthThreshold"/>):
    ///             sets a brief retreat window that overrides the Ollama intent.</item>
    ///     </list>
    ///     Both cues are <i>opt-in</i> — they only fire if the user actually configured a matching
    ///     OCR region. No region = no behaviour change.
    /// </summary>
    private void MaybeReactToOcrCues()
    {
        // OCR can be disabled entirely (engine off) — bail cheaply.
        if (AppConfig.Current?.OcrSettings?.Enabled != true) return;

        var now = DateTime.UtcNow;
        var ocr = OcrService.Instance.Latest;
        if (ocr.Count == 0) return;

        // ---- Ammo cue ----
        double? ammo = TryReadOcrNumber(ocr, _ammoAliases);
        if (ammo.HasValue && ammo.Value <= LowAmmoThreshold
            && (now - _lastReloadAt).TotalSeconds > ReloadCooldownSec)
        {
            if (_aReload != null && _aReload.IsValid)
            {
                Activate(_aReload);
                _lastReloadAt = now;
                Log($"OCR: ammo={ammo.Value:0} → reload");
            }
        }

        // ---- Health cue ----
        double? health = TryReadOcrNumber(ocr, _healthAliases);
        if (health.HasValue && health.Value <= LowHealthThreshold)
        {
            // Extend (or open) the retreat window. Letting the timer slide forward as long as
            // health stays low avoids the bot bouncing between retreat and engage.
            _healthRetreatUntil = now + TimeSpan.FromSeconds(HealthRetreatSeconds);
        }
    }

    /// <summary>
    ///     Look up the first OCR region whose name (case-insensitive) matches one of the
    ///     <paramref name="aliases"/> and return its parsed numeric value, if any.
    /// </summary>
    private static double? TryReadOcrNumber(IReadOnlyDictionary<string, OcrResult> latest, string[] aliases)
    {
        foreach (var kv in latest)
        {
            foreach (var alias in aliases)
            {
                if (kv.Key.Equals(alias, StringComparison.OrdinalIgnoreCase)
                    || kv.Key.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    // Only accept readings with a sane confidence — Tesseract's confidence is
                    // 0..1 here (already normalized in OcrService) and below 0.3 is usually noise.
                    if (kv.Value.Confidence >= 0.3f && kv.Value.Number.HasValue)
                        return kv.Value.Number;
                }
            }
        }
        return null;
    }

    // Region name conventions the OCR cue layer recognizes. Users can name their regions any of
    // these (case-insensitive, substring match).
    private static readonly string[] _ammoAliases   = ["ammo", "mag", "bullets", "magazine", "rounds"];
    private static readonly string[] _healthAliases = ["health", "hp", "armor", "armour", "shield"];

    private const double LowAmmoThreshold    = 6;
    private const double LowHealthThreshold  = 30;
    private const double ReloadCooldownSec   = 4.0;
    private const double HealthRetreatSeconds = 2.5;

    // ============================================================================ STRATEGIC (Ollama) ====

    private void StartStrategicLayer()
    {
        if (_strategicTask is { IsCompleted: false }) return; // already running
        // Respect the per-profile opt-out: skip the whole strategic loop (no HTTP polling, no
        // screenshot capture) when the active profile has UseOllama disabled. The loop's inner
        // check picks up the toggle if it's flipped on later while AutoPlay is already running.
        var profile = GetActiveProfile();
        if (profile != null && !profile.UseOllama)
        {
            Log("Strategic layer skipped — active profile has UseOllama=false");
            return;
        }
        _strategicCts?.Dispose();
        _strategicCts = new CancellationTokenSource();
        _ollama ??= new OllamaClient();
        _strategicTask = Task.Run(() => StrategicLoopAsync(_strategicCts.Token));
    }

    private void StopStrategicLayer()
    {
        try { _strategicCts?.Cancel(); } catch { /* ignored */ }
        _strategicTask = null;
        lock (_intentLock) _intent = Default;
    }

    /// <summary>
    ///     Background loop: every few seconds (per <see cref="AutoPlayProfile.DecisionInterval"/>),
    ///     grabs a screenshot of the play area and asks the configured Ollama vision model for a
    ///     high-level intent. Failures are swallowed so the heuristic keeps running.
    /// </summary>
    private async Task StrategicLoopAsync(CancellationToken ct)
    {
        Log("Strategic layer started");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var profile = GetActiveProfile();
                if (profile == null || string.IsNullOrWhiteSpace(profile.OllamaModel))
                {
                    await SafeDelay(3000, ct);
                    continue;
                }

                // Live opt-out: if the user toggled UseOllama off while the loop was running, stop
                // doing the expensive work and idle on a longer poll until they flip it back.
                if (!profile.UseOllama)
                {
                    lock (_intentLock) _intent = Default;
                    await SafeDelay(5000, ct);
                    continue;
                }

                bool available;
                try { available = await _ollama!.IsAvailableAsync(); }
                catch { available = false; }

                if (!available)
                {
                    lock (_intentLock) _intent = Default; // drop stale hints when Ollama drops out
                    await SafeDelay(5000, ct);
                    continue;
                }

                try
                {
                    var bmp = CaptureFrameSafely();
                    if (bmp != null)
                    {
                        var prompt = BuildPrompt(profile);
                        var raw = await _ollama.AnalyzeImageAsync(bmp, prompt, profile.OllamaModel);
                        var intent = ParseIntent(raw);
                        if (intent != null)
                        {
                            lock (_intentLock) _intent = intent;
                            RememberIntent(intent);
                            Log($"Intent: {intent.Mode} dir={intent.Direction} action={intent.ActionHint}");
                        }
                        bmp.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Log($"Strategic query failed: {ex.Message}");
                }

                var waitSec = Math.Max(0.5, profile.DecisionInterval);
                await SafeDelay(TimeSpan.FromSeconds(waitSec), ct);
            }
        }
        catch (OperationCanceledException) { /* expected */ }
        catch (Exception ex) { Log($"Strategic loop crashed: {ex.Message}"); }
        finally { Log("Strategic layer stopped"); }
    }

    private Bitmap? CaptureFrameSafely()
    {
        try
        {
            var capture = ImageCapture;
            if (capture == null) return null;
            var area = capture.CaptureArea;
            if (area.Width < 8 || area.Height < 8) return null;
            return capture.Capture(area);
        }
        catch { return null; }
    }

    private string BuildPrompt(AutoPlayProfile profile)
    {
        var tactical = string.Join(", ", _tacticalActions.Select(a => a.Name));
        var ctx = string.IsNullOrWhiteSpace(profile.GameContext) ? "a first-person shooter game" : profile.GameContext;
        var sb = new System.Text.StringBuilder();
        sb.Append("You are guiding an AI bot playing ").Append(ctx).Append('.').Append('\n');
        sb.Append("Look at the current frame and produce ONE single-line JSON with the bot's tactical intent.\n");

        AppendSituation(sb);

        sb.Append("Allowed shapes:\n");
        sb.Append("  {\"mode\":\"explore\",\"direction\":\"forward\"|\"backward\"|\"left\"|\"right\"}\n");
        sb.Append("  {\"mode\":\"engage\",\"priority\":\"left\"|\"right\"|\"center\"}\n");
        sb.Append("  {\"mode\":\"retreat\"}\n");
        sb.Append("  {\"mode\":\"hold\"}\n");
        sb.Append("  {\"mode\":\"tactical\",\"action\":\"<name>\"}    where <name> is one of: ").Append(tactical).Append('\n');
        sb.Append("Avoid repeating the same mode every time; vary your decisions to keep the bot moving.\n");
        sb.Append("Reply with the JSON only, no other text.");
        return sb.ToString();
    }

    /// <summary>
    ///     Appends the live "current situation" block to the strategic prompt: how many enemies the
    ///     detector currently sees, any OCR HP/ammo readings, and the bot's recent intent history.
    ///     Gives the vision model grounded numbers it can't reliably read off a downscaled frame, and
    ///     lets it react to its own recent choices instead of oscillating.
    /// </summary>
    private void AppendSituation(System.Text.StringBuilder sb)
    {
        sb.Append("Current situation:\n");
        int enemies = _lastEnemyCount;
        sb.Append("  - enemies detected on screen: ").Append(enemies).Append('\n');

        if (AppConfig.Current?.OcrSettings?.Enabled == true)
        {
            var ocr = OcrService.Instance.Latest;
            if (ocr.Count > 0)
            {
                double? ammo = TryReadOcrNumber(ocr, _ammoAliases);
                if (ammo.HasValue)
                    sb.Append("  - ammo (OCR): ").Append(ammo.Value.ToString("0")).Append('\n');
                double? health = TryReadOcrNumber(ocr, _healthAliases);
                if (health.HasValue)
                    sb.Append("  - health (OCR): ").Append(health.Value.ToString("0")).Append('\n');
            }
        }

        string[] recent;
        lock (_recentIntentsLock) recent = _recentIntents.ToArray();
        if (recent.Length > 0)
            sb.Append("  - your recent decisions (oldest→newest): ").Append(string.Join(", ", recent)).Append('\n');
    }

    private static readonly Regex IntentRegex = new(
        @"""mode""\s*:\s*""(?<mode>[a-zA-Z_]+)""(?:[^}]*?""direction""\s*:\s*""(?<dir>[a-zA-Z_]+)"")?(?:[^}]*?""action""\s*:\s*""(?<act>[^""]+)"")?",
        RegexOptions.Compiled);

    private StrategicIntent? ParseIntent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = IntentRegex.Match(raw);
        if (!m.Success) return null;
        var mode = m.Groups["mode"].Value.ToLowerInvariant();
        var dir = m.Groups["dir"].Success ? m.Groups["dir"].Value.ToLowerInvariant() : null;
        var act = m.Groups["act"].Success ? m.Groups["act"].Value : null;
        return new StrategicIntent(mode, dir, act, DateTime.Now);
    }

    /// <summary>Keep a short rolling history of strategic modes so the next prompt can ask for variety.</summary>
    private void RememberIntent(StrategicIntent intent)
    {
        var label = string.IsNullOrEmpty(intent.Direction) ? intent.Mode : $"{intent.Mode}/{intent.Direction}";
        lock (_recentIntentsLock)
        {
            _recentIntents.Enqueue(label);
            while (_recentIntents.Count > 5) _recentIntents.Dequeue();
        }
    }

    private static async Task SafeDelay(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); } catch { /* cancelled */ }
    }
    private static async Task SafeDelay(TimeSpan span, CancellationToken ct)
    {
        try { await Task.Delay(span, ct); } catch { /* cancelled */ }
    }

    // ============================================================================ PROFILE MAP ====

    private AutoPlayProfile? GetActiveProfile()
        => AppConfig.Current.AutoPlayProfiles.FirstOrDefault(p => p.IsActive);

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
        if (e.OldItems != null)
            foreach (AutoPlayAction a in e.OldItems) a.PropertyChanged -= OnActionPropertyChanged;
        if (e.NewItems != null)
            foreach (AutoPlayAction a in e.NewItems) a.PropertyChanged += OnActionPropertyChanged;
        _mapDirty = true;
    }

    private void OnActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AutoPlayAction.Name)
            or nameof(AutoPlayAction.Keys)
            or nameof(AutoPlayAction.ActionType))
        {
            _mapDirty = true;
        }
    }

    private void RebuildActionMap(AutoPlayProfile profile)
    {
        _aMoveFwd   = FindAction(profile, "move_forward",  "forward", "fwd", "walk", "move_fwd");
        _aMoveBack  = FindAction(profile, "move_backward", "move_back", "backward", "back", "retreat");
        _aMoveLeft  = FindAction(profile, "move_left",     "strafe_left", "left");
        _aMoveRight = FindAction(profile, "move_right",    "strafe_right", "right");
        _aSprint    = FindAction(profile, "sprint",        "run", "dash");
        _aJump      = FindAction(profile, "jump",          "leap");
        _aShoot     = FindAction(profile, "shoot",         "fire", "attack", "primary");
        _aAim       = FindAction(profile, "aim",           "ads", "scope", "secondary");
        _aCrouch    = FindAction(profile, "crouch",        "duck", "prone");
        _aReload    = FindAction(profile, "reload");

        var mapped = new HashSet<AutoPlayAction?>
        {
            _aMoveFwd, _aMoveBack, _aMoveLeft, _aMoveRight,
            _aSprint, _aJump, _aShoot, _aAim, _aCrouch, _aReload
        };
        _tacticalActions.Clear();
        foreach (var a in profile.Actions)
            if (a.IsValid && !mapped.Contains(a)) _tacticalActions.Add(a);

        Log($"Profile '{profile.Name}': fwd={(_aMoveFwd != null)} L={(_aMoveLeft != null)} R={(_aMoveRight != null)} " +
            $"shoot={(_aShoot != null)} aim={(_aAim != null)} jump={(_aJump != null)} tactical={_tacticalActions.Count}");
    }

    private static AutoPlayAction? FindAction(AutoPlayProfile profile, params string[] candidates)
    {
        foreach (var c in candidates)
        {
            var hit = profile.Actions.FirstOrDefault(a => a.IsValid &&
                string.Equals(a.Name, c, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        foreach (var c in candidates)
        {
            var hit = profile.Actions.FirstOrDefault(a => a.IsValid && a.Name != null &&
                a.Name.Contains(c, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }
        return null;
    }

    // ============================================================================ ACTIVATION ====

    /// <summary>Activate respecting <see cref="AutoPlayAction.ActionType"/>.</summary>
    private void Activate(AutoPlayAction? action)
    {
        if (action == null || !action.IsValid) return;
        switch (action.ActionType)
        {
            case AutoPlayActionType.Instant:
            case AutoPlayActionType.Toggle:
                Tap(action);
                break;
            default:
                Press(action);
                break;
        }
    }

    /// <summary>Always-hold path — for movement / aim / shoot / sprint.</summary>
    private void ActivateHold(AutoPlayAction? action) => Press(action);

    private void Press(AutoPlayAction? action)
    {
        if (action == null || !action.IsValid) return;
        if (!_heldActions.Add(action)) return;
        foreach (var key in action.Keys.Where(k => k.IsValid))
            _ = InputSender.SendKeyAsync(key, KeyPressState.Down);
    }

    private void Release(AutoPlayAction? action)
    {
        if (action == null || !action.IsValid) return;
        if (!_heldActions.Remove(action)) return;
        foreach (var key in action.Keys.Where(k => k.IsValid))
            _ = InputSender.SendKeyAsync(key, KeyPressState.Up);
    }

    private void Tap(AutoPlayAction? action)
    {
        if (action == null || !action.IsValid) return;
        if (_heldActions.Contains(action)) return;
        _heldActions.Add(action);
        foreach (var key in action.Keys.Where(k => k.IsValid))
            _ = InputSender.SendKeyAsync(key, KeyPressState.Down);
        var delaySec = Math.Max(action.Duration, 0.05);
        _scheduledReleases.Add(new ScheduledRelease(action, DateTime.Now.AddSeconds(delaySec)));
    }

    private void ProcessScheduledReleases()
    {
        if (_scheduledReleases.Count == 0) return;
        var now = DateTime.Now;
        for (int i = _scheduledReleases.Count - 1; i >= 0; i--)
        {
            if (_scheduledReleases[i].At > now) continue;
            var action = _scheduledReleases[i].Action;
            _scheduledReleases.RemoveAt(i);
            if (_heldActions.Remove(action))
                foreach (var key in action.Keys.Where(k => k.IsValid))
                    _ = InputSender.SendKeyAsync(key, KeyPressState.Up);
        }
    }

    // ============================================================================ HELPERS ====

    private void ReleaseEverything()
    {
        foreach (var action in _heldActions.ToList())
            foreach (var key in action.Keys.Where(k => k.IsValid))
                _ = InputSender.SendKeyAsync(key, KeyPressState.Up);
        _heldActions.Clear();
        _scheduledReleases.Clear();
        _burstOn = false;
        _yawRemaining = 0;
    }

    private static double DistSq(Prediction p, float cx, float cy)
    {
        var dx = p.CenterXTranslated - cx;
        var dy = p.CenterYTranslated - cy;
        return dx * dx + dy * dy;
    }
}
