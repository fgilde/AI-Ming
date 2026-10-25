using System.Windows.Forms;
using Gma.System.MouseKeyHook;

namespace PowerAim.App.Tests;

/// <summary>In-memory stand-in for the global keyboard/mouse hook.</summary>
internal sealed class FakeHook : IKeyboardMouseEvents
{
    public void Press(Keys key) => KeyDown?.Invoke(this, new KeyEventArgs(key));
    public void Release(Keys key) => KeyUp?.Invoke(this, new KeyEventArgs(key));
    public void Press(MouseButtons button) => MouseDown?.Invoke(this, new MouseEventArgs(button, 1, 0, 0, 0));
    public void Release(MouseButtons button) => MouseUp?.Invoke(this, new MouseEventArgs(button, 1, 0, 0, 0));

    public event KeyEventHandler? KeyDown;
    public event KeyEventHandler? KeyUp;
    public event MouseEventHandler? MouseDown;
    public event MouseEventHandler? MouseUp;
    public event MouseEventHandler? MouseMove;

    // Unused by InputBindingManager.
    public event KeyPressEventHandler? KeyPress { add { } remove { } }
    public event EventHandler<KeyDownTxtEventArgs>? KeyDownTxt { add { } remove { } }
    public event EventHandler<MouseEventExtArgs>? MouseMoveExt { add { } remove { } }
    public event MouseEventHandler? MouseClick { add { } remove { } }
    public event EventHandler<MouseEventExtArgs>? MouseDownExt { add { } remove { } }
    public event EventHandler<MouseEventExtArgs>? MouseUpExt { add { } remove { } }
    public event MouseEventHandler? MouseWheel { add { } remove { } }
    public event MouseEventHandler? MouseHWheel { add { } remove { } }
    public event EventHandler<MouseEventExtArgs>? MouseWheelExt { add { } remove { } }
    public event EventHandler<MouseEventExtArgs>? MouseHWheelExt { add { } remove { } }
    public event MouseEventHandler? MouseDoubleClick { add { } remove { } }
    public event MouseEventHandler? MouseDragStarted { add { } remove { } }
    public event EventHandler<MouseEventExtArgs>? MouseDragStartedExt { add { } remove { } }
    public event MouseEventHandler? MouseDragFinished { add { } remove { } }
    public event EventHandler<MouseEventExtArgs>? MouseDragFinishedExt { add { } remove { } }

    public void Dispose() { }
}
