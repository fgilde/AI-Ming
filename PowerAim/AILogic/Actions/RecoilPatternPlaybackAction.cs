using System.Windows.Forms;
using PowerAim.Config;
using PowerAim.InputLogic;
using InputLogic;

namespace PowerAim.AILogic.Actions;

/// <summary>
///     Replays the active <see cref="RecoilPattern"/> while the user is firing. Tracks the elapsed
///     time since fire-down and dispatches incremental compensation strokes whose magnitudes are
///     read off the recorded pattern's per-frame drift.
///     <para>
///     Now driven by the active <see cref="AntiRecoilProfile"/>: this action only fires when the
///     active profile's <see cref="AntiRecoilProfile.Mode"/> is
///     <see cref="AntiRecoilMode.PatternPlayback"/>. The pattern name and per-profile strength come
///     from the profile, not from the global settings.
///     </para>
///     <para>
///     All compensation goes through <see cref="InputSender.Move"/> so controller-aim users get
///     anti-recoil on the virtual right-stick too. Loses the
///     <c>MOUSEEVENTF_MOVE_NOCOALESCE</c> finesse the previous direct-SendInput path used — the
///     trade-off is worth it for controller support; if NOCOALESCE turns out to matter in practice
///     it can be re-added as an opt-in flag on <see cref="InputSender.Move"/>.
///     </para>
/// </summary>
public class RecoilPatternPlaybackAction : BaseAction
{
    private DateTime _fireDownAt = DateTime.MinValue;
    private int _lastSampleIndex = -1;
    private bool _wasHolding = false;
    private double _lastAppliedX = 0;
    private double _lastAppliedY = 0;

    public override bool Active
    {
        get
        {
            if (!base.Active) return false;
            if (!AppConfig.Current.ToggleState.AntiRecoil) return false;
            var profile = AppConfig.Current.AntiRecoilSettings?.ActiveProfile;
            return profile != null
                && profile.Mode == AntiRecoilMode.PatternPlayback
                && !string.IsNullOrEmpty(profile.PatternName);
        }
    }

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active) { Reset(); return Task.CompletedTask; }

        var keybind = AppConfig.Current.BindingSettings.AntiRecoilKeybind;
        bool isHolding = keybind != null && keybind.IsValid
            ? InputBindingManager.IsHoldingBindingFor(keybind, TimeSpan.Zero)
            : InputBindingManager.IsHoldingBinding(MouseButtons.Left);

        if (!isHolding) { Reset(); return Task.CompletedTask; }

        var profile = AppConfig.Current.AntiRecoilSettings!.ActiveProfile!;
        var pattern = ResolvePattern(profile.PatternName);
        if (pattern == null || pattern.Samples.Count == 0) return Task.CompletedTask;

        var now = DateTime.UtcNow;
        if (!_wasHolding)
        {
            _fireDownAt = now;
            _lastSampleIndex = -1;
            _lastAppliedX = 0;
            _lastAppliedY = 0;
            _wasHolding = true;
            return Task.CompletedTask;
        }

        int elapsedMs = (int)(now - _fireDownAt).TotalMilliseconds;

        // Find the latest sample whose TimeMs <= elapsedMs. Samples are sorted on record.
        int idx = _lastSampleIndex;
        while (idx + 1 < pattern.Samples.Count && pattern.Samples[idx + 1].TimeMs <= elapsedMs)
            idx++;

        if (idx == _lastSampleIndex) return Task.CompletedTask;
        _lastSampleIndex = idx;
        if (idx < 0) return Task.CompletedTask;

        double strength = Math.Clamp(profile.PatternStrength, 0.0, 3.0);
        var sample = pattern.Samples[idx];
        // Accumulate the cumulative delta, then emit only the delta-since-last-applied to keep
        // mouse strokes incremental.
        double targetX = sample.DeltaX * strength;
        double targetY = sample.DeltaY * strength;
        int dx = (int)Math.Round(targetX - _lastAppliedX);
        int dy = (int)Math.Round(targetY - _lastAppliedY);
        if (dx != 0 || dy != 0)
        {
            InputSender.Move(dx, dy);
            _lastAppliedX += dx;
            _lastAppliedY += dy;
        }

        return Task.CompletedTask;
    }

    private static RecoilPattern? ResolvePattern(string name)
    {
        var settings = AppConfig.Current?.AntiRecoilSettings;
        if (settings == null || string.IsNullOrEmpty(name)) return null;
        foreach (var p in settings.Patterns)
            if (p.Name == name) return p;
        return null;
    }

    private void Reset()
    {
        _wasHolding = false;
        _lastSampleIndex = -1;
        _lastAppliedX = 0;
        _lastAppliedY = 0;
    }

    public override Task OnPause()  { Reset(); return Task.CompletedTask; }
    public override Task OnResume() => Task.CompletedTask;
    public override void Dispose() { /* no resources */ }
}
