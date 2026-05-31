using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using Visuality;

namespace MouseMovementLibraries.RazerSupport;

internal class RZMouse
{
    #region Razer Variables

    private const string rzctlpath = "rzctl.dll";
    private const string rzctlDownloadUrl = "https://github.com/MarsQQ/rzctl/releases/download/1.0.0/rzctl.dll";

    [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool init();

    [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mouse_move(int x, int y, bool starting_point);

    [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mouse_click(int up_down);

    private static readonly List<string> Razer_HID = [];

    #endregion Razer Variables

    public static bool CheckForRazerDevices()
    {
        Razer_HID.Clear();
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE 'Razer%'");
        var razerDevices = searcher.Get().Cast<ManagementBaseObject>();

        Razer_HID.AddRange(razerDevices.Select(device => device["DeviceID"]?.ToString() ?? string.Empty));

        return Razer_HID.Count != 0;
    }

    /// <summary>
    ///     Process names Razer uses across Synapse versions. The legacy "Razer Synapse" alone
    ///     misses Synapse 4 (which ships <c>RazerAppEngine</c> as the main engine and
    ///     <c>Razer Synapse Service</c> / <c>Razer Synapse Service Process</c> as the background
    ///     service that actually exposes <c>rzctl.dll</c>). We treat any of these running as
    ///     "Synapse is installed and running".
    /// </summary>
    private static readonly string[] RazerSynapseProcessNames =
    {
        "RazerAppEngine",              // Synapse 4 main engine (recommended)
        "Razer Synapse Service",       // background service
        "Razer Synapse Service Process",
        "Razer Synapse",               // Synapse 3 legacy
        "RazerCentralService",
        "Razer Central"
    };

    public static async Task<bool> CheckRazerSynapseInstall()
    {
        bool anyRunning = RazerSynapseProcessNames
            .Any(name => Process.GetProcessesByName(name).Length > 0);
        if (!anyRunning)
        {
            var result = PowerAim.Visuality.MessageDialog.Show("Razer Synapse is not running, do you have it installed?", "PowerAim - Razer Synapse", PowerAim.Visuality.MessageDialog.DialogButtons.YesNo);
            if (result == PowerAim.Visuality.MessageDialog.DialogResult.No)
            {
                await InstallRazerSynapse();
                return false;
            }
            else
            {
                return true;
            }
        }
        else return true;
    }

    private static async Task InstallRazerSynapse()
    {
        using HttpClient httpClient = new();

        var response = await httpClient.GetAsync(new Uri("https://rzr.to/synapse-new-pc-download-beta"));
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync($"{Path.GetTempPath()}\\rz.exe", content);
        }

        new NoticeBar("Razer Synapse downloaded, please look for UAC prompt and install Razer Synapse.", 4000).Show();

        Process.Start(new ProcessStartInfo
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = "cmd.exe",
            Arguments = "/C start rz.exe",
            WorkingDirectory = Path.GetTempPath()
        });
    }

    private static async Task downloadrzctl()
    {
        try
        {
            new NoticeBar($"{rzctlpath} is missing, attempting to download {rzctlpath}.", 4000).Show();

            using HttpClient httpClient = new();
            using var response = await httpClient.GetAsync(new Uri(rzctlDownloadUrl), HttpCompletionOption.ResponseHeadersRead);

            if (response.IsSuccessStatusCode)
            {
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(rzctlpath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await contentStream.CopyToAsync(fileStream);
                new NoticeBar($"{rzctlpath} has downloaded successfully, please re-select Razer Synapse to load the DLL.", 4000).Show();
            }
        }
        catch
        {
            new NoticeBar($"{rzctlpath} has failed to install, please try a different Mouse Movement Method.", 4000).Show();
        }
    }

    public static async Task<bool> Load()
    {
        if (!await CheckRazerSynapseInstall())
        {
            return false;
        }
        if (!File.Exists(rzctlpath))
        {
            await downloadrzctl();
            return false;
        }
        if (!CheckForRazerDevices())
        {
            PowerAim.Visuality.MessageDialog.Show("No Razer Peripheral is detected, this Mouse Movement Method is unusable.", "PowerAim", PowerAim.Visuality.MessageDialog.DialogButtons.OK, PowerAim.Visuality.MessageDialog.DialogIcon.Warning);
            return false;
        }
        try
        {
            return init();
        }
        catch (Exception ex)
        {
            PowerAim.Visuality.MessageDialog.Show($"Unfortunately, Razer Synapse mode cannot be ran sufficiently.\n{ex}", "PowerAim", PowerAim.Visuality.MessageDialog.DialogButtons.OK, PowerAim.Visuality.MessageDialog.DialogIcon.Error);
            return false;
        }
    }
}