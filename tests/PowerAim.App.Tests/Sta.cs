using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace PowerAim.App.Tests;

/// <summary>
///     Runs a test body on the one STA thread that hosts the app's <see cref="System.Windows.Application"/>
///     (WPF elements need an STA thread, and the dialogs need the app resources — converters, styles — from
///     App.xaml). The app is only initialised, never started: no main window, no startup services.
/// </summary>
internal static class Sta
{
    private static readonly Lazy<Dispatcher> Host = new(() =>
    {
        Dispatcher? dispatcher = null;
        Exception? error = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                // The app class is PowerAim.App, which this test namespace shadows — reach it by name.
                var appType = typeof(global::PowerAim.Config.AppConfig).Assembly.GetType("PowerAim.App", throwOnError: true)!;
                var app = (System.Windows.Application)Activator.CreateInstance(appType)!;
                app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                appType.GetMethod("InitializeComponent")!.Invoke(app, null);
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex) { error = ex; }
            finally { ready.Set(); }
            if (error == null) Dispatcher.Run();
        }) { IsBackground = true, Name = "WPF test host" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        return dispatcher!;
    });

    public static void Run(Action body)
    {
        ExceptionDispatchInfo? failure = null;
        Host.Value.Invoke(() =>
        {
            try { body(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        failure?.Throw();
    }
}
