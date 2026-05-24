using System.Windows.Forms;
using PowerAim.Config;
using PowerAim.InputLogic;
using InputLogic;

namespace PowerAim.AILogic.Actions;

/// <summary>
///     Legacy "manual" anti-recoil: while the configured trigger key (default Left Mouse
///     Button) is held, applies a fixed per-tick (<see cref="AntiRecoilSettings.XRecoil"/>,
///     <see cref="AntiRecoilSettings.YRecoil"/>) mouse-delta on a timer.
///     <para>
///     <see cref="AntiRecoilSettings.HoldTime"/> is the delay (ms) before the first correction
///     fires after the key first goes down — gives the gun a moment of natural recoil before
///     the compensator kicks in. <see cref="AntiRecoilSettings.FireRate"/> is the cooldown (ms)
///     between corrections.
///     </para>
///     <para>
///     Disabled (no-op) when:
///     <list type="bullet">
///       <item><see cref="ToggleState.GlobalActive"/> is off</item>
///       <item><see cref="ToggleState.AntiRecoil"/> is off</item>
///       <item>The user has enabled the experimental <see cref="ImageBasedAntiRecoilAction"/>
///             via <see cref="AntiRecoilSettings.UseImageBasedAntiRecoil"/></item>
///     </list>
///     Previously this logic lived in a parallel <c>DispatcherTimer</c> on the UI thread
///     (<c>AntiRecoilManager</c> + <c>MouseManager.DoAntiRecoil</c>) which ignored every gate
///     above — that's been deleted; the action-class lifecycle is now the single source of truth.
///     </para>
/// </summary>
public class AntiRecoilAction : BaseAction
{
    private DateTime _keyDownAt = DateTime.MinValue;
    private DateTime _lastApplyAt = DateTime.MinValue;
    private bool _wasHolding = false;

    public override bool Active =>
        base.Active &&
        AppConfig.Current.ToggleState.AntiRecoil &&
        !AppConfig.Current.AntiRecoilSettings.UseImageBasedAntiRecoil &&
        // Pattern playback owns the recoil entirely when armed — overlaying the fixed-step
        // legacy compensation on top of recorded strokes just double-counts and feels wrong.
        !IsPatternPlaybackArmed();

    private static bool IsPatternPlaybackArmed()
    {
        var s = AppConfig.Current?.AntiRecoilSettings;
        return s != null && s.UsePatternRecoil && !string.IsNullOrEmpty(s.ActivePatternName);
    }

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active)
        {
            _wasHolding = false;
            return Task.CompletedTask;
        }

        var keybind = AppConfig.Current.BindingSettings.AntiRecoilKeybind;
        bool isHolding = keybind != null && keybind.IsValid
            ? InputBindingManager.IsHoldingBindingFor(keybind, TimeSpan.Zero)
            : InputBindingManager.IsHoldingBinding(MouseButtons.Left);

        if (!isHolding)
        {
            _wasHolding = false;
            return Task.CompletedTask;
        }

        var now = DateTime.UtcNow;

        // First tick after the key goes down — start the hold-time clock.
        if (!_wasHolding)
        {
            _keyDownAt = now;
            _lastApplyAt = now;
            _wasHolding = true;
            return Task.CompletedTask;
        }

        var settings = AppConfig.Current.AntiRecoilSettings;
        if ((now - _keyDownAt).TotalMilliseconds < settings.HoldTime) return Task.CompletedTask;
        if ((now - _lastApplyAt).TotalMilliseconds < settings.FireRate) return Task.CompletedTask;

        MouseManager.Move(settings.XRecoil, settings.YRecoil);
        _lastApplyAt = now;
        return Task.CompletedTask;
    }

    public override Task OnPause()  { _wasHolding = false; return Task.CompletedTask; }
    public override Task OnResume() => Task.CompletedTask;
    public override void Dispose() { /* no resources to release */ }
}
