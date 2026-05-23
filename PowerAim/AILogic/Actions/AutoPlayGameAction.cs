using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using PowerAim.AILogic.Contracts;
using PowerAim.Config;
using PowerAim.InputLogic;
using InputLogic;
using System.Windows.Forms;

namespace PowerAim.AILogic.Actions;

/// <summary>
///     Heuristic AutoPlay driver. Designed to be responsive and to make full use of the user's
///     profile/actions — no hardcoded key bindings, no blocking calls.
///     <para>
///     <b>Responsiveness</b>: nothing blocks the inference loop. Instant-tap actions release on a
///     scheduled timer; mouse "look around" is interpolated in small per-tick steps.
///     </para>
///     <para>
///     <b>Action utilisation</b>: actions are resolved by semantic role (forward/back/L/R/sprint/
///     jump/shoot/aim/crouch/reload) via case-insensitive exact then substring matches, so a
///     profile that calls its forward action <c>walk</c> still works. Any action that doesn't fit
///     a role becomes a "tactical" action and is used periodically (every 6–14 s), so things like
///     <c>throw_grenade</c>, <c>melee</c>, <c>interact</c>, <c>mark</c> get triggered.
///     </para>
///     <para>
///     <b>ActionType respected</b>: <c>Continuous</c>/<c>Modifier</c> are held; <c>Instant</c>/
///     <c>Toggle</c> are tapped (press + scheduled release after <c>Duration</c>).
///     </para>
///     <para>
///     <b>Cache invalidation</b>: subscribes to <see cref="AutoPlayProfile.Actions"/>
///     <see cref="INotifyCollectionChanged"/> and to each action's <see cref="INotifyPropertyChanged"/>,
///     so renames, additions and removals take effect immediately without restart.
///     </para>
///     <para>
///     <b>Combat</b>: as soon as a prediction enters the frame the aim key is held; after a brief
///     ~140 ms settling window so the aim-assist can converge, burst-fire starts and stays on as
///     long as enemies are visible. Aim/fire have short stickiness windows so brief occlusions
///     don't drop the gun.
///     </para>
/// </summary>
public class AutoPlayGameAction : BaseAction
{
    private readonly record struct ScheduledRelease(AutoPlayAction Action, DateTime At);

    // Currently held actions (Continuous / Modifier / Toggle waiting for release timer).
    private readonly HashSet<AutoPlayAction> _heldActions = new();
    private readonly List<ScheduledRelease> _scheduledReleases = new();

    // Cached profile + semantic role lookups.
    private AutoPlayProfile? _cachedProfile;
    private AutoPlayAction? _aMoveFwd, _aMoveBack, _aMoveLeft, _aMoveRight;
    private AutoPlayAction? _aSprint, _aJump, _aShoot, _aAim, _aCrouch, _aReload;
    private readonly List<AutoPlayAction> _tacticalActions = new();
    private bool _mapDirty = true;

    // Aim / fire stickiness so brief occlusions don't drop the gun.
    private DateTime _lastSeenEnemy = DateTime.MinValue;
    private DateTime _firstSightingThisStreak = DateTime.MinValue;
    private const double AimReleaseDelayMs = 700;
    private const double FireReleaseDelayMs = 300;
    private const double FireSettlingMs = 140;   // give aim-assist time to converge before pulling the trigger

    // Burst-fire control.
    private DateTime _burstStartedAt = DateTime.MinValue;
    private bool _burstOn = false;
    private const double BurstOnMs = 220;
    private const double BurstOffMs = 110;

    // Exploration state — commit to a direction for several seconds.
    private string _exploreDir = "forward";
    private DateTime _commitDirectionUntil = DateTime.MinValue;
    private int _ticksWithoutEnemy = 0;
    private readonly Random _rng = new();

    // Smooth yaw drift.
    private double _yawRemaining = 0;
    private double _yawPerTick = 0;
    private DateTime _nextYawDecision = DateTime.MinValue;
    private int _lastYawSign = 0;  // strict alternation memo so the bot doesn't drift only one way

    // Tactical use cooldown.
    private DateTime _nextTacticalUse = DateTime.MinValue;

    private static void Log(string msg)
        => System.Diagnostics.Debug.WriteLine($"[AutoPlay {DateTime.Now:HH:mm:ss.fff}] {msg}");

    public AutoPlayGameAction()
    {
        AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChanged;
        Log("AutoPlayGameAction initialized (heuristic mode, non-blocking, profile-aware)");
    }

    private void OnToggleStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppConfig.Current.ToggleState.AutoPlay)) return;
        if (AppConfig.Current.ToggleState.AutoPlay) Log("AutoPlay enabled");
        else { Log("AutoPlay disabled"); ReleaseEverything(); }
    }

    public override bool Active => base.Active && AppConfig.Current.ToggleState.AutoPlay;

    // =============================== Main tick ==================================

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active) { ReleaseEverything(); return Task.CompletedTask; }

        var profile = GetActiveProfile();
        if (profile == null) return Task.CompletedTask;

        EnsureSubscribed(profile);
        if (_mapDirty) { RebuildActionMap(profile); _mapDirty = false; }

        ProcessScheduledReleases();

        var screen = Screen.PrimaryScreen;
        if (screen == null) return Task.CompletedTask;
        var centerX = screen.Bounds.Width / 2f;
        var centerY = screen.Bounds.Height / 2f;

        if (predictions.Length > 0)
        {
            if ((DateTime.Now - _lastSeenEnemy).TotalMilliseconds > 800)
            {
                // First sighting after a gap → start the settling clock.
                _firstSightingThisStreak = DateTime.Now;
            }
            _lastSeenEnemy = DateTime.Now;
            _ticksWithoutEnemy = 0;
            HandleCombat(predictions, centerX, centerY);
        }
        else
        {
            _ticksWithoutEnemy++;
            HandleAimStickiness();
            HandleExploration();
            MaybeUseTacticalAction(inCombat: false);
        }

        StepSmoothYaw();
        return Task.CompletedTask;
    }

    // =============================== Combat =====================================

    private void HandleCombat(Prediction[] predictions, float centerX, float centerY)
    {
        // Closest target to the crosshair.
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

        // Always hold the aim key while an enemy is in frame. AimingAction does the crosshair
        // work for us when AimAssist is on. When it's off, we do it ourselves below.
        ActivateHold(_aAim);

        bool aimAssistOn = AppConfig.Current?.ToggleState?.AimAssist == true;
        if (!aimAssistOn)
        {
            // Manual aim: nudge the crosshair towards the enemy each tick. Fraction is small so
            // multiple ticks combine into a smooth track instead of a teleport. Clamp prevents
            // jolts when the enemy is at the far edge of the detection box.
            int moveX = (int)Math.Clamp(dx * 0.20, -40, 40);
            int moveY = (int)Math.Clamp(dy * 0.18, -30, 30);
            if (moveX != 0 || moveY != 0)
                MouseManager.Move(moveX, moveY);
        }

        // Burst-fire as long as enemies are visible. Aim-settling delay gives whichever path
        // (auto or manual) ~140 ms to bring the crosshair onto target before opening up.
        var aimSettled = (DateTime.Now - _firstSightingThisStreak).TotalMilliseconds >= FireSettlingMs;
        if (aimSettled)
        {
            // If we're aiming manually and the crosshair isn't even close to the target yet,
            // hold fire a moment longer instead of spraying.
            bool readyToFire = aimAssistOn
                || (Math.Abs(dx) < 90 && Math.Abs(dy) < 90);
            if (readyToFire) UpdateBurstFire();
            else             Release(_aShoot);
        }

        // Don't sprint in combat — slows turn rate and ruins recoil control in many games.
        Release(_aSprint);

        // Strafe to keep the target on the centerline.
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

        // Engage the target — push forward when there's distance to close, back off only when
        // way too close. The old 600 px threshold was way too cautious and made the bot stand
        // still while enemies were comfortably in view.
        if (dist > 300)         { ActivateHold(_aMoveFwd); Release(_aMoveBack); }
        else if (dist < 90)     { ActivateHold(_aMoveBack); Release(_aMoveFwd); }
        else                    { Release(_aMoveFwd); Release(_aMoveBack); }

        // Tactical actions (grenades / abilities / melee) — a bit more frequently in combat,
        // and only when more than one target is around so we don't waste a grenade on a single
        // weak enemy.
        if (predictions.Length >= 2)
        {
            MaybeUseTacticalAction(inCombat: true);
        }
    }

    private void UpdateBurstFire()
    {
        if (_aShoot == null) return;
        var now = DateTime.Now;

        if (!_burstOn && _heldActions.Contains(_aShoot))
        {
            // Mid-cool-off — wait until BurstOffMs has elapsed before re-firing.
            if ((now - _burstStartedAt).TotalMilliseconds < BurstOffMs) return;
            _burstOn = true;
            _burstStartedAt = now;
            ActivateHold(_aShoot);
            return;
        }
        if (!_burstOn)
        {
            // Start the very first burst of this combat streak.
            _burstOn = true;
            _burstStartedAt = now;
            ActivateHold(_aShoot);
            return;
        }

        // Burst already running.
        if ((now - _burstStartedAt).TotalMilliseconds >= BurstOnMs)
        {
            // End burst, enter cool-off.
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
        {
            Release(_aAim);
        }
    }

    // =============================== Exploration ================================

    private void HandleExploration()
    {
        var now = DateTime.Now;

        if (now > _commitDirectionUntil)
        {
            var patterns = new[]
            {
                "forward", "forward", "forward", "forward",
                "forward_left", "forward_right",
                "left", "right",
            };
            _exploreDir = patterns[_rng.Next(patterns.Length)];
            _commitDirectionUntil = now.AddSeconds(_rng.NextDouble() * 3 + 4);
        }

        ActivateHold(_aSprint);

        var (fwd, lft, rgt) = (
            _exploreDir.Contains("forward"),
            _exploreDir.Contains("left"),
            _exploreDir.Contains("right"));
        if (_exploreDir == "left")  { fwd = false; lft = true; }
        if (_exploreDir == "right") { fwd = false; rgt = true; }

        if (fwd) ActivateHold(_aMoveFwd); else Release(_aMoveFwd);
        Release(_aMoveBack);
        if (lft) ActivateHold(_aMoveLeft); else Release(_aMoveLeft);
        if (rgt) ActivateHold(_aMoveRight); else Release(_aMoveRight);

        if (_rng.Next(400) < 1) Activate(_aJump);

        // Smooth yaw scan when we're standing still or going straight. The previous version
        // used pure 50/50 random sign which in practice produced visible left-bias streaks —
        // alternate explicitly with 20 % randomness so both directions get equal coverage.
        if (now > _nextYawDecision && Math.Abs(_yawRemaining) < 1)
        {
            _nextYawDecision = now.AddSeconds(_rng.NextDouble() * 2 + 1.5);

            int sign;
            if (_lastYawSign == 0 || _rng.NextDouble() < 0.20)
            {
                sign = _rng.Next(2) == 0 ? -1 : 1;
            }
            else
            {
                sign = -_lastYawSign;
            }
            _lastYawSign = sign;

            int magnitude = _ticksWithoutEnemy > 200 ? _rng.Next(220, 360) : _rng.Next(80, 180);
            int durationTicks = _rng.Next(8, 18);
            _yawRemaining = sign * magnitude;
            _yawPerTick = _yawRemaining / durationTicks;
        }
    }

    // =============================== Tactical actions ===========================

    /// <summary>
    ///     Pick a random profile action that doesn't map to a known role and fire it. Cooldown
    ///     keeps the use rate sane. Skipped when no tactical actions are configured.
    /// </summary>
    private void MaybeUseTacticalAction(bool inCombat)
    {
        if (_tacticalActions.Count == 0) return;
        var now = DateTime.Now;
        if (now < _nextTacticalUse) return;

        // Pick a random tactical action that's not currently held.
        var candidates = _tacticalActions.Where(a => !_heldActions.Contains(a)).ToList();
        if (candidates.Count == 0)
        {
            _nextTacticalUse = now.AddSeconds(2);
            return;
        }
        var action = candidates[_rng.Next(candidates.Count)];
        Activate(action);
        Log($"Tactical: {action.Name} (combat={inCombat})");

        // Shorter cooldown in combat for more dynamic plays.
        _nextTacticalUse = now.AddSeconds(inCombat
            ? _rng.NextDouble() * 4 + 3   // 3–7 s
            : _rng.NextDouble() * 8 + 6); // 6–14 s
    }

    // =============================== Smooth yaw =================================

    private void StepSmoothYaw()
    {
        if (Math.Abs(_yawRemaining) < 1) return;
        int step = (int)Math.Round(_yawPerTick);
        if (step == 0) step = Math.Sign(_yawRemaining);
        step = Math.Clamp(step, -24, 24);
        MouseManager.Move(step, 0);
        _yawRemaining -= step;
    }

    // =============================== Profile / action map =======================

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
        {
            if (a.IsValid && !mapped.Contains(a)) _tacticalActions.Add(a);
        }

        Log($"Profile '{profile.Name}': fwd={(_aMoveFwd != null)} back={(_aMoveBack != null)} L={(_aMoveLeft != null)} R={(_aMoveRight != null)} " +
            $"sprint={(_aSprint != null)} jump={(_aJump != null)} shoot={(_aShoot != null)} aim={(_aAim != null)} " +
            $"crouch={(_aCrouch != null)} reload={(_aReload != null)} tactical={_tacticalActions.Count}");
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

    // =============================== Activation primitives ======================

    /// <summary>
    ///     Activate an action respecting its <see cref="AutoPlayAction.ActionType"/>. Use this for
    ///     occasional/tactical use where the caller doesn't care whether the action is hold-style
    ///     or tap-style. Movement/aim/shoot use the explicit <see cref="ActivateHold"/> path.
    /// </summary>
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
                // Continuous, Modifier: held while caller wants.
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
            {
                foreach (var key in action.Keys.Where(k => k.IsValid))
                    _ = InputSender.SendKeyAsync(key, KeyPressState.Up);
            }
        }
    }

    // =============================== Lifecycle ==================================

    private void ReleaseEverything()
    {
        foreach (var action in _heldActions.ToList())
        {
            foreach (var key in action.Keys.Where(k => k.IsValid))
                _ = InputSender.SendKeyAsync(key, KeyPressState.Up);
        }
        _heldActions.Clear();
        _scheduledReleases.Clear();
        _burstOn = false;
        _yawRemaining = 0;
    }

    public override Task OnPause()
    {
        ReleaseEverything();
        return base.OnPause();
    }

    public override void Dispose()
    {
        ReleaseEverything();
        UnsubscribeFromProfile();
        AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChanged;
        base.Dispose();
    }

    private static double DistSq(Prediction p, float cx, float cy)
    {
        var dx = p.CenterXTranslated - cx;
        var dy = p.CenterYTranslated - cy;
        return dx * dx + dy * dy;
    }
}
