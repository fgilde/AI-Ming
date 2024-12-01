using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Aimmy2.Other;

public enum DefenderExclusionType
{
    File,
    Folder,
    None
}

public static class WindowsHelper
{
    private static string _toolDirectory = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "Resources");

    public static void RunResourceToolAsAdmin(string fileName, DefenderExclusionType windowsDefenderExclusionType = DefenderExclusionType.None, Action beforeStart = null, Action<Process> onExit = null) 
        => RunAsAdmin(Path.Combine(_toolDirectory, fileName), windowsDefenderExclusionType, beforeStart, onExit);

    public static void RunAsAdmin(string fullFilePath, DefenderExclusionType windowsDefenderExclusionType = DefenderExclusionType.None, Action beforeStart = null, Action<Process> onExit = null)
    {
        //var toolDirectory = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "Resources");
        //var toolPath = Path.Combine(toolDirectory, "SecHex-GUI.exe");
        if (File.Exists(fullFilePath) && AddToWindowsDefenderExclusions(fullFilePath, windowsDefenderExclusionType))
        {
            var psi = new ProcessStartInfo
            {
                FileName = fullFilePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                beforeStart?.Invoke();
                var p = Process.Start(psi);
                p.Exited += (sender, args) => onExit?.Invoke(p);
            }
            catch (System.ComponentModel.Win32Exception)
            { }
        }
    }

    public static void RunResourceTool(string fileName, Action beforeStart = null, Action<Process> onExit = null) => Run(Path.Combine(_toolDirectory, fileName), beforeStart, onExit);

    public static void Run(string fullFilePath, Action beforeStart = null, Action<Process> onExit = null)
    {
        if (File.Exists(fullFilePath))
        {
            beforeStart?.Invoke();
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = fullFilePath,
            });
            p.Exited += (sender, args) => onExit?.Invoke(p);
        }
    }

    public static bool AddToWindowsDefenderExclusions(string path, DefenderExclusionType windowsDefenderExclusionType)
    {
        if (windowsDefenderExclusionType == DefenderExclusionType.None)
            return true;
        
        if (windowsDefenderExclusionType == DefenderExclusionType.Folder && File.Exists(path))
            path = Path.GetDirectoryName(path);

        var psCommand = $"Add-MpPreference -ExclusionPath '{path}'";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-Command \"{psCommand}\"",
            Verb = "runas", // Startet den Prozess mit Administratorrechten
            UseShellExecute = true,
            CreateNoWindow = true
        };

        try
        {
            var process = Process.Start(psi);
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                return true;
                //MessageBox.Show("Das Tool wurde erfolgreich zu den Windows Defender-Ausnahmen hinzugefügt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                return false;
                //MessageBox.Show("Fehler beim Hinzufügen des Tools zu den Windows Defender-Ausnahmen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            // MessageBox.Show($"Ein Fehler ist aufgetreten: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

}