using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.InputLogic.Contracts;
using SharpDX.XInput;

namespace PowerAim.InputLogic.Mapping;

/// <summary>
///     Background service that reads physical input devices and writes synthesized targets based
///     on the currently-active <see cref="ControllerMappingProfile"/>.
///     <para>
///     Two directions are supported:
///     <list type="bullet">
///       <item>Source = keyboard/mouse, target = gamepad → drives a <see cref="VirtualGamepadHost"/>
///             (ViGEm). Used to play "gamepad-only" titles with KB+M.</item>
///       <item>Source = gamepad, target = keyboard/mouse → uses <see cref="SendInput"/> via the
///             native helpers. Used to play "KB-only" titles with a controller.</item>
///     </list>
///     Hot-reloads on profile / mapping changes via INotifyPropertyChanged on the config tree.
///     Only one profile is active at a time (first <see cref="ControllerMappingProfile.Enabled"/>
///     match whose <see cref="ControllerMappingProfile.MatchProcess"/> resolves).
///     </para>
/// </summary>
public sealed class MappingEngine : INotifyPropertyChanged, IDisposable
{
    private static readonly Lazy<MappingEngine> _lazy = new(() => new MappingEngine());
    public static MappingEngine Instance => _lazy.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ---- State ----
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private IKeyboardMouseEvents? _hook;
    // No standalone VirtualGamepadHost anymore — we drive GamepadManager.GamepadSender so the
    // AI loop, trigger pipeline and mapping engine all push to the SAME virtual pad. Two
    // parallel ViGEm clients competing for slots is the root cause of the games-don't-see-it
    // problem we kept chasing.
    private Controller? _physicalPad;
    private ControllerMappingProfile? _activeProfile;
    private string _status = "Idle";

    // Pressed state tracking — sources that are currently held. Used to fire press / release pairs.
    private readonly HashSet<(MappingInputKind, int)> _heldSources = new();

    // Activator bookkeeping — per-mapping timestamps / latch state so press / long-press /
    // double-tap / toggle / pulse can all coexist in a single profile.
    private readonly Dictionary<InputMapping, long> _activatorPressedAt = new();
    private readonly Dictionary<InputMapping, long> _activatorLastReleaseAt = new();
    private readonly HashSet<InputMapping> _toggleHeld = new();
    private readonly Dictionary<InputMapping, long> _pulseUntil = new();

    // Mouse-to-stick state
    private long _lastMouseDeltaTickMs;
    private int _accumulatedMouseDx, _accumulatedMouseDy;

    public string Status
    {
        get => _status;
        private set { _status = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); }
    }

    public ControllerMappingProfile? ActiveProfile => _activeProfile;

    /// <summary>
    ///     Replace <see cref="_activeProfile"/> AND fire <see cref="PropertyChanged"/> for
    ///     <see cref="ActiveProfile"/> so subscribers (the mapping page's "Engine: …" status line)
    ///     repaint when the engine picks a different profile. Without this the UI would only react
    ///     to <see cref="Status"/> changes and never reflect which profile is live.
    ///     <para>
    ///     Also takes ownership of every virtual-pad channel the profile targets (Pause + Resume
    ///     on the shared <see cref="GamepadManager.GamepadSender"/>). Without that step the sender's
    ///     <c>SyncLoop</c> would mirror the physical pad's state onto the virtual pad every 1 ms,
    ///     immediately overwriting whatever we just wrote — so e.g. pressing <c>W</c> looked like
    ///     nothing happened in the gamepad tester. Pause/Resume scopes the sync mirror to channels
    ///     the mapping engine does NOT own.
    ///     </para>
    /// </summary>
    private void SetActiveProfile(ControllerMappingProfile? p)
    {
        if (ReferenceEquals(_activeProfile, p)) return;
        if (_activeProfile != null) ResumeOwnedChannels(_activeProfile);
        _activeProfile = p;
        if (p != null) PauseOwnedChannels(p);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveProfile)));
    }

    /// <summary>
    ///     Pause every virtual-pad channel that <paramref name="profile"/> writes to. Once paused,
    ///     the shared sender's <c>SyncLoop</c> no longer mirrors the physical pad onto that
    ///     channel — meaning our <c>SetButton/Slider/Axis</c> writes survive instead of being
    ///     instantly overwritten.
    /// </summary>
    private static void PauseOwnedChannels(ControllerMappingProfile profile)
    {
        if (!VirtualReady) return;
        var sender = GamepadManager.GamepadSender!;
        foreach (var m in profile.Mappings)
        {
            if (!m.Enabled) continue;
            switch (m.TargetKind)
            {
                case MappingInputKind.GamepadButton:
                    sender.PauseSync(((XboxButtonId)m.TargetCode).ToGamepadButton());
                    break;
                case MappingInputKind.GamepadTrigger:
                    sender.PauseSync(m.TargetCode == 0 ? GamepadSlider.LeftTrigger : GamepadSlider.RightTrigger);
                    break;
                case MappingInputKind.GamepadStickDirection:
                    var (ax, _) = AxisForStickDirection((GamepadStickDirection)m.TargetCode);
                    sender.PauseSync(ax);
                    break;
            }
        }
        // Mouse-to-stick sentinel takes RightThumb X+Y regardless of source.
        if (profile.Mappings.Any(m => m.Enabled
                                       && m.SourceKind == MappingInputKind.MouseButton
                                       && m.SourceCode == MouseMotionSentinel))
        {
            sender.PauseSync(GamepadAxis.RightThumbX);
            sender.PauseSync(GamepadAxis.RightThumbY);
        }
    }

    private static void ResumeOwnedChannels(ControllerMappingProfile profile)
    {
        if (!VirtualReady) return;
        var sender = GamepadManager.GamepadSender!;
        foreach (var m in profile.Mappings)
        {
            if (!m.Enabled) continue;
            switch (m.TargetKind)
            {
                case MappingInputKind.GamepadButton:
                    sender.ResumeSync(((XboxButtonId)m.TargetCode).ToGamepadButton());
                    break;
                case MappingInputKind.GamepadTrigger:
                    sender.ResumeSync(m.TargetCode == 0 ? GamepadSlider.LeftTrigger : GamepadSlider.RightTrigger);
                    break;
                case MappingInputKind.GamepadStickDirection:
                    var (ax, _) = AxisForStickDirection((GamepadStickDirection)m.TargetCode);
                    sender.ResumeSync(ax);
                    break;
            }
        }
        if (profile.Mappings.Any(m => m.Enabled
                                       && m.SourceKind == MappingInputKind.MouseButton
                                       && m.SourceCode == MouseMotionSentinel))
        {
            sender.ResumeSync(GamepadAxis.RightThumbX);
            sender.ResumeSync(GamepadAxis.RightThumbY);
        }
    }

    /// <summary>Helper — what axis/sign a stick direction maps onto.</summary>
    private static (GamepadAxis Axis, short PositiveDir) AxisForStickDirection(GamepadStickDirection d) => d switch
    {
        GamepadStickDirection.LeftStickUp     => (GamepadAxis.LeftThumbY, +1),
        GamepadStickDirection.LeftStickDown   => (GamepadAxis.LeftThumbY, -1),
        GamepadStickDirection.LeftStickLeft   => (GamepadAxis.LeftThumbX, -1),
        GamepadStickDirection.LeftStickRight  => (GamepadAxis.LeftThumbX, +1),
        GamepadStickDirection.RightStickUp    => (GamepadAxis.RightThumbY, +1),
        GamepadStickDirection.RightStickDown  => (GamepadAxis.RightThumbY, -1),
        GamepadStickDirection.RightStickLeft  => (GamepadAxis.RightThumbX, -1),
        GamepadStickDirection.RightStickRight => (GamepadAxis.RightThumbX, +1),
        _ => (GamepadAxis.LeftThumbX, +1),
    };

    // ============================================================================ LIFECYCLE ====

    /// <summary>Start the engine. Idempotent. Re-evaluates the active profile every loop tick.</summary>
    public void Start()
    {
        if (_loopTask is { IsCompleted: false }) return;
        _cts = new CancellationTokenSource();
        try
        {
            _hook = Hook.GlobalEvents();
            _hook.KeyDown      += OnKeyDown;
            _hook.KeyUp        += OnKeyUp;
            _hook.MouseDownExt += OnMouseDown;
            _hook.MouseUpExt   += OnMouseUp;
            _hook.MouseMoveExt += OnMouseMove;
        }
        catch (Exception ex)
        {
            Status = $"Hook failed: {ex.Message}";
            return;
        }

        try { _physicalPad = new Controller(UserIndex.One); if (!_physicalPad.IsConnected) _physicalPad = null; }
        catch { _physicalPad = null; }

        _loopTask = Task.Run(() => Loop(_cts.Token));
        Status = "Running";
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _loopTask?.Wait(500); } catch { }
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
        if (_hook != null)
        {
            _hook.KeyDown      -= OnKeyDown;
            _hook.KeyUp        -= OnKeyUp;
            _hook.MouseDownExt -= OnMouseDown;
            _hook.MouseUpExt   -= OnMouseUp;
            _hook.MouseMoveExt -= OnMouseMove;
            _hook.Dispose();
            _hook = null;
        }
        IdleVirtual();
        _heldSources.Clear();
        _activatorPressedAt.Clear();
        _activatorLastReleaseAt.Clear();
        _toggleHeld.Clear();
        _pulseUntil.Clear();
        Status = "Stopped";
    }

    public void Dispose()
    {
        Stop();
    }

    // ===========================================================================
    //  Virtual-pad accessors — route through the SHARED GamepadManager.GamepadSender
    //  so we don't run a parallel ViGEm client. CanWork gates everything.
    // ===========================================================================

    private static bool VirtualReady =>
        GamepadManager.CanSend && GamepadManager.GamepadSender != null;

    private static void SendVButton(GamepadButton b, bool pressed)
    {
        GamepadManager.GamepadSender?.SetButtonState(b, pressed);
    }
    private static void SendVSlider(GamepadSlider s, byte value)
    {
        GamepadManager.GamepadSender?.SetSliderValue(s, value);
    }
    private static void SendVAxis(GamepadAxis a, short value)
    {
        GamepadManager.GamepadSender?.SetAxisValue(a, value);
    }

    /// <summary>Reset the virtual pad to neutral — used when the active profile changes or stops.</summary>
    private static void IdleVirtual()
    {
        if (!VirtualReady) return;
        foreach (GamepadButton b in Enum.GetValues<GamepadButton>())
            SendVButton(b, false);
        SendVSlider(GamepadSlider.LeftTrigger, 0);
        SendVSlider(GamepadSlider.RightTrigger, 0);
        SendVAxis(GamepadAxis.LeftThumbX, 0);
        SendVAxis(GamepadAxis.LeftThumbY, 0);
        SendVAxis(GamepadAxis.RightThumbX, 0);
        SendVAxis(GamepadAxis.RightThumbY, 0);
    }

    // ============================================================================ LOOP ====

    private async Task Loop(CancellationToken ct)
    {
        // 8 ms ≈ 125 Hz — fast enough for stick→mouse smoothness and physical-pad polling.
        while (!ct.IsCancellationRequested)
        {
            ResolveActiveProfile();
            if (_activeProfile == null)
            {
                IdleVirtual();
                try { await Task.Delay(80, ct); } catch { break; }
                continue;
            }

            if (NeedsVirtualPad(_activeProfile) && !VirtualReady)
            {
                Status = "ViGEm sender unavailable — install ViGEmBus and restart PowerAim.";
            }

            // Pad→KB: poll physical controller state, fire mappings whose source is a gamepad.
            if (NeedsPhysicalPad(_activeProfile))
            {
                if (_physicalPad == null || !_physicalPad.IsConnected)
                {
                    try { _physicalPad = new Controller(UserIndex.One); if (!_physicalPad.IsConnected) _physicalPad = null; }
                    catch { _physicalPad = null; }
                }
                if (_physicalPad != null) PollPhysicalPad(_activeProfile);
            }

            // KB→Pad mouse-to-stick: drain accumulated delta and feed into virtual stick.
            ApplyMouseToStick(_activeProfile);

            // Long-press / pulse follow-up tick.
            TickActivators();

            // Re-pause owned channels every tick (cheap — internal HashSet.Add). Necessary because
            // the user can add/remove mappings while a profile is active and the sender doesn't
            // know about those topology changes.
            PauseOwnedChannels(_activeProfile);

            try { await Task.Delay(8, ct); } catch { break; }
        }
    }

    private void ResolveActiveProfile()
    {
        // Master kill-switch — when the user toggles "Mapping active" off, drop the active
        // profile and idle the virtual pad. The engine loop keeps ticking but does nothing
        // until the user flips it back on (or hits the hotkey).
        if (AppConfig.Current == null)
        {
            Status = "Waiting for config…";
            SetActiveProfile(null);
            return;
        }
        if (AppConfig.Current.ToggleState?.MappingActive != true)
        {
            if (_activeProfile != null)
            {
                IdleVirtual();
                _heldSources.Clear();
            }
            Status = "Idle — master toggle is OFF";
            SetActiveProfile(null);
            return;
        }
        var profiles = AppConfig.Current.ControllerMappingProfiles;
        if (profiles == null || profiles.Count == 0)
        {
            Status = "Idle — no mapping profiles defined";
            SetActiveProfile(null);
            return;
        }
        var focused = PowerAim.Class.WindowFocusWatcher.Instance.CurrentProcessName;
        bool anyEnabled = false;
        foreach (var p in profiles)
        {
            if (!p.Enabled) continue;
            anyEnabled = true;
            if (!string.IsNullOrWhiteSpace(p.MatchProcess)
                && !PowerAim.Class.ProcessMatcher.Matches(p.MatchProcess, focused))
                continue;
            if (!ReferenceEquals(_activeProfile, p))
            {
                IdleVirtual();
                _heldSources.Clear();
            }
            SetActiveProfile(p);
            Status = $"Running — '{p.Name}' active";
            return;
        }
        if (_activeProfile != null)
        {
            IdleVirtual();
            _heldSources.Clear();
        }
        Status = anyEnabled
            ? "Idle — enabled profile(s) exist but MatchProcess didn't match the focused window"
            : "Idle — no profile has Enabled=true";
        SetActiveProfile(null);
    }

    private static bool NeedsVirtualPad(ControllerMappingProfile p)
        => p.Mappings.Any(m => m.Enabled && IsGamepadKind(m.TargetKind));

    private static bool NeedsPhysicalPad(ControllerMappingProfile p)
        => p.Mappings.Any(m => m.Enabled && IsGamepadKind(m.SourceKind));

    private static bool IsGamepadKind(MappingInputKind k)
        => k == MappingInputKind.GamepadButton
        || k == MappingInputKind.GamepadTrigger
        || k == MappingInputKind.GamepadStickDirection;

    // ============================================================================ KB/MOUSE → PAD ====

    private void OnKeyDown(object? s, KeyEventArgs e) => HandleSourcePress(MappingInputKind.KeyboardKey, (int)e.KeyCode, true);
    private void OnKeyUp(object? s, KeyEventArgs e)   => HandleSourcePress(MappingInputKind.KeyboardKey, (int)e.KeyCode, false);
    private void OnMouseDown(object? s, MouseEventExtArgs e) => HandleSourcePress(MappingInputKind.MouseButton, (int)e.Button, true);
    private void OnMouseUp(object? s, MouseEventExtArgs e)   => HandleSourcePress(MappingInputKind.MouseButton, (int)e.Button, false);

    private void OnMouseMove(object? s, MouseEventExtArgs e)
    {
        // Accumulator drained by the loop. Read absolute coords via the existing cursor helper
        // because MouseKeyHook only reports absolute positions, not deltas.
        var p = NativeAPIMethods.GetCursorPosition();
        if (_lastMouseDeltaTickMs == 0)
        {
            _lastMouseDeltaTickMs = Environment.TickCount64;
            _lastMouseX = p.X; _lastMouseY = p.Y;
            return;
        }
        _accumulatedMouseDx += p.X - _lastMouseX;
        _accumulatedMouseDy += p.Y - _lastMouseY;
        _lastMouseX = p.X; _lastMouseY = p.Y;
    }
    private int _lastMouseX, _lastMouseY;

    private void HandleSourcePress(MappingInputKind kind, int code, bool pressed)
    {
        var profile = _activeProfile;
        if (profile == null) return;

        bool changed = pressed
            ? _heldSources.Add((kind, code))
            : _heldSources.Remove((kind, code));
        if (!changed) return;

        foreach (var m in profile.Mappings)
        {
            if (!m.Enabled) continue;
            if (m.SourceKind != kind || m.SourceCode != code) continue;
            if (!IsDirectionAllowed(m)) continue;
            if (!ModifierSatisfied(m)) continue;
            ApplyActivator(m, pressed);
        }
    }

    /// <summary>True when the mapping has no modifier set OR the modifier source is currently held.</summary>
    private bool ModifierSatisfied(InputMapping m)
    {
        if (m.ModifierKind == MappingInputKind.None) return true;
        return _heldSources.Contains((m.ModifierKind, m.ModifierCode))
            || _padHeld.Contains(m.ModifierCode); // gamepad-source modifier
    }

    /// <summary>
    ///     Translate raw source press/release into a target press/release using the configured
    ///     <see cref="MappingActivator"/>. Press-style is the default and just forwards. Long-press,
    ///     double-tap, toggle and pulse handle their own timing.
    /// </summary>
    private void ApplyActivator(InputMapping m, bool pressed)
    {
        long now = Environment.TickCount64;
        switch (m.Activator)
        {
            case MappingActivator.Press:
                ApplyTarget(m, pressed);
                break;

            case MappingActivator.LongPress:
                if (pressed)
                {
                    _activatorPressedAt[m] = now;
                    // Schedule a check — done in the loop tick so we don't need a dedicated timer.
                }
                else
                {
                    _activatorPressedAt.Remove(m);
                    ApplyTarget(m, false); // release in case we'd already fired
                }
                break;

            case MappingActivator.DoubleTap:
                if (pressed)
                {
                    long last = _activatorLastReleaseAt.GetValueOrDefault(m, 0);
                    if (now - last < 320)
                    {
                        ApplyTarget(m, true);
                        _pulseUntil[m] = now + Math.Max(60, m.LongPressMs);
                        _activatorLastReleaseAt[m] = 0;
                    }
                }
                else
                {
                    _activatorLastReleaseAt[m] = now;
                }
                break;

            case MappingActivator.Toggle:
                if (pressed)
                {
                    if (_toggleHeld.Remove(m))
                    {
                        ApplyTarget(m, false);
                    }
                    else
                    {
                        _toggleHeld.Add(m);
                        ApplyTarget(m, true);
                    }
                }
                break;

            case MappingActivator.Pulse:
                if (pressed)
                {
                    ApplyTarget(m, true);
                    _pulseUntil[m] = now + Math.Max(40, m.LongPressMs);
                }
                break;
        }
    }

    /// <summary>
    ///     Tick-driven activator follow-up: fires long-press targets once their hold time elapses
    ///     and releases pulse/double-tap targets after their pulse duration. Called from
    ///     <see cref="Loop"/>.
    /// </summary>
    private void TickActivators()
    {
        long now = Environment.TickCount64;
        // Long-press follow-ups.
        if (_activatorPressedAt.Count > 0)
        {
            // Snapshot to allow modifying the dictionary inside the loop.
            foreach (var kv in _activatorPressedAt.ToArray())
            {
                var m = kv.Key;
                if (now - kv.Value >= Math.Max(50, m.LongPressMs))
                {
                    ApplyTarget(m, true);
                    _activatorPressedAt.Remove(m);
                    _pulseUntil[m] = now + 80; // brief auto-release
                }
            }
        }
        // Pulse releases.
        if (_pulseUntil.Count > 0)
        {
            foreach (var kv in _pulseUntil.ToArray())
            {
                if (now >= kv.Value)
                {
                    ApplyTarget(kv.Key, false);
                    _pulseUntil.Remove(kv.Key);
                }
            }
        }
    }

    /// <summary>
    ///     Filter mappings by the user-selected runtime <see cref="AppConfig.MappingDirection"/>.
    ///     Mappings whose source is gamepad-ish fire only in <c>Both</c> or
    ///     <c>ControllerToKeyboard</c> mode; KB/Mouse-sourced mappings fire only in <c>Both</c>
    ///     or <c>KeyboardToController</c>. Lets one profile carry both directions and the user
    ///     flip without editing.
    /// </summary>
    private static bool IsDirectionAllowed(InputMapping m)
    {
        var dir = AppConfig.Current?.MappingDirection ?? MappingDirection.Both;
        if (dir == MappingDirection.Both) return true;
        bool srcIsGamepad = IsGamepadKind(m.SourceKind);
        return dir switch
        {
            MappingDirection.ControllerToKeyboard => srcIsGamepad,
            MappingDirection.KeyboardToController => !srcIsGamepad,
            _ => true
        };
    }

    private void ApplyTarget(InputMapping m, bool pressed)
    {
        switch (m.TargetKind)
        {
            case MappingInputKind.GamepadButton:
                SendVButton(((XboxButtonId)m.TargetCode).ToGamepadButton(), pressed);
                break;
            case MappingInputKind.GamepadTrigger:
                SendVSlider(m.TargetCode == 0 ? GamepadSlider.LeftTrigger : GamepadSlider.RightTrigger,
                                       pressed ? (byte)255 : (byte)0);
                break;
            case MappingInputKind.GamepadStickDirection:
                ApplyStickDirection((GamepadStickDirection)m.TargetCode, pressed);
                break;
            case MappingInputKind.KeyboardKey:
                SendKey((Keys)m.TargetCode, pressed);
                break;
            case MappingInputKind.MouseButton:
                SendMouse((MouseButtons)m.TargetCode, pressed);
                break;
        }
    }

    private void ApplyStickDirection(GamepadStickDirection dir, bool pressed)
    {
        // Full deflection in the selected direction. Combining horizontal+vertical would need a
        // proper accumulator — for v1 we accept that opposing keys cancel out, which is what
        // games expect (W releases when S is pressed, etc.).
        short value = pressed ? short.MaxValue : (short)0;
        short neg = pressed ? short.MinValue : (short)0;
        switch (dir)
        {
            case GamepadStickDirection.LeftStickUp:    SendVAxis(GamepadAxis.LeftThumbY,  value); break;
            case GamepadStickDirection.LeftStickDown:  SendVAxis(GamepadAxis.LeftThumbY,  neg);   break;
            case GamepadStickDirection.LeftStickLeft:  SendVAxis(GamepadAxis.LeftThumbX,  neg);   break;
            case GamepadStickDirection.LeftStickRight: SendVAxis(GamepadAxis.LeftThumbX,  value); break;
            case GamepadStickDirection.RightStickUp:   SendVAxis(GamepadAxis.RightThumbY, value); break;
            case GamepadStickDirection.RightStickDown: SendVAxis(GamepadAxis.RightThumbY, neg);   break;
            case GamepadStickDirection.RightStickLeft: SendVAxis(GamepadAxis.RightThumbX, neg);   break;
            case GamepadStickDirection.RightStickRight:SendVAxis(GamepadAxis.RightThumbX, value); break;
        }
    }

    /// <summary>
    ///     Treats horizontal mouse motion as right-stick X and vertical motion as right-stick Y
    ///     (inverted because in FPS conventions, push stick up = look up = lift the mouse). Only
    ///     applies if the profile contains a mapping with MouseButton-code 0xFFFF (sentinel for
    ///     "mouse axis"). Keeps mouse free of side-effects for profiles that don't opt in.
    /// </summary>
    private const int MouseMotionSentinel = 0xFFFF;

    private void ApplyMouseToStick(ControllerMappingProfile profile)
    {
        if (_accumulatedMouseDx == 0 && _accumulatedMouseDy == 0) return;
        // Has any "MouseAxis → RightStick" mapping AND is its direction allowed right now?
        // (Source is mouse-motion = a KB+M source, so it must respect the KeyboardToController /
        // Both filter — without this gate the right stick of the virtual pad would still get
        // driven even when the user said "only pad → KB".)
        bool wantsMouseRStick = profile.Mappings.Any(m =>
            m.Enabled
            && m.SourceKind == MappingInputKind.MouseButton
            && m.SourceCode == MouseMotionSentinel
            && IsDirectionAllowed(m)
            && (m.TargetKind == MappingInputKind.GamepadStickDirection
                || m.TargetKind == MappingInputKind.GamepadButton)); // tolerate quirky targets
        if (!wantsMouseRStick) { _accumulatedMouseDx = 0; _accumulatedMouseDy = 0; return; }

        double sens = Math.Max(0.05, profile.MouseToStickSensitivity);
        double scale = sens * 327.67; // 100px@sens=1.0 ≈ full deflection
        double sx = Math.Clamp(_accumulatedMouseDx * scale, short.MinValue, short.MaxValue);
        double sy = Math.Clamp(-_accumulatedMouseDy * scale, short.MinValue, short.MaxValue);
        SendVAxis(GamepadAxis.RightThumbX, (short)sx);
        SendVAxis(GamepadAxis.RightThumbY, (short)sy);
        _accumulatedMouseDx = 0;
        _accumulatedMouseDy = 0;
    }

    // ============================================================================ PAD → KB/MOUSE ====

    private readonly HashSet<int> _padHeld = new();

    private void PollPhysicalPad(ControllerMappingProfile profile)
    {
        if (_physicalPad == null) return;
        State state;
        try { state = _physicalPad.GetState(); }
        catch { return; }
        var flags = state.Gamepad.Buttons;
        // Buttons. CRITICAL: profiles persist gamepad source codes as XboxButtonId indices
        // (A=10, RightThumb=7, …) — NOT as XInput bit flags (A=0x1000, RightThumb=0x80). Use
        // the central extensions in Contracts + Mapping to translate flag → GamepadButton →
        // XboxButtonId so there's exactly one source of truth (Pad→KB never fired before this
        // translation existed).
        foreach (GamepadButtonFlags f in Enum.GetValues<GamepadButtonFlags>())
        {
            if (f == GamepadButtonFlags.None) continue;
            var gb = f.ToGamepadButton();
            if (gb == null) continue; // unknown / composite flag, skip
            DispatchPadButton((int)gb.Value.ToXboxButtonId(), flags.HasFlag(f), profile);
        }
        // Triggers (digital threshold at 128).
        DispatchPadButton(unchecked((int)0x80000001), state.Gamepad.LeftTrigger  > 128, profile);
        DispatchPadButton(unchecked((int)0x80000002), state.Gamepad.RightTrigger > 128, profile);
        // Stick directions (digital deflection > 16000).
        DispatchStick((int)GamepadStickDirection.LeftStickUp,    state.Gamepad.LeftThumbY  >  16000, profile);
        DispatchStick((int)GamepadStickDirection.LeftStickDown,  state.Gamepad.LeftThumbY  < -16000, profile);
        DispatchStick((int)GamepadStickDirection.LeftStickLeft,  state.Gamepad.LeftThumbX  < -16000, profile);
        DispatchStick((int)GamepadStickDirection.LeftStickRight, state.Gamepad.LeftThumbX  >  16000, profile);
        DispatchStick((int)GamepadStickDirection.RightStickUp,   state.Gamepad.RightThumbY >  16000, profile);
        DispatchStick((int)GamepadStickDirection.RightStickDown, state.Gamepad.RightThumbY < -16000, profile);
        DispatchStick((int)GamepadStickDirection.RightStickLeft, state.Gamepad.RightThumbX < -16000, profile);
        DispatchStick((int)GamepadStickDirection.RightStickRight,state.Gamepad.RightThumbX >  16000, profile);
        // Continuous right-stick → mouse motion (only fires if profile has the sentinel mapping).
        ApplyStickToMouse(profile, state);
    }

    private void DispatchPadButton(int code, bool down, ControllerMappingProfile profile)
    {
        bool was = _padHeld.Contains(code);
        if (down == was) return;
        if (down) _padHeld.Add(code); else _padHeld.Remove(code);
        foreach (var m in profile.Mappings)
        {
            if (!m.Enabled) continue;
            if (m.SourceKind != MappingInputKind.GamepadButton && m.SourceKind != MappingInputKind.GamepadTrigger) continue;
            if (m.SourceCode != code) continue;
            if (!IsDirectionAllowed(m)) continue;
            if (!ModifierSatisfied(m)) continue;
            ApplyActivator(m, down);
        }
    }

    private void DispatchStick(int code, bool down, ControllerMappingProfile profile)
    {
        int packed = unchecked((int)0xC0000000) | code; // distinct namespace from button flags
        bool was = _padHeld.Contains(packed);
        if (down == was) return;
        if (down) _padHeld.Add(packed); else _padHeld.Remove(packed);
        foreach (var m in profile.Mappings)
        {
            if (!m.Enabled) continue;
            if (m.SourceKind != MappingInputKind.GamepadStickDirection) continue;
            if (m.SourceCode != code) continue;
            if (!IsDirectionAllowed(m)) continue;
            if (!ModifierSatisfied(m)) continue;
            ApplyActivator(m, down);
        }
    }

    private void ApplyStickToMouse(ControllerMappingProfile profile, State state)
    {
        // Opt-in via a sentinel mapping: source RightStickRight + target MouseButton with code
        // 0xFFFF means "feed right stick into mouse motion". Source is gamepad-side, so this
        // must also obey the direction filter — otherwise pulling the physical stick still
        // moves the OS cursor even when the user has set 'Keyboard → Controller'.
        bool wantsStickToMouse = profile.Mappings.Any(m =>
            m.Enabled
            && m.SourceKind == MappingInputKind.GamepadStickDirection
            && m.SourceCode == (int)GamepadStickDirection.RightStickRight
            && m.TargetKind == MappingInputKind.MouseButton
            && m.TargetCode == MouseMotionSentinel
            && IsDirectionAllowed(m));
        if (!wantsStickToMouse) return;

        // Map -32768..32767 → -1..+1 with profile-controlled deadzone + response curve.
        double dz = Math.Clamp(profile.StickDeadzone, 0.0, 0.5);
        double ad = Math.Clamp(profile.StickAntiDeadzone, 0.0, 0.5);
        double curve = Math.Max(0.5, profile.StickMouseExponent);
        double nx = state.Gamepad.RightThumbX / 32768.0;
        double ny = state.Gamepad.RightThumbY / 32768.0;
        double mag = Math.Sqrt(nx * nx + ny * ny);
        if (mag < dz) return;
        // Normalise to 0..1 after deadzone, then re-add anti-deadzone, then curve.
        double t = (mag - dz) / (1 - dz);
        t = ad + (1.0 - ad) * Math.Pow(t, curve);
        double scale = profile.StickToMouseSensitivity * t / Math.Max(0.0001, mag);
        int dx = (int)Math.Round(nx * scale);
        int dy = (int)Math.Round((profile.InvertMouseY ? +1 : -1) * ny * scale);
        if (dx == 0 && dy == 0) return;
        MouseMoveRelative(dx, dy);
    }

    // ============================================================================ NATIVE SENDERS ====

    // Win32 SendInput INPUT.type values + KEYBDINPUT flags. The project's NativeTypes only
    // exposes MOUSE constants on MInputType — we use the full INPUT struct (which has the union
    // for both ki/mi) and hard-code these numeric values.
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static void SendKey(Keys k, bool pressed)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki = new KEYBDINPUT
        {
            wVk = (ushort)k,
            dwFlags = pressed ? 0u : KEYEVENTF_KEYUP,
        };
        NativeAPIMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void SendMouse(MouseButtons btn, bool pressed)
    {
        InputEventFlags flag = btn switch
        {
            MouseButtons.Left   => pressed ? InputEventFlags.MOUSEEVENTF_LEFTDOWN   : InputEventFlags.MOUSEEVENTF_LEFTUP,
            MouseButtons.Right  => pressed ? InputEventFlags.MOUSEEVENTF_RIGHTDOWN  : InputEventFlags.MOUSEEVENTF_RIGHTUP,
            MouseButtons.Middle => pressed ? InputEventFlags.MOUSEEVENTF_MIDDLEDOWN : InputEventFlags.MOUSEEVENTF_MIDDLEUP,
            _ => default
        };
        if (flag == default) return;
        var inputs = new MINPUT[1];
        inputs[0].type = (uint)MInputType.INPUT_MOUSE;
        inputs[0].U.mi = new MOUSEINPUT { dwFlags = (uint)flag };
        NativeAPIMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void MouseMoveRelative(int dx, int dy)
    {
        var inputs = new MINPUT[1];
        inputs[0].type = (uint)MInputType.INPUT_MOUSE;
        inputs[0].U.mi = new MOUSEINPUT
        {
            dx = dx, dy = dy,
            dwFlags = (uint)(InputEventFlags.MOUSEEVENTF_MOVE | InputEventFlags.MOUSEEVENTF_MOVE_NOCOALESCE),
        };
        NativeAPIMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

}
