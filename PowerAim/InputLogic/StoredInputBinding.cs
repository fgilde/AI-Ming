using System.Diagnostics;
using PowerAim.InputLogic.Contracts;
using System.Windows.Forms;
using PowerAim.InputLogic;

namespace PowerAim.InputLogic;

public class StoredInputBinding
{
    public bool Equals(StoredInputBinding other)
    {
        if (other is null) return false;
        if (!IsValid && !other.IsValid)
            return true;
        if (IsCombo || other.IsCombo)
        {
            // Combos compare as an order-independent SET of components; a combo never equals a single.
            if (!IsCombo || !other.IsCombo || Components!.Count != other.Components!.Count) return false;
            var remaining = new List<StoredInputBinding>(other.Components!);
            foreach (var c in Components!)
            {
                int idx = remaining.FindIndex(o => c.Equals(o));
                if (idx < 0) return false;
                remaining.RemoveAt(idx);
            }
            return true;
        }
        return Key == other.Key &&
               (Is<GamepadEventArgs>() && other.Is<GamepadEventArgs>() || Is<MouseEventArgs>() && other.Is<MouseEventArgs>() || Is<KeyEventArgs>() && other.Is<KeyEventArgs>());
    }

    public override bool Equals(object? obj)
    {
        return obj is StoredInputBinding other && Equals(other);
    }

    public override int GetHashCode()
    {
        // Combos must hash order-independently so {Ctrl,X} and {X,Ctrl} collide (Equals is a set
        // compare). XOR of component hashes is order-independent; components are distinct in practice.
        if (IsCombo)
        {
            int h = 19;
            foreach (var c in Components!) h ^= c.GetHashCode();
            return h;
        }
        return HashCode.Combine(Key, MouseEventArgs, KeyEventArgs, GamepadEventArgs);
    }

    public string Key { get; set; }
    public MouseEventArgs? MouseEventArgs { get; set; }
    public KeyEventArgs? KeyEventArgs { get; set; }
    public GamepadEventArgs? GamepadEventArgs { get; set; }
    public double MinTime { get; set; } = 0;

    /// <summary>
    ///     Component inputs of a CHORD/COMBO binding (e.g. Ctrl+Shift+X, X+B, Ctrl+LeftMouse).
    ///     <c>null</c> = a plain single binding (the <see cref="Key"/>/<see cref="KeyEventArgs"/>/…
    ///     fields are used). When set, every child must be held simultaneously to match, and they are
    ///     sent as a chord. Children are always plain singles (never nested combos).
    /// </summary>
    public List<StoredInputBinding>? Components { get; set; }

    /// <summary>True when this binding is a multi-input chord rather than a single input.</summary>
    public bool IsCombo => Components is { Count: > 0 };

    public StoredInputBinding SetMinTime(double value)
    {
        MinTime = value;
        return this;
    }

    /// <summary>
    ///     Build a chord from its parts. Invalid parts are dropped; an empty result is
    ///     <see cref="Empty"/>, and a single surviving part collapses back to that plain single — so
    ///     there is never a 1-element combo (keeps <see cref="Equals"/>/<see cref="GetHashCode"/>
    ///     unambiguous). The parent carries a joined <see cref="Key"/> for display/equality and leaves
    ///     its own device-event fields null.
    /// </summary>
    public static StoredInputBinding Combo(IEnumerable<StoredInputBinding> parts)
    {
        var valid = parts.Where(p => p is { IsValid: true }).ToList();
        if (valid.Count == 0) return Empty;
        if (valid.Count == 1) return valid[0];
        return new StoredInputBinding
        {
            Components = valid,
            Key = string.Join("+", valid.Select(p => p.Key)),
        };
    }

    /// <summary>The component singles of a combo, or this single itself — always plain singles.</summary>
    public IEnumerable<StoredInputBinding> Flatten() => IsCombo ? Components! : new[] { this };

    public bool Is<T>() where T : EventArgs => typeof(T) switch
    {
        not null when typeof(T) == typeof(MouseEventArgs) => MouseEventArgs != null,
        not null when typeof(T) == typeof(KeyEventArgs) => KeyEventArgs != null,
        not null when typeof(T) == typeof(GamepadEventArgs) => GamepadEventArgs != null,
        _ => false
    };

