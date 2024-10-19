using System.Diagnostics;
using System.IO;
using System.Reflection;
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

    private void ChangeResourceTable(VersionResource versionResource, string exe)
    {
        string newName = GenerateRandomString(15).ToUpper();
        var resource = versionResource["StringFileInfo"];
        var fi = resource as StringFileInfo;

        foreach (var table in fi.Strings.Select(pair => pair.Value))
        {
            table["CompanyName"] = newName;
            table["FileDescription"] = newName;
            table["InternalName"] = $"{newName}.dll";
            table["OriginalFilename"] = $"{newName}.dll";
            table["ProductName"] = newName;
        }


        versionResource.SaveTo(exe);
    }

    public static Assembly LoadAssemblyViaStream(string assemblyLocation)
    {
        byte[] file = null;
        int bufferSize = 1024;
        using (FileStream fileStream = File.Open(assemblyLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int readBytesCount = 0;
                while ((readBytesCount = fileStream.Read(buffer, 0, bufferSize)) > 0)
                    memoryStream.Write(buffer, 0, readBytesCount);
                file = memoryStream.ToArray();
            }
        }

        return Assembly.Load(file);
    }



    public static string FindExecutable()
    {
        var launcherExe = Process.GetCurrentProcess().MainModule.FileName;
        var currentDir = Path.GetDirectoryName(launcherExe);
        //var currentDir = @"C:\dev\privat\github\AI-Ming\Aimmy2\bin\Release\Release_1.0.0.4";

        var exeList = Directory.EnumerateFiles(currentDir, "*.exe")
            .Where(x => x != launcherExe && !x.EndsWith("createdump.exe") && !x.EndsWith("Installer.exe"))
            .ToList();

        if (exeList.Count == 1)
            return exeList[0];

        return exeList.FirstOrDefault(n => Path.GetFileNameWithoutExtension(n).Length == 8);
    }

    public static async Task RenameExecutable(string exe, Action<string> beforeRename = null)
    {
        var workingDirectory = Path.GetDirectoryName(exe);
        string newName = $"{GenerateRandomString()}.exe";
        System.Windows.Application.Current.Dispatcher.Invoke(() => beforeRename?.Invoke(newName));
        var newExe = Path.Combine(Path.GetDirectoryName(exe), newName);
        File.Move(exe, newExe);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo(newExe) { UseShellExecute = true, WorkingDirectory = workingDirectory}
        };

        process.Start();
        process.WaitForInputIdle();
        await Task.Delay(3000);
    }

    private static string GenerateRandomString(int length = 8)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}