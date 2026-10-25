using System.Windows;
using System.Windows.Forms;
using Nextended.UI.Input;
using PowerAim.Config;
using PowerAim.UI.UILibrary;

namespace PowerAim.App.Tests;

public class AKeyChangerTests
{
    private static FakeHook Load(AKeyChanger changer)
    {
        // AppConfig.Current has no public setter; each test starts from a fresh default config.
        typeof(AppConfig).GetProperty(nameof(AppConfig.Current))!.SetValue(null, new AppConfig());
        var hook = new FakeHook();
        changer.BindingManager = new InputBindingManager(enableGamepad: false, () => hook);
        changer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        changer.Changer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        return hook;
    }

    private static void Tap(FakeHook hook, Keys key)
    {
        hook.Press(key);
        hook.Release(key);
    }

    [Fact]
    public void User_recording_is_persisted_under_the_prefixed_id() => Sta.Run(() =>
    {
        var changer = new AKeyChanger { KeyConfigName = "zoom in", KeyConfigPrefix = "USER_TOOL" };
        var hook = Load(changer);

        changer.RecordNewBinding();
        Tap(hook, Keys.F9);

        Assert.Equal("USER_TOOL_ZOOM_IN", changer.BindingId);
        Assert.Equal(Keys.F9, changer.KeyBind!.KeyboardKey);
        Assert.Equal(Keys.F9, AppConfig.Current.BindingSettings["USER_TOOL_ZOOM_IN"].KeyboardKey);
    });

    [Fact]
    public void Programmatic_set_is_not_persisted() => Sta.Run(() =>
    {
        var changer = new AKeyChanger { KeyConfigName = "x", KeyConfigPrefix = "CONFIG" };
        Load(changer);

        changer.KeyBind = Keys.F8;

        Assert.Null(AppConfig.Current.BindingSettings[changer.BindingId]);
    });

    [Fact]
    public void Stored_binding_is_loaded_when_none_is_given() => Sta.Run(() =>
    {
        var changer = new AKeyChanger { KeyConfigName = "a.cfg", KeyConfigPrefix = "CONFIG" };
        typeof(AppConfig).GetProperty(nameof(AppConfig.Current))!.SetValue(null, new AppConfig());
        AppConfig.Current.BindingSettings["CONFIG_A.CFG"] = Keys.F7;

        changer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        Assert.Equal(Keys.F7, changer.KeyBind!.KeyboardKey);
    });

    [Fact]
    public void Presses_respect_the_global_active_gate() => Sta.Run(() =>
    {
        var changer = new AKeyChanger { KeyConfigName = "gated", KeyConfigPrefix = "DYN" };
        var hook = Load(changer);
        AppConfig.Current.ToggleState.RequireGlobalActiveForKeybinds = true;
        AppConfig.Current.ToggleState.GlobalActive = false;
        changer.KeyBind = Keys.F6;
        var pressed = 0;
        changer.GlobalKeyPressed += (_, _) => pressed++;

        Tap(hook, Keys.F6);
        Assert.Equal(0, pressed);

        changer.IgnoreGlobalActiveGate = true;
        Tap(hook, Keys.F6);
        Assert.Equal(1, pressed);
    });
}
