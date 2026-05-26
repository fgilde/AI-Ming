using System.Windows;

namespace PowerAim.Visuality;

/// <summary>
///     Non-modal pop-out window for the embedded documentation viewer (<see cref="UILibrary.HelpPanel"/>).
///     Lets the user keep the docs visible while doing other things in PowerAim — same pattern as
///     <see cref="GamepadTesterWindow"/>. Static <see cref="Current"/> handle prevents stacking
///     duplicate windows from repeated pop-out clicks.
/// </summary>
public partial class HelpWindow : Window
{
    public static HelpWindow? Current { get; private set; }

    public HelpWindow()
    {
        InitializeComponent();
        // The panel hides its own Back + PopOut buttons when inside a stand-alone window so the
        // toolbar matches what makes sense in this context (window has its own close + restore).
        Panel.IsHostedInWindow = true;
        Current = this;
        Closed += (_, _) => { if (ReferenceEquals(Current, this)) Current = null; };
    }
}
