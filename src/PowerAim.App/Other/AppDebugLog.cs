using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using PowerAim.Config;

namespace PowerAim.Other;

/// <summary>
///     Application-wide structured debug log + live health snapshot.
///     Use to verify wiring: toggles, model, AI loop, providers, null/error states.
/// </summary>
public static class AppDebugLog
{
    public enum Level
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4
    }

    public readonly record struct Entry(
        DateTime Utc,
        Level Level,
        string Category,
        string Message,
        string? Detail);

    /// <summary>One row in the System Health / wiring table.</summary>
    public sealed class HealthItem
    {
        public string Area { get; init; } = "";
        public string Name { get; init; } = "";
        public string Value { get; init; } = "";
        public string Status { get; init; } = "OK"; // OK | WARN | ERROR | NULL | OFF
        public string Detail { get; init; } = "";
    }

    private const int MaxMemoryEntries = 8000;
    private static readonly ConcurrentQueue<Entry> _entries = new();
    private static readonly object _fileGate = new();
    private static readonly object _hookGate = new();
    private static StreamWriter? _writer;
    private static string? _currentFile;
    private static bool _hooksInstalled;
    private static int _seq;

    public static volatile bool Enabled = false;
    public static volatile bool MirrorToDebug = true;
    public static volatile Level MinLevel = Level.Debug;

    public static event Action<Entry>? EntryAdded;

    public static string LogDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PowerAim", "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string? CurrentLogFile => _currentFile;

    public static IReadOnlyList<Entry> Snapshot() => _entries.ToArray();

    public static void ClearMemory()
    {
        while (_entries.TryDequeue(out _)) { }
    }

    public static void Enable(bool on, string? reason = null)
    {
        Enabled = on;
        if (on)
        {
            EnsureWriter();
            Info("Debug", "Debug logging ENABLED" + (string.IsNullOrEmpty(reason) ? "" : ": " + reason));
            InstallHooks();
            Info("Health", "Initial system snapshot");
            foreach (var line in FormatHealthLines(CaptureHealth()))
                Write(Level.Info, "Health", line, null);
        }
        else
        {
            Info("Debug", "Debug logging DISABLED");
            Flush();
        }
    }

    public static void Trace(string category, string message, string? detail = null)
        => Write(Level.Trace, category, message, detail);
    public static void Debug(string category, string message, string? detail = null)
        => Write(Level.Debug, category, message, detail);
    public static void Info(string category, string message, string? detail = null)
        => Write(Level.Info, category, message, detail);
    public static void Warn(string category, string message, string? detail = null)
        => Write(Level.Warn, category, message, detail);
    public static void Error(string category, string message, string? detail = null)
        => Write(Level.Error, category, message, detail, force: true);

    public static void Exception(string category, Exception ex, string? context = null)
    {
        if (ex is OperationCanceledException) return;
        var msg = string.IsNullOrEmpty(context)
            ? ex.GetType().Name + ": " + ex.Message
            : context + " — " + ex.GetType().Name + ": " + ex.Message;
        Write(Level.Error, category, msg, ex.ToString(), force: true);
    }

    public static void Feature(string name, bool enabled, string? source = null)
    {
        var src = string.IsNullOrEmpty(source) ? "" : " (" + source + ")";
        Write(Level.Info, "Feature", $"{name} → {(enabled ? "ON" : "OFF")}{src}", null);
    }

    public static void FeatureChange(string name, object? oldValue, object? newValue, string? source = null)
    {
        // Skip pure null→null noise from toggle-restore during model load
        if (oldValue is null && newValue is null) return;
        var src = string.IsNullOrEmpty(source) ? "" : " (" + source + ")";
        Write(Level.Info, "Feature", $"{name}: {Fmt(oldValue)} → {Fmt(newValue)}{src}", null);
    }

    public static void Write(Level level, string category, string message, string? detail = null, bool force = false)
    {
        if (!force && !Enabled) return;
        if (!force && level < MinLevel) return;

        var entry = new Entry(DateTime.UtcNow, level, category ?? "App", message ?? "", detail);
        _entries.Enqueue(entry);
        while (_entries.Count > MaxMemoryEntries && _entries.TryDequeue(out _)) { }

        if (MirrorToDebug)
        {
            System.Diagnostics.Debug.WriteLine(FormatLine(entry));
            if (detail != null) System.Diagnostics.Debug.WriteLine(detail);
        }

        try { EntryAdded?.Invoke(entry); } catch { }

        if (!force && !Enabled) return;
        AppendToFile(entry);
    }

    public static void Flush()
    {
        lock (_fileGate)
        {
            try { _writer?.Flush(); } catch { }
        }
    }

    // -------------------------------------------------------------------------
    // System health / wiring snapshot
    // -------------------------------------------------------------------------

    public static List<HealthItem> CaptureHealth()
    {
        var items = new List<HealthItem>();
        try
        {
            var cfg = AppConfig.Current;
            items.Add(Item("Runtime", "ProcessId", Environment.ProcessId.ToString(), "OK"));
            items.Add(Item("Runtime", "BaseDirectory", AppDomain.CurrentDomain.BaseDirectory ?? "", "OK"));
            items.Add(Item("Runtime", "OS", Environment.OSVersion.ToString(), "OK"));
            items.Add(Item("Runtime", "DebugLogging", Enabled ? "ON" : "OFF", Enabled ? "OK" : "OFF"));
            items.Add(Item("Runtime", "LogFile", _currentFile ?? "(none)", _currentFile != null ? "OK" : "WARN"));

            if (cfg == null)
            {
                items.Add(Item("Config", "AppConfig.Current", "null", "ERROR", "Config not loaded"));
                return items;
            }

            items.Add(Item("Config", "Path", cfg.Path ?? "", string.IsNullOrEmpty(cfg.Path) ? "WARN" : "OK"));
            {
                var lm = cfg.LastLoadedModel?.Trim();
                var empty = string.IsNullOrEmpty(lm) || lm is "N/A" or "n/a";
                items.Add(Item("Config", "LastLoadedModel", empty ? "(none)" : lm!, empty ? "WARN" : "OK"));
            }
            items.Add(Item("Config", "LastLoadedConfig", cfg.LastLoadedConfig ?? "", "OK"));

            // Toggles — every bool property
            var ts = cfg.ToggleState;
            if (ts == null)
                items.Add(Item("Toggle", "ToggleState", "null", "ERROR"));
            else
            {
                foreach (var prop in ts.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.PropertyType != typeof(bool) || !prop.CanRead) continue;
                    try
                    {
                        var v = (bool)prop.GetValue(ts)!;
                        items.Add(Item("Toggle", prop.Name, v ? "ON" : "OFF", v ? "OK" : "OFF"));
                    }
                    catch (Exception ex)
                    {
                        items.Add(Item("Toggle", prop.Name, "ERROR", "ERROR", ex.Message));
                    }
                }
            }

            // AI manager / model
            var mgr = FileManager.AIManager;
            if (mgr == null)
                items.Add(Item("AI", "AIManager", "null", "NULL", "No manager — load a model"));
            else
            {
                items.Add(Item("AI", "AIManager", "live", "OK"));
                items.Add(Item("AI", "IsModelLoaded", mgr.IsModelLoaded.ToString(), mgr.IsModelLoaded ? "OK" : "WARN"));
                items.Add(Item("AI", "IsRunning", mgr.IsRunning.ToString(), mgr.IsRunning ? "OK" : "WARN"));
                try
                {
                    var pl = mgr.PredictionLogic;
                    if (pl == null)
                        items.Add(Item("AI", "PredictionLogic", "null", "ERROR"));
                    else
                    {
                        items.Add(Item("AI", "PredictionLogic.IsLoaded", pl.IsLoaded.ToString(), pl.IsLoaded ? "OK" : "ERROR"));
                        var err = pl.LoadError;
                        items.Add(Item("AI", "LoadError", string.IsNullOrEmpty(err) ? "(none)" : err!,
                            string.IsNullOrEmpty(err) ? "OK" : "ERROR", err));
                        try
                        {
                            var ep = pl.GetType().GetProperty("ExecutionProvider")?.GetValue(pl)?.ToString() ?? "?";
                            items.Add(Item("AI", "ExecutionProvider", ep, ep is "Cpu" or "CPU" ? "WARN" : "OK",
                                ep is "Cpu" or "CPU" ? "Running on CPU — slower; check CUDA/DirectML" : ""));
                        }
                        catch { }
                        try
                        {
                            var size = pl.GetType().GetProperty("CurrentImageSize")?.GetValue(pl)?.ToString() ?? "?";
                            items.Add(Item("AI", "ImageSize", size, "OK"));
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    items.Add(Item("AI", "PredictionLogic", "error", "ERROR", ex.Message));
                }

                try
                {
                    var cap = mgr.ImageCapture;
                    items.Add(Item("AI", "ImageCapture", cap == null ? "null" : cap.GetType().Name,
                        cap == null ? "ERROR" : "OK"));
                }
                catch (Exception ex)
                {
                    items.Add(Item("AI", "ImageCapture", "error", "ERROR", ex.Message));
                }
            }

            items.Add(Item("AI", "CurrentlyLoadingModel", FileManager.CurrentlyLoadingModel.ToString(),
                FileManager.CurrentlyLoadingModel ? "WARN" : "OK"));

            // Model file on disk
            try
            {
                var modelName = cfg.LastLoadedModel?.Trim();
                if (string.IsNullOrEmpty(modelName) || modelName is "N/A" or "n/a" or "(none)")
                {
                    items.Add(Item("Model", "File", "(none loaded)", "WARN", "Load a model to verify path"));
                }
                else
                {
                    var path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "bin", "models", modelName));
                    var exists = File.Exists(path);
                    items.Add(Item("Model", "File", path, exists ? "OK" : "ERROR",
                        exists ? $"Size={new FileInfo(path).Length}" : "File missing"));
                }
            }
            catch (Exception ex)
            {
                items.Add(Item("Model", "File", "error", "ERROR", ex.Message));
            }

            // Capture source
            try
            {
                var cs = cfg.CaptureSource;
                items.Add(Item("Capture", "Source", cs?.ToString() ?? "null", cs == null ? "WARN" : "OK"));
            }
            catch (Exception ex)
            {
                items.Add(Item("Capture", "Source", "error", "ERROR", ex.Message));
            }

            // Dropdown / movement
            try
            {
                var dd = cfg.DropdownState;
                if (dd != null)
                {
                    items.Add(Item("Input", "MouseMovementMethod",
                        dd.MouseMovementMethod.ToString(), "OK"));
                    items.Add(Item("Input", "DetectionAreaType",
                        dd.DetectionAreaType.ToString(), "OK"));
                }
            }
            catch (Exception ex)
            {
                items.Add(Item("Input", "DropdownState", "error", "ERROR", ex.Message));
            }

            // Session stats
            try
            {
                var st = PowerAim.Class.SessionStats.Instance;
                items.Add(Item("Stats", "FramesProcessed", st.FramesProcessed.ToString(), "OK"));
                items.Add(Item("Stats", "LastInferenceMs", st.LastInferenceMs.ToString("0.0"), "OK"));
                items.Add(Item("Stats", "InstantFps", st.InstantFps.ToString("0.0"), "OK"));
                items.Add(Item("Stats", "LastDetectionCount", st.LastDetectionCount.ToString(), "OK"));
            }
            catch { }

            // AI settings preference
            try
            {
                var ai = cfg.AISettings;
                if (ai != null)
                {
                    var pref = ai.GetType().GetProperty("PreferredExecutionProvider")?.GetValue(ai)?.ToString() ?? "?";
                    items.Add(Item("AI", "PreferredProvider", pref, "OK"));
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            items.Add(Item("Health", "CaptureHealth", "failed", "ERROR", ex.ToString()));
        }

        return items;
    }

    public static string FormatHealthReport(IReadOnlyList<HealthItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========== SYSTEM HEALTH / WIRING ==========");
        sb.AppendLine($"UTC {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
        string? lastArea = null;
        foreach (var i in items)
        {
            if (i.Area != lastArea)
            {
                sb.AppendLine();
                sb.AppendLine($"── {i.Area} ──");
                lastArea = i.Area;
            }
            var mark = i.Status switch
            {
                "OK" => "[OK]   ",
                "OFF" => "[OFF]  ",
                "WARN" => "[WARN] ",
                "NULL" => "[NULL] ",
                "ERROR" => "[ERROR]",
                _ => "[?]    "
            };
            sb.AppendLine($"{mark} {i.Name,-28} = {i.Value}");
            if (!string.IsNullOrEmpty(i.Detail))
                sb.AppendLine($"         {i.Detail}");
        }
        var errors = items.Count(x => x.Status is "ERROR" or "NULL");
        var warns = items.Count(x => x.Status == "WARN");
        sb.AppendLine();
        sb.AppendLine($"Summary: {items.Count} checks — {errors} error(s), {warns} warning(s)");
        sb.AppendLine("===========================================");
        return sb.ToString();
    }

    private static IEnumerable<string> FormatHealthLines(IReadOnlyList<HealthItem> items)
    {
        foreach (var i in items)
            yield return $"{i.Status,-5} | {i.Area}/{i.Name} = {i.Value}" +
                         (string.IsNullOrEmpty(i.Detail) ? "" : " | " + i.Detail);
    }

    private static HealthItem Item(string area, string name, string value, string status, string detail = "")
        => new() { Area = area, Name = name, Value = value, Status = status, Detail = detail };

    // -------------------------------------------------------------------------
    // Hooks
    // -------------------------------------------------------------------------

    public static void InstallHooks()
    {
        lock (_hookGate)
        {
            if (_hooksInstalled) return;
            _hooksInstalled = true;
        }

        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is OperationCanceledException) return;
                if (e.ExceptionObject is Exception ex)
                    Exception("Unhandled", ex, e.IsTerminating ? "terminating" : "non-terminating");
                else
                    Error("Unhandled", e.ExceptionObject?.ToString() ?? "unknown");
                Flush();
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                if (e.Exception is OperationCanceledException ||
                    e.Exception?.InnerException is OperationCanceledException ||
                    e.Exception is AggregateException ae && ae.InnerExceptions.All(x => x is OperationCanceledException))
                {
                    e.SetObserved();
                    return;
                }
                Exception("Task", e.Exception!, "UnobservedTaskException");
                e.SetObserved();
                Flush();
            };

            HookConfig(AppConfig.Current);
            AppConfig.ConfigLoaded += (_, args) =>
            {
                Info("Config", "Config loaded", args?.Value?.Path);
                HookConfig(args?.Value ?? AppConfig.Current);
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[AppDebugLog] InstallHooks failed: " + ex.Message);
        }
    }

    private static void HookConfig(AppConfig? cfg)
    {
        if (cfg == null) return;
        try
        {
            cfg.PropertyChanged -= OnConfigPropertyChanged;
            cfg.PropertyChanged += OnConfigPropertyChanged;

            if (cfg.ToggleState != null)
            {
                cfg.ToggleState.PropertyChanged -= OnTogglePropertyChanged;
                cfg.ToggleState.PropertyChanged += OnTogglePropertyChanged;
            }
            if (cfg.AISettings != null)
            {
                cfg.AISettings.PropertyChanged -= OnAiSettingsChanged;
                cfg.AISettings.PropertyChanged += OnAiSettingsChanged;
            }
            if (cfg.SliderSettings != null)
            {
                cfg.SliderSettings.PropertyChanged -= OnSliderChanged;
                cfg.SliderSettings.PropertyChanged += OnSliderChanged;
            }
            if (cfg.DropdownState != null)
            {
                cfg.DropdownState.PropertyChanged -= OnDropdownChanged;
                cfg.DropdownState.PropertyChanged += OnDropdownChanged;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[AppDebugLog] HookConfig: " + ex.Message);
        }
    }

    private static void OnTogglePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Enabled) return;
        var name = e.PropertyName;
        if (string.IsNullOrEmpty(name)) return;
        try
        {
            var ts = AppConfig.Current?.ToggleState;
            if (ts == null) return;
            var prop = ts.GetType().GetProperty(name);
            var val = prop?.GetValue(ts);
            if (val is bool b) Feature(name, b, "ToggleState");
            else FeatureChange(name, null, val, "ToggleState");
        }
        catch { }
    }

    private static void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Enabled) return;
        var name = e.PropertyName;
        if (string.IsNullOrEmpty(name)) return;
        if (name is "ToggleState" or "AISettings" or "SliderSettings" or "DropdownState"
            or "AntiRecoilSettings" or "AimSettings" or "Triggers")
            return;
        try
        {
            var cfg = AppConfig.Current;
            var prop = cfg?.GetType().GetProperty(name);
            FeatureChange("Config." + name, null, prop?.GetValue(cfg), "AppConfig");
        }
        catch { }
    }

    private static void OnAiSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Enabled || string.IsNullOrEmpty(e.PropertyName)) return;
        try
        {
            var s = AppConfig.Current?.AISettings;
            var prop = s?.GetType().GetProperty(e.PropertyName!);
            FeatureChange("AI." + e.PropertyName, null, prop?.GetValue(s), "AISettings");
        }
        catch { }
    }

    private static void OnSliderChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Enabled || string.IsNullOrEmpty(e.PropertyName)) return;
        // ActualFovSize is recomputed often — only log when user-facing FOV fields change
        if (e.PropertyName is "ActualFovSize") return;
        try
        {
            var s = AppConfig.Current?.SliderSettings;
            var prop = s?.GetType().GetProperty(e.PropertyName!);
            FeatureChange("Slider." + e.PropertyName, null, prop?.GetValue(s), "SliderSettings");
        }
        catch { }
    }

    private static void OnDropdownChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Enabled || string.IsNullOrEmpty(e.PropertyName)) return;
        try
        {
            var s = AppConfig.Current?.DropdownState;
            var prop = s?.GetType().GetProperty(e.PropertyName!);
            FeatureChange("Dropdown." + e.PropertyName, null, prop?.GetValue(s), "DropdownState");
        }
        catch { }
    }

    private static void EnsureWriter()
    {
        lock (_fileGate)
        {
            if (_writer != null) return;
            try
            {
                var name = $"poweraim-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log";
                _currentFile = Path.Combine(LogDirectory, name);
                _writer = new StreamWriter(new FileStream(_currentFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
                {
                    AutoFlush = true
                };
                _writer.WriteLine($"# PowerAim debug log started {DateTime.Now:O}");
                _writer.WriteLine($"# PID={Environment.ProcessId} Machine={Environment.MachineName} OS={Environment.OSVersion}");
                _writer.WriteLine($"# BaseDir={AppDomain.CurrentDomain.BaseDirectory}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AppDebugLog] open file failed: " + ex.Message);
                _writer = null;
                _currentFile = null;
            }
        }
    }

    private static void AppendToFile(Entry entry)
    {
        lock (_fileGate)
        {
            try
            {
                if (_writer == null && Enabled) EnsureWriter();
                _writer?.WriteLine(FormatLine(entry));
                if (entry.Detail != null)
                {
                    foreach (var line in entry.Detail.Split('\n'))
                        _writer?.WriteLine("  | " + line.TrimEnd('\r'));
                }
            }
            catch { }
        }
    }

    private static string FormatLine(Entry e)
    {
        var n = System.Threading.Interlocked.Increment(ref _seq);
        return $"{e.Utc:yyyy-MM-dd HH:mm:ss.fff}Z [{e.Level.ToString().ToUpperInvariant(),-5}] [{e.Category}] {e.Message}  #{n}";
    }

    private static string Fmt(object? v) => v switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        float f => f.ToString("0.###"),
        double d => d.ToString("0.###"),
        _ => v.ToString() ?? ""
    };
}
