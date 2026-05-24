using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerAim.Class.Native;
using PowerAim.Config;
using InputLogic;

namespace PowerAim.AILogic.Actions;

/// <summary>
///     Replays the active <see cref="RecoilPattern"/> while the user is firing. Tracks the elapsed
///     time since fire-down and dispatches incremental compensation strokes whose magnitudes are
///     read off the recorded pattern's per-frame drift.
///     <para>
///     Gated independently of the legacy and image-based anti-recoil actions. When this action is
///     active (i.e. <see cref="AntiRecoilSettings.UsePatternRecoil"/> is on and a pattern is
///     selected), the other two should be considered redundant — UI is responsible for that
///     visual hint; we don't disable them here on purpose, because power users may want to layer.
///     </para>
/// </summary>
public class RecoilPatternPlaybackAction : BaseAction
{
    private DateTime _fireDownAt = DateTime.MinValue;
    private int _lastSampleIndex = -1;
    private bool _wasHolding = false;
    private double _lastAppliedX = 0;
    private double _lastAppliedY = 0;

    public override bool Active =>
        base.Active &&
        AppConfig.Current.ToggleState.AntiRecoil &&
        AppConfig.Current.AntiRecoilSettings.UsePatternRecoil &&
        !string.IsNullOrEmpty(AppConfig.Current.AntiRecoilSettings.ActivePatternName);

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active) { Reset(); return Task.CompletedTask; }

        var keybind = AppConfig.Current.BindingSettings.AntiRecoilKeybind;
        bool isHolding = keybind != null && keybind.IsValid
            ? InputBindingManager.IsHoldingBindingFor(keybind, TimeSpan.Zero)
            : InputBindingManager.IsHoldingBinding(MouseButtons.Left);

        if (!isHolding) { Reset(); return Task.CompletedTask; }

        var pattern = ResolveActivePattern();
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

        double strength = Math.Clamp(AppConfig.Current.AntiRecoilSettings.PatternStrength, 0.0, 3.0);
        var sample = pattern.Samples[idx];
        // We accumulate the cumulative delta, then emit only the delta-since-last-applied to keep
        // mouse strokes incremental.
        double targetX = sample.DeltaX * strength;
        double targetY = sample.DeltaY * strength;
        int dx = (int)Math.Round(targetX - _lastAppliedX);
        int dy = (int)Math.Round(targetY - _lastAppliedY);
        if (dx != 0 || dy != 0)
        {
            MouseMove(dx, dy);
            _lastAppliedX += dx;
            _lastAppliedY += dy;
        }

        return Task.CompletedTask;
    }

    private static RecoilPattern? ResolveActivePattern()
    {
        var settings = AppConfig.Current?.AntiRecoilSettings;
        if (settings == null) return null;
        var name = settings.ActivePatternName;
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var p in settings.Patterns)
            if (p.Name == name) return p;
        return null;
    }

    private static void MouseMove(int dx, int dy)
    {
        var inputs = new MINPUT[1];
        inputs[0].type = (uint)MInputType.INPUT_MOUSE;
        inputs[0].U.mi = new MOUSEINPUT
        {
            dx = dx,
            dy = dy,
            dwFlags = (uint)(InputEventFlags.MOUSEEVENTF_MOVE | InputEventFlags.MOUSEEVENTF_MOVE_NOCOALESCE)
        };
        NativeAPIMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
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