    public bool Is(GamepadButton button) => Is<GamepadEventArgs>() && GamepadEventArgs.GamepadButton == button;
    public bool Is(GamepadSlider slider) => Is<GamepadEventArgs>() && GamepadEventArgs.GamepadSlider == slider;
    public bool Is(GamepadAxis axis) => Is<GamepadEventArgs>() && GamepadEventArgs.GamepadAxis == axis;
    public bool Is(MouseButtons button) => Is<MouseEventArgs>() && MouseEventArgs.Button == button;
    public bool Is(Keys key) => Is<KeyEventArgs>() && KeyEventArgs.KeyCode == key;


    public bool Matches(GamepadEventArgs data) => Is<GamepadEventArgs>() && (GamepadEventArgs.Code == data.Code);
    public bool Matches(MouseEventArgs data) => Is<MouseEventArgs>() && MouseEventArgs.Button == data.Button;
    public bool Matches(KeyEventArgs data) => Is<KeyEventArgs>() && KeyEventArgs.KeyCode.ToString() == data.KeyCode.ToString();

    public bool IsValid => IsCombo
        ? Components!.All(c => c is { IsValid: true })
        : !string.IsNullOrWhiteSpace(Key) && Key.ToLower() != "none" && (MouseEventArgs != null || KeyEventArgs != null || GamepadEventArgs != null);
    public static StoredInputBinding Empty => new();
    public string DeviceName => IsCombo ? "Combo" : MouseEventArgs != null ? "Mouse" : KeyEventArgs != null ? "Keyboard" : GamepadEventArgs != null ? "Gamepad" : "Unknown";


    public StoredInputBinding()
    { }

    public StoredInputBinding(KeyEventArgs data)
    {
        Key = data.KeyCode.ToString();
        KeyEventArgs = data;
    }

    public StoredInputBinding(MouseEventArgs data)
    {
        Key = data.Button.ToString();
        MouseEventArgs = data;
    }

    public StoredInputBinding(GamepadEventArgs data)
    {
        Key = data.Code;
        GamepadEventArgs = data;
    }

    public StoredInputBinding(GamepadSlider slider) : this(new GamepadEventArgs(slider))
    { }

    public StoredInputBinding(MouseButtons mouseButtons) : this(new MouseEventArgs(mouseButtons, 0, 0, 0, 0))
    { }

    public StoredInputBinding(Keys keys) : this(new KeyEventArgs(keys))
    { }

    public StoredInputBinding(GamepadButton gamepadButton) : this(new GamepadEventArgs(gamepadButton))
    { }

    public StoredInputBinding(GamepadAxis gamepadAxis) : this(new GamepadEventArgs(gamepadAxis))
    { }

    public static implicit operator StoredInputBinding(Keys a) => new(a);
    public static implicit operator StoredInputBinding(MouseButtons a) => new(a);
    public static implicit operator StoredInputBinding(GamepadButton a) => new(a);
    public static implicit operator StoredInputBinding(GamepadSlider a) => new(a);
    public static implicit operator StoredInputBinding(GamepadAxis a) => new(a);

    public bool IsHoldingFor(TimeSpan? timeSpan = null) => InputBindingManager.IsHoldingBindingFor(this, timeSpan ?? TimeSpan.FromSeconds(MinTime));
    public bool IsHolding() => InputBindingManager.IsHoldingBindingFor(this, null);

    public async Task<bool> WaitHoldingFor()
    {
        var cancel = new CancellationTokenSource();
        Action<StoredInputBinding>? onReleased = s =>
        {
            if (s?.Equals(this) == true)
            {
                cancel.Cancel();
            }
        };
        try
        {
            if (MinTime <= 0)
                return IsHolding();
            if(InputBindingManager.Instance != null)
                InputBindingManager.Instance.OnKeyReleased += onReleased;

            var fromSeconds = TimeSpan.FromSeconds(MinTime);
            await Task.Delay(fromSeconds, cancel.Token);
            return !cancel.IsCancellationRequested && IsHolding();
        }
        catch
        {
            return false;
        }
        finally
        {
            if (InputBindingManager.Instance != null)
                InputBindingManager.Instance.OnKeyReleased -= onReleased;
        }
    }

    public void ExecuteWhenHold(Action action)
    {
        WaitHoldingFor().ContinueWith(t =>
        {
            if(t.Result)
                System.Windows.Application.Current.Dispatcher.Invoke(action);
        });
    }

}