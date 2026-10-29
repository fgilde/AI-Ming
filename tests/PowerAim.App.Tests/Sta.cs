using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace PowerAim.App.Tests;

/// <summary>
///     Runs a test body on one shared STA thread that hosts a plain WPF <see cref="Application"/> carrying the
///     app's resources (converters, styles, brushes from App.xaml) — WPF elements need an STA thread and the
///     dialogs need those resources. Deliberately NOT the app class itself: starting it would open the main
///     window and boot the gamepad, mapping and OCR services inside the test run.
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
                var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources = LoadAppResources();
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

    /// <summary>The &lt;Application.Resources&gt; dictionary of App.xaml, parsed from the source file.</summary>
    private static ResourceDictionary LoadAppResources()
    {
        string? path = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null && path == null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "PowerAim.App", "App.xaml");
            if (File.Exists(candidate)) path = candidate;
        }
        var xaml = File.ReadAllText(path ?? throw new FileNotFoundException("App.xaml not found above the test output."));
        int start = xaml.IndexOf("<Application.Resources>", StringComparison.Ordinal) + "<Application.Resources>".Length;
        int end = xaml.IndexOf("</Application.Resources>", StringComparison.Ordinal);
        var resources = xaml[start..end].Trim();
        int root = resources.IndexOf("<ResourceDictionary>", StringComparison.Ordinal);
        resources = resources[..root] + "<ResourceDictionary " +
                    "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                    "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
                    "xmlns:converter=\"clr-namespace:PowerAim.UI.Converters;assembly=" +
                    typeof(global::PowerAim.Config.AppConfig).Assembly.GetName().Name + "\">" +
                    resources[(root + "<ResourceDictionary>".Length)..];
        var context = new ParserContext
        {
            // Relative sources (Theme.xaml) resolve against the app assembly like in App.xaml.
            BaseUri = new Uri($"pack://application:,,,/{typeof(global::PowerAim.Config.AppConfig).Assembly.GetName().Name};component/App.xaml"),
        };
        return (ResourceDictionary)XamlReader.Parse(resources, context);
    }

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
