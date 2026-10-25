using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Vestris.ResourceLib;
using PowerAim.Core;

namespace PowerAim.Launcher;

/// <summary>
/// Discrete anonymous launcher core — DigitalAI parity for PowerAim.
/// Crypto random rename, AppData state, brand-pool discovery, sibling bin walk.
/// </summary>
public static class ExecutableManager
{
    private const int RandomNameLength = 8;
    private const int StateSchemaVersion = 1;

    /// <summary>AppData root folder name (not the product brand on disk next to the exe).</summary>
    private const string StateFolderName = "PowerAim";

    private const string LauncherStateSubfolder = "Launcher";
    private const string StateFileSuffix = "-launcher-state.json";
    private const string LegacyStateFileName = ".launcher-state";
    private const int MaxTrackedPreviousNames = 16;

    private sealed class LauncherState
    {
        [JsonPropertyName("schema")]
        public int Schema { get; set; } = StateSchemaVersion;

        [JsonPropertyName("launcherHash")]
        public string LauncherHash { get; set; } = string.Empty;

        [JsonPropertyName("currentExeName")]
        public string CurrentExeName { get; set; } = string.Empty;

        [JsonPropertyName("originalExeName")]
        public string OriginalExeName { get; set; } = string.Empty;

        [JsonPropertyName("workingDirectory")]
        public string WorkingDirectory { get; set; } = string.Empty;

        [JsonPropertyName("lastLaunchUtc")]
        public DateTime LastLaunchUtc { get; set; }

        [JsonPropertyName("launchCount")]
        public int LaunchCount { get; set; }

        [JsonPropertyName("previousExeNames")]
        public List<string> PreviousExeNames { get; set; } = new();
    }

    private static readonly JsonSerializerOptions StateJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static VersionResource? LoadResourceTable(string exe, Action<string>? onStatus)
    {
        try
        {
            InvokeOnUi(() => onStatus?.Invoke("Load resource"));
            var versionResource = new VersionResource();
            versionResource.LoadFrom(exe);
            return versionResource;
        }
        catch (Exception e)
        {
            InvokeOnUi(() => onStatus?.Invoke($"Error: {e.Message}"));
            return null;
        }
    }


    // NOTE: rewriting version resources to random strings (ChangeResourceTable / ScrubVersionResources) and
    // loading assemblies from in-memory byte[] are deliberately absent: both are textbook dropper/packer
    // heuristics that antivirus engines flag statically, even as dead code.

    /// <summary>
    /// Discovery: AppData state → legacy .launcher-state → brand pool / 8-char / sole exe
    /// in the launcher's folder.
    /// </summary>
    public static string? FindExecutable()
    {
        var launcherExe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(launcherExe)) return null;

        var launcherDir = Path.GetDirectoryName(launcherExe);
        if (string.IsNullOrEmpty(launcherDir)) return null;

        var state = ReadState(launcherExe);
        if (state is not null
            && !string.IsNullOrEmpty(state.WorkingDirectory)
            && !string.IsNullOrEmpty(state.CurrentExeName))
        {
            var fromState = Path.Combine(state.WorkingDirectory, state.CurrentExeName);
            if (File.Exists(fromState)) return fromState;
        }

        var legacy = ReadLegacyState(launcherDir);
        if (legacy is not null) return legacy;

