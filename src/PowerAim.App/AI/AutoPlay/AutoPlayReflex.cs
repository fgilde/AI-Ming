namespace PowerAim.AI.AutoPlay;

/// <summary>What the reflex wants this frame: controls to hold, controls to tap, a mouse delta.</summary>
public sealed class ReflexCommand
{
    public HashSet<ControlRole> Hold { get; } = new();
    public List<ControlRole> Tap { get; } = new();
    public int MouseDx { get; set; }
    public int MouseDy { get; set; }

    /// <summary>The reflex has room for an ability this frame (in or out of combat).</summary>
    public bool WantsTactical { get; set; }

    public bool InCombat { get; set; }

    internal void Clear()
    {
        Hold.Clear();
        Tap.Clear();
        MouseDx = MouseDy = 0;
        WantsTactical = InCombat = false;
    }
}

/// <summary>Per-frame inputs besides the detections.</summary>
/// <param name="AimHandledExternally">The aim feature steers the mouse — the reflex must not.</param>
/// <param name="FireHandledExternally">An active AutoTrigger fires the shoot keys — the reflex must not.</param>
/// <param name="Stuck">Moving, but the view hasn't changed for a while (see <see cref="StuckDetector"/>).</param>
public readonly record struct ReflexContext(
    bool AimHandledExternally = false,
    bool FireHandledExternally = false,
    double? Ammo = null,
    double? Health = null,
    bool Stuck = false);

/// <summary>
///     The per-frame driver: fights when enemies are visible, explores otherwise, and follows the decision
///     model's <see cref="AutoPlayIntent"/>. Pure decision logic — it never touches input; the
///     <see cref="AutoPlayActuator"/> turns its <see cref="ReflexCommand"/> into key presses.
/// </summary>
public sealed class AutoPlayReflex
{
    private const double AimReleaseDelayMs = 700;
    private const double FireReleaseDelayMs = 300;
    private const double FireSettlingMs = 140;
    private const double BurstOnMs = 220;
    private const double BurstOffMs = 110;
    private const double SightingGapMs = 800;
    private const int MaxYawStep = 24;
    private const double PitchCorrectionPeriodSec = 4;
    private const double PitchCorrectionFraction = 0.25;
    private const int PitchCorrectionMaxStep = 14;
    private const double LowAmmo = 6, LowHealth = 30;
    private const double ReloadCooldownSec = 4, HealthRetreatSec = 2.5;

    /// <summary>Enemy box height (fraction of the capture height) below which it counts as far away.</summary>
    public const float FarSize = 0.12f;

    /// <summary>Enemy box height above which it is too close.</summary>
    public const float CloseSize = 0.45f;

    private readonly Random _rng;
    private readonly ReflexCommand _cmd = new();

    private DateTime _lastSeenEnemy = DateTime.MinValue;
    private DateTime _firstSighting = DateTime.MinValue;
    private bool _burstOn;
    private DateTime _burstChangedAt = DateTime.MinValue;
    private bool _shootHeld, _aimHeld;

    private string _exploreDir = "forward";
    private DateTime _commitDirectionUntil = DateTime.MinValue;
    private int _ticksWithoutEnemy;

    private double _yawRemaining, _yawPerTick;
    private DateTime _nextYawDecision = DateTime.MinValue;
    private int _lastYawSign;

    private double _pitchSum;
    private DateTime _lastPitchCorrection = DateTime.MinValue;

    private DateTime _lastReloadAt = DateTime.MinValue;
    private DateTime _healthRetreatUntil = DateTime.MinValue;
    private DateTime _unstickUntil = DateTime.MinValue;

    public AutoPlayReflex(Random? rng = null) => _rng = rng ?? new Random();

    /// <summary>Learned explore direction for a state label, or null (see <see cref="StateLabel"/>).</summary>
    public Func<string, string?>? LearnedDirection { get; set; }

    /// <summary>Notable events ("Combat", "OCR", "Stuck"), for the activity log.</summary>
    public event Action<string, string>? Event;

    public DateTime LastSeenEnemy => _lastSeenEnemy;

    /// <summary>Coarse state label for the learning model.</summary>
    public string StateLabel(bool inCombat) => inCombat
        ? (_burstOn ? "combat_burst" : "combat_aim")
        : (_ticksWithoutEnemy < 30 ? "explore_recent" : "explore_idle");

    /// <summary>The intent after low-health overrides: a retreat window beats the model's choice.</summary>
    public AutoPlayIntent Effective(AutoPlayIntent intent, DateTime now)
        => now < _healthRetreatUntil ? intent with { Mode = "retreat" } : intent;

    public ReflexCommand Tick(AutoPlayPerception seen, AutoPlayIntent intent, ReflexContext ctx, DateTime now)
    {
        _cmd.Clear();
        ReactToHud(ctx, now);
        intent = Effective(intent, now);

        if (seen.Nearest != null)
        {
            if ((now - _lastSeenEnemy).TotalMilliseconds > SightingGapMs)
            {
                _firstSighting = now;
                Event?.Invoke("Combat", $"{seen.EnemyCount} enem{(seen.EnemyCount == 1 ? "y" : "ies")}");
            }
            _lastSeenEnemy = now;
            _ticksWithoutEnemy = 0;
            Combat(seen, intent, ctx, now);
        }
        else
        {
            _ticksWithoutEnemy++;
            KeepAimBriefly(now);
            Explore(intent, ctx, now);
            _cmd.WantsTactical = true;
        }

        StepYaw();
        CorrectPitch(now);
        _shootHeld = _cmd.Hold.Contains(ControlRole.Shoot);
        _aimHeld = _cmd.Hold.Contains(ControlRole.Aim);
        return _cmd;
    }

    /// <summary>Forget held state, e.g. when AutoPlay pauses (all keys get released).</summary>
    public void Reset()
    {
        _burstOn = _shootHeld = _aimHeld = false;
        _yawRemaining = 0;
    }

    // ------------------------------------------------------------------ combat

    private void Combat(AutoPlayPerception seen, AutoPlayIntent intent, ReflexContext ctx, DateTime now)
    {
        var e = seen.Nearest!;
        _cmd.InCombat = true;
        _cmd.Hold.Add(ControlRole.Aim);

        if (!ctx.AimHandledExternally)
            AddMouse((int)Math.Clamp(e.Dx * 0.20, -40, 40), (int)Math.Clamp(e.Dy * 0.18, -30, 30));

        bool settled = (now - _firstSighting).TotalMilliseconds >= FireSettlingMs;
        bool onTarget = ctx.AimHandledExternally || (Math.Abs(e.Dx) < 90 && Math.Abs(e.Dy) < 90);
        if (settled && onTarget && !ctx.FireHandledExternally) Burst(now);
        else _burstOn = false;

        if (e.Dx < -160) _cmd.Hold.Add(ControlRole.MoveLeft);
        else if (e.Dx > 160) _cmd.Hold.Add(ControlRole.MoveRight);

        // Range from box size: a small box is far away, a huge one is in our face.
        float size = seen.NearestSize;
        bool push = intent.Mode == "engage";
        if (intent.Mode == "retreat") _cmd.Hold.Add(ControlRole.MoveBackward);
        else if (size < (push ? FarSize * 1.5f : FarSize)) _cmd.Hold.Add(ControlRole.MoveForward);
        else if (size > (push ? CloseSize * 1.3f : CloseSize)) _cmd.Hold.Add(ControlRole.MoveBackward);

        _cmd.WantsTactical = seen.EnemyCount >= 2;
    }

    private void Burst(DateTime now)
    {
        double sinceChange = (now - _burstChangedAt).TotalMilliseconds;
        if (!_burstOn && sinceChange >= BurstOffMs) { _burstOn = true; _burstChangedAt = now; }
        else if (_burstOn && sinceChange >= BurstOnMs) { _burstOn = false; _burstChangedAt = now; }
        if (_burstOn) _cmd.Hold.Add(ControlRole.Shoot);
    }

    /// <summary>Keep firing / aiming a moment after the enemy vanishes, so a one-frame miss doesn't drop them.</summary>
    private void KeepAimBriefly(DateTime now)
    {
        double ms = (now - _lastSeenEnemy).TotalMilliseconds;
        if (_shootHeld && ms <= FireReleaseDelayMs) _cmd.Hold.Add(ControlRole.Shoot);
        else _burstOn = false;
        if (_aimHeld && ms <= AimReleaseDelayMs) _cmd.Hold.Add(ControlRole.Aim);
    }

    // ------------------------------------------------------------------ exploration

    private void Explore(AutoPlayIntent intent, ReflexContext ctx, DateTime now)
    {
        if (ctx.Stuck && now >= _unstickUntil)
        {
            // Walking into a wall: turn away hard, hop, and commit to a sideways direction for a moment.
            int sign = _lastYawSign == 0 ? (_rng.Next(2) == 0 ? -1 : 1) : -_lastYawSign;
            _lastYawSign = sign;
            _yawRemaining = sign * _rng.Next(300, 500);
            _yawPerTick = _yawRemaining / 10;
            _nextYawDecision = now.AddSeconds(2);
            _exploreDir = sign < 0 ? "left" : "right";
            _commitDirectionUntil = now.AddSeconds(1.5);
            _unstickUntil = now.AddSeconds(2);
            _cmd.Tap.Add(ControlRole.Jump);
            Event?.Invoke("Stuck", "turning away");
        }
        else if (now > _commitDirectionUntil)
        {
            _exploreDir = PickDirection(intent);
            _commitDirectionUntil = now.AddSeconds(_rng.NextDouble() * 3 + 4);
        }

        if (intent.Mode != "hold")
        {
            _cmd.Hold.Add(ControlRole.Sprint);
            bool fwd = _exploreDir.Contains("forward"), back = _exploreDir.Contains("back");
            bool left = _exploreDir.Contains("left"), right = _exploreDir.Contains("right");
            if (fwd) _cmd.Hold.Add(ControlRole.MoveForward);
            if (back) _cmd.Hold.Add(ControlRole.MoveBackward);
            if (left) _cmd.Hold.Add(ControlRole.MoveLeft);
            if (right) _cmd.Hold.Add(ControlRole.MoveRight);
            if (_rng.Next(400) < 1) _cmd.Tap.Add(ControlRole.Jump);
        }

        if (now > _nextYawDecision && Math.Abs(_yawRemaining) < 1)
            ScheduleYawScan(now);
    }

    /// <summary>Retreat goes back; a model's direction is honoured 70 % of the time; else learned or random.</summary>
    private string PickDirection(AutoPlayIntent intent)
    {
        if (intent.Mode == "retreat") return "back";

        if (!string.IsNullOrEmpty(intent.Direction) && _rng.NextDouble() < 0.7)
        {
            return intent.Direction switch
            {
                "backward" => "back",
                "left" => "forward_left",
                "right" => "forward_right",
                _ => "forward",
            };
        }

        var learned = LearnedDirection?.Invoke(StateLabel(false));
        if (learned != null) return learned;

        string[] patterns = ["forward", "forward", "forward", "forward", "forward_left", "forward_right", "left", "right"];
        return patterns[_rng.Next(patterns.Length)];
    }

    private void ScheduleYawScan(DateTime now)
    {
        _nextYawDecision = now.AddSeconds(_rng.NextDouble() * 2 + 1.5);
        // Alternate with 20 % randomness — pure random produced visible one-sided streaks.
        int sign = _lastYawSign == 0 || _rng.NextDouble() < 0.20 ? (_rng.Next(2) == 0 ? -1 : 1) : -_lastYawSign;
        _lastYawSign = sign;
        int magnitude = _ticksWithoutEnemy > 200 ? _rng.Next(220, 360) : _rng.Next(80, 180);
        _yawRemaining = sign * magnitude;
        _yawPerTick = _yawRemaining / _rng.Next(8, 18);
    }

    private void StepYaw()
    {
        if (Math.Abs(_yawRemaining) < 1) return;
        int step = (int)Math.Round(_yawPerTick);
        if (step == 0) step = Math.Sign(_yawRemaining);
        step = Math.Clamp(step, -MaxYawStep, MaxYawStep);
        AddMouse(step, 0);
        _yawRemaining -= step;
    }

    /// <summary>Undo part of the pitch we sent ourselves every few seconds so the view doesn't drift up or down.</summary>
    private void CorrectPitch(DateTime now)
    {
        if ((now - _lastPitchCorrection).TotalSeconds < PitchCorrectionPeriodSec) return;
        _lastPitchCorrection = now;
        if (Math.Abs(_pitchSum) < 6) return;
        AddMouse(0, (int)Math.Round(Math.Clamp(-_pitchSum * PitchCorrectionFraction, -PitchCorrectionMaxStep, PitchCorrectionMaxStep)));
    }

    private void AddMouse(int dx, int dy)
    {
        _cmd.MouseDx += dx;
        _cmd.MouseDy += dy;
        _pitchSum += dy;
    }

    // ------------------------------------------------------------------ HUD (OCR)

    private void ReactToHud(ReflexContext ctx, DateTime now)
    {
        if (ctx.Ammo is { } ammo && ammo <= LowAmmo && (now - _lastReloadAt).TotalSeconds > ReloadCooldownSec)
        {
            _cmd.Tap.Add(ControlRole.Reload);
            _lastReloadAt = now;
            Event?.Invoke("OCR", $"reload (ammo={ammo:0})");
        }
        if (ctx.Health is { } hp && hp <= LowHealth)
        {
            if (now >= _healthRetreatUntil) Event?.Invoke("OCR", $"retreat (hp={hp:0})");
            _healthRetreatUntil = now.AddSeconds(HealthRetreatSec);
        }
    }
}
