using System.Windows;

namespace PowerAim.Visuality;

/// <summary>
///     Non-modal pop-out for the <see cref="UILibrary.GamepadTesterPanel"/>. Lets the user keep
///     the live controller view visible while editing mapping profiles, tuning sensitivities, or
///     interacting with any other PowerAim page — without losing context. A static <see cref="Current"/>
///     handle prevents stacking duplicate windows from repeated button clicks.
/// </summary>
public partial class GamepadTesterWindow : Window
{
    public static GamepadTesterWindow? Current { get; private set; }

    public GamepadTesterWindow()
    {
        InitializeComponent();
        // Hide the inner panel's "Back" button — it makes no sense in a stand-alone window. The
        // panel was originally written for inline navigation; in pop-out mode the X button is the
        // only "close" affordance.
        Loaded += (_, _) =>
        {
            var back = Tester.FindName("BackButton") as System.Windows.Controls.Button;
            if (back != null) back.Visibility = Visibility.Collapsed;
            var pop = Tester.FindName("PopOutButton") as System.Windows.Controls.Button;
            if (pop != null) pop.Visibility = Visibility.Collapsed;
        };
        Current = this;
        Closed += (_, _) => { if (ReferenceEquals(Current, this)) Current = null; };
    }
}
