using System.Windows.Forms;
using PowerAim.Config;
using PowerAim.InputLogic;

namespace PowerAim.AILogic.Actions;

/// <summary>
///     Legacy fixed-step anti-recoil. Active only when:
///     <list type="bullet">
///       <item><see cref="ToggleState.AntiRecoil"/> master switch is on,</item>
///       <item>An active <see cref="AntiRecoilProfile"/> exists,</item>
///       <item>That profile's <see cref="AntiRecoilProfile.Mode"/> is
///             <see cref="AntiRecoilMode.Legacy"/>.</item>
///     </list>
///     Reads <see cref="AntiRecoilProfile.HoldTime"/> / <see cref="AntiRecoilProfile.FireRate"/> /
///     <see cref="AntiRecoilProfile.XRecoil"/> / <see cref="AntiRecoilProfile.YRecoil"/> from the
///     active profile (not from the global <see cref="AntiRecoilSettings"/> any more — those legacy
///     fields are only consulted once during config migration).
///     <para>
///     All mouse compensation is dispatched through <see cref="InputSender.Move"/> so when the user
///     toggles <c>UseControllerForAim</c> the recoil compensation rides the virtual right-stick
///     instead of the synthetic mouse.
///     </para>
/// </summary>
public class AntiRecoilAction : BaseAction
{
    private DateTime _keyDownAt = DateTime.MinValue;
    private DateTime _lastApplyAt = DateTime.MinValue;
    private bool _wasHolding = false;

    public override bool Active
    {
        get
        {
            if (!base.Active) return false;
            if (!AppConfig.Current.ToggleState.AntiRecoil) return false;
            var profile = AppConfig.Current.AntiRecoilSettings?.ActiveProfile;
            return profile != null
                && profile.Mode == AntiRecoilMode.Legacy;
        }
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
        if (!_wasHolding)
        {
            _keyDownAt = now;
            _lastApplyAt = now;
            _wasHolding = true;
            return Task.CompletedTask;
        }

        var profile = AppConfig.Current.AntiRecoilSettings!.ActiveProfile!;
        if ((now - _keyDownAt).TotalMilliseconds < profile.HoldTime) return Task.CompletedTask;
        if ((now - _lastApplyAt).TotalMilliseconds < profile.FireRate) return Task.CompletedTask;

        InputSender.Move(profile.XRecoil, profile.YRecoil);
        _lastApplyAt = now;
        return Task.CompletedTask;
    }

    public override Task OnPause()  { _wasHolding = false; return Task.CompletedTask; }
    public override Task OnResume() => Task.CompletedTask;
    public override void Dispose() { /* no resources to release */ }
}