        // Only the launcher's own folder: never pick up some other build further up the tree.
        return FindInDirectory(launcherDir, launcherExe);
    }

    /// <summary>
    /// Crypto-random rename → atomic state write → start process.
    /// Returns null on success, user-facing error otherwise.
    /// </summary>
    public static async Task<string?> RenameExecutable(
        string exe, Action<string>? beforeRename = null)
    {
        const string avHint =
            "If your antivirus flagged the app, add an exclusion for this folder and reinstall.";

        if (!File.Exists(exe))
            return "The app executable is gone — most likely quarantined by your antivirus. " + avHint;

        var workingDirectory = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(workingDirectory))
            return "Could not determine the app's working directory.";

        var launcherPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        var state = ReadState(launcherPath) ?? new LauncherState();

        var currentBaseName = Path.GetFileNameWithoutExtension(exe);
        if (string.IsNullOrEmpty(state.OriginalExeName))
            state.OriginalExeName = currentBaseName;

        var newName = GenerateUniqueRandomName(workingDirectory);
        var newExe = Path.Combine(workingDirectory, newName);

        try
        {
            InvokeOnUi(() => beforeRename?.Invoke(newName));

            File.Move(exe, newExe);

            if (!File.Exists(newExe))
                return "The rename completed but the target file is missing. " + avHint;

            // Do NOT rewrite PE resources on published builds.
            // Single-file apphost bundles are corrupted by ResourceLib.SaveTo
            // (host exits 0x8000809F BundleExtractionFailure). Identity change
            // is the random filename only.

            state.Schema = StateSchemaVersion;
            state.LauncherHash = ComputeLauncherHash(launcherPath);
            state.CurrentExeName = newName;
            state.WorkingDirectory = workingDirectory;
            state.LastLaunchUtc = DateTime.UtcNow;
            state.LaunchCount += 1;

            if (!string.IsNullOrEmpty(currentBaseName)
                && !string.Equals(currentBaseName, newName, StringComparison.OrdinalIgnoreCase)
                && !state.PreviousExeNames.Contains(currentBaseName, StringComparer.OrdinalIgnoreCase))
            {
                state.PreviousExeNames.Add(currentBaseName);
            }

            state.PreviousExeNames.RemoveAll(n =>
                string.Equals(n, newName, StringComparison.OrdinalIgnoreCase));

            if (state.PreviousExeNames.Count > MaxTrackedPreviousNames)
            {
                state.PreviousExeNames.RemoveRange(
                    0, state.PreviousExeNames.Count - MaxTrackedPreviousNames);
            }

            WriteState(state);
            DeleteLegacyState(workingDirectory);

            var process = Process.Start(new ProcessStartInfo(newExe)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDirectory
            });

            if (process == null)
                return "Windows refused to start the app. " + avHint;

            try { process.WaitForInputIdle(10_000); }
            catch (InvalidOperationException) { /* already exited or no message loop */ }

            if (process.HasExited)
            {
                var code = process.ExitCode;
                // 0x8000809F = BundleExtractionFailure (single-file host cannot extract bundle)
                if (unchecked((uint)code) == 0x8000809Fu)
                    return "The app failed to start (exit 0x8000809F BundleExtractionFailure). " +
                           "This usually means the single-file exe was modified after publish " +
                           "(e.g. VERSIONINFO rewrite). Rebuild/republish a clean copy and try again.";
                // 0x80008096 = FrameworkMissingFailure / app-launch-failed
                if (unchecked((uint)code) == 0x80008096u)
                    return $"The app failed to start (exit 0x80008096). Install the matching .NET desktop runtime, " +
                           $"or republish with --self-contained true. {avHint}";
                return $"The app exited right after starting (exit code {code}). {avHint}";
            }

            await Task.Delay(3000).ConfigureAwait(false);
            return null;
        }
        catch (Exception e)
        {
            return $"Could not start the app: {e.Message}\n{avHint}";
        }
    }

    private static LauncherState? ReadState(string launcherPath)
    {
        try
        {
            var path = GetStateFilePath(launcherPath);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<LauncherState>(json, StateJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteState(LauncherState state)
    {
        try
        {
            var path = GetStateFilePath(
                Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(state, StateJsonOptions);
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Non-fatal — next run falls back to directory discovery.
        }
    }

    private static string GetStateFilePath(string launcherPath)
    {
        var hash = ComputeLauncherHash(launcherPath);
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                StateFolderName,
                LauncherStateSubfolder);
            return Path.Combine(dir, hash + StateFileSuffix);
        }
        catch
        {
            var fallbackDir = Path.GetDirectoryName(launcherPath) ?? AppContext.BaseDirectory;
            return Path.Combine(fallbackDir, hash + StateFileSuffix);
        }
    }

    private static string? ReadLegacyState(string directory)
    {
        try
        {
            var path = Path.Combine(directory, LegacyStateFileName);
            if (!File.Exists(path)) return null;
            var contents = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(contents)) return null;
            var full = Path.Combine(directory, contents);
            return File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    private static void DeleteLegacyState(string directory)
    {
        try
        {
            var path = Path.Combine(directory, LegacyStateFileName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* non-fatal */ }
    }

    private static string? FindInDirectory(string directory, string launcherExe)
    {
        List<string> exeList;
        try
        {
            exeList = Directory.EnumerateFiles(directory, "*.exe")
                .Where(x => !string.Equals(x, launcherExe, StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.EndsWith("createdump.exe", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.EndsWith("Installer.exe", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.EndsWith("Launcher.exe", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch
        {
            return null;
        }

        if (exeList.Count == 0) return null;

        // Match build.ps1-stamped AssemblyName against Core.Constants.Names
        // (spaces stripped the same way build.ps1 cleans the filename).
        var byBrand = exeList.FirstOrDefault(x =>
        {
            var name = Path.GetFileNameWithoutExtension(x);
            return Constants.Names.Any(b =>
            {
                var cleaned = (b ?? string.Empty).Replace(" ", "", StringComparison.Ordinal);
                return string.Equals(name, cleaned, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(name, b, StringComparison.OrdinalIgnoreCase);
            });
        });
        if (byBrand is not null) return byBrand;

        var byRandomName = exeList.FirstOrDefault(x =>
            Path.GetFileNameWithoutExtension(x).Length == RandomNameLength);
        if (byRandomName is not null) return byRandomName;

        return exeList.Count == 1 ? exeList[0] : null;
    }

    private static string GenerateUniqueRandomName(string directory)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var candidate = $"{GenerateRandomString()}.exe";
            if (!File.Exists(Path.Combine(directory, candidate)))
                return candidate;
        }
        return $"{GenerateRandomString(RandomNameLength + 4)}.exe";
    }

    /// <summary>Cryptographic RNG — not System.Random.</summary>
    private static string GenerateRandomString(int length = RandomNameLength)
    {
        const string chars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        return new string(result);
    }

    private static string ComputeLauncherHash(string launcherPath)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(launcherPath.ToLowerInvariant());
            var hash = SHA256.HashData(bytes);
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
        catch
        {
            return "default";
        }
    }

    private static void InvokeOnUi(Action action)
    {
        // Fully qualify — WinForms + WPF both define Application.
        var app = System.Windows.Application.Current;
        if (app is null) { action(); return; }
        if (app.Dispatcher.CheckAccess())
            action();
        else
            app.Dispatcher.Invoke(action);
    }
}