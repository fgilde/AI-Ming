using System.IO;
using System.Windows;
using PowerAim.AI;
using PowerAim.Config;
using PowerAim.Other;
using PowerAim.UI.Theme;

namespace PowerAim;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Initialize();
        // Before anything creates the virtual pad: the controller order decides which pad games read.
        ShutdownMode = ShutdownMode.OnExplicitShutdown; // the hint is the only window yet — closing it must not end the app
        PowerAim.UI.Visuality.GamepadOrderHintDialog.ShowIfWanted();
        MainWindow = null; // let the real main window claim it
        ShutdownMode = ShutdownMode.OnLastWindowClose;
        // Boot the OCR sampler — it ticks whenever OcrSettings.Enabled is true and
        // gracefully no-ops when no regions are configured or eng.traineddata is missing.
        OcrService.Instance.Start();
        // Best-effort load of the persisted AutoPlay learning model. Failures are silent —
        // the model just starts empty and the recorder fills it over time.
        try { AutoPlayLearningModel.Instance.Load(AppConfig.Current?.AutoPlayLearningSettings?.ModelPath); }
        catch { /* ignored */ }
        // Same for the recorded demonstrations behind the "learned" decision model.
        PowerAim.AI.AutoPlay.DemonstrationStore.Instance.Load(PowerAim.AI.AutoPlay.DemonstrationStore.DefaultPath);
        // Controller-mapping engine: KB↔Pad cross-mapping. Self-gates on profile presence,
        // so booting always is cheap. ViGEm bus driver is only required if the user enables
        // a profile with gamepad targets.
        // The focus watcher polls on a DispatcherTimer: create it here on the UI thread, before the AI
        // loop or the mapping engine first ask for the focused process from their own threads.
        _ = PowerAim.Class.WindowFocusWatcher.Instance;
        PowerAim.Input.Mapping.MappingEngine.Instance.Start();
        // Application debug log: feature flips, model switches, exceptions.
        AppDebugLog.InstallHooks();
        // Balanced / power-saver plans clock the hardware down between frames — a common cause of low FPS.
        if (PowerAim.Class.SystemChecks.ActivePowerPlan() is { } plan && PowerAim.Class.SystemChecks.IsThrottlingPowerPlan(plan))
        {
            AppDebugLog.Info("Startup", "Power plan throttles: " + plan);
            Dispatcher.BeginInvoke(new Action(() => new PowerAim.UI.Visuality.NoticeBar(Locale.PowerPlanWarning, 12000).Show()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        if (AppConfig.Current?.ToggleState?.EnableDebugLogging == true)
            AppDebugLog.Enable(true, "startup");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Keep what was recorded this session (or that it was cleared) for the learned decision model.
        if (PowerAim.AI.AutoPlay.DemonstrationStore.Instance.IsDirty)
        {
            try { PowerAim.AI.AutoPlay.DemonstrationStore.Instance.Save(PowerAim.AI.AutoPlay.DemonstrationStore.DefaultPath); }
            catch { /* best effort on shutdown */ }
        }
        try { PowerAim.Input.InputSender.Shutdown(); } catch { }
        try { OcrService.Instance.Stop(); } catch { }
        try { PowerAim.Input.Mapping.MappingEngine.Instance.Dispose(); } catch { }
        try { AppDebugLog.Flush(); } catch { }
        base.OnExit(e);
    }

    public string ReadEmbeddedResource(string resourceName)
    {
        var assembly = typeof(App).Assembly;

        string defaultNamespace = typeof(App).Namespace;
        string fullResourceName = $"{defaultNamespace}.{resourceName}";

        using Stream stream = assembly.GetManifestResourceStream(fullResourceName)
            ?? throw new Exception("Resource not found: " + fullResourceName);

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
