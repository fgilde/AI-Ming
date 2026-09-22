using System.Diagnostics;
using System.IO;
using Vestris.ResourceLib;

public class ExecutableManager
{
    private static readonly Random _random = new Random();

   
    public static VersionResource? LoadResourceTable(string exe, Action<string> onStatus)
    {
        try
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => onStatus?.Invoke($"Load resources"));

            var versionResource = new VersionResource();
            versionResource.LoadFrom(exe);
            return versionResource;
        }
        catch (Exception e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => onStatus?.Invoke($"Error: {e.Message}"));
            return null;
        }
    }

    // NOTE: the old ChangeResourceTable (rewrite version resources to random strings) and
    // LoadAssemblyViaStream (Assembly.Load from an in-memory byte[]) were removed: both were dead code,
    // and both are textbook dropper/packer heuristics that antivirus engines flag statically — their mere
    // presence in the binary contributed to false-positive detections.

    public static string FindExecutable()
    {
        var launcherExe = Process.GetCurrentProcess().MainModule.FileName;
        var currentDir = Path.GetDirectoryName(launcherExe);
        //var currentDir = @"C:\dev\privat\github\AI-Ming\PowerAim\bin\Release\Release_1.0.0.4";

        var exeList = Directory.EnumerateFiles(currentDir, "*.exe")
            .Where(x => x != launcherExe && !x.EndsWith("createdump.exe") && !x.EndsWith("Installer.exe"))
            .ToList();

        if (exeList.Count == 1)
            return exeList[0];

        return exeList.FirstOrDefault(n => Path.GetFileNameWithoutExtension(n).Length == 8);
    }

    /// <summary>
    ///     Rename + start the app. Returns <c>null</c> on success, otherwise a user-facing error. Every
    ///     step here can fail when an antivirus quarantines the exe or blocks its start — previously
    ///     those exceptions were swallowed by the fire-and-forget caller and the launcher just sat there
    ///     on "Shuffle name to …" forever with no message.
    /// </summary>
    public static async Task<string?> RenameExecutable(string exe, Action<string> beforeRename = null)
    {
        const string avHint = "If your antivirus flagged the app, add an exclusion for this folder and reinstall.";

        if (!File.Exists(exe))
            return "The app executable is gone — most likely quarantined by your antivirus. " + avHint;

        var workingDirectory = Path.GetDirectoryName(exe);
        string newName = $"{GenerateRandomString()}.exe";
        var newExe = Path.Combine(workingDirectory, newName);
        try
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => beforeRename?.Invoke(newName));
            File.Move(exe, newExe);

            var process = Process.Start(new ProcessStartInfo(newExe) { UseShellExecute = true, WorkingDirectory = workingDirectory });
            if (process == null)
                return "Windows refused to start the app. " + avHint;

            // ponytail: 10s cap — a process the AV kills on launch never reaches input-idle and would hang here.
            try { process.WaitForInputIdle(10000); }
            catch (InvalidOperationException) { /* already exited or no message loop — handled below */ }

            if (process.HasExited)
                return $"The app exited right after starting (exit code {process.ExitCode}) — an antivirus probably blocked it. " + avHint;

            await Task.Delay(3000);
            return null;
        }
        catch (Exception e)
        {
            return $"Could not start the app: {e.Message}\n" + avHint;
        }
    }

    private static string GenerateRandomString(int length = 8)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}