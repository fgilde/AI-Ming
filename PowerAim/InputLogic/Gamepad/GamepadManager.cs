using System.Diagnostics;
using System.IO;
using PowerAim.Config;
using PowerAim.InputLogic.Contracts;
using PowerAim.InputLogic.Gamepad.Interaction;
using PowerAim.InputLogic.HidHide;
using PowerAim.Models;

namespace PowerAim.InputLogic;

public static class GamepadManager
{
    private static bool _controllerHidden;
    public static bool CanRead { get; private set; }
    public static IGamepadReader? GamepadReader { get; private set; }

    /// <summary>
    ///     The sender used for all output. This is a <see cref="ReportingGamepadSender"/> wrapper
    ///     around the concrete sender so the debug input visualizer sees every emitted input
    ///     regardless of send mode. Use <see cref="RawSender"/> when you need the underlying type.
    /// </summary>
    public static IGamepadSender? GamepadSender { get; private set; }

    /// <summary>The unwrapped concrete sender (e.g. for <c>is GamepadSenderInternal</c> checks).</summary>
    public static IGamepadSender? RawSender { get; private set; }

    public static bool CanSend => GamepadSender?.CanWork ?? false;

    public static void Init()
    {
        Dispose();
        if (GamepadReader == null)
        {
            GamepadReader = new GamepadReader();
            GamepadReader.Controller.GetControllerId(); // Needs to be called before virtual one is created
        }

        try
        {
            RawSender = CreateSender();
            // Wrap so the debug input visualizer observes every send, for any send mode.
            GamepadSender = RawSender == null ? null : new ReportingGamepadSender(RawSender);
            // Pass the physical controller along, but the sender now tolerates a missing one —
            // it'll still pump direct SetButton/Axis calls so the aim pipeline can drive the
            // virtual pad even when no real controller is plugged in.
            GamepadSender?.SyncWith(GamepadReader.Controller);
            _controllerHidden = GamepadSender != null
                                && (GamepadSender.CanWork)
                                && AppConfig.Current.ToggleState.AutoHideController;
            if (_controllerHidden)
                GamepadReader.Controller.Hide();
        }
        catch (Exception e)
        {
            // Previously this rethrew, killing the whole UI bootstrap when ViGEm wasn't
            // installed. Now we degrade gracefully: GamepadSender stays null, CanSend reports
            // false, and the UI's "Use controller for aim" toggle gates itself accordingly.
            Console.WriteLine($"[GamepadManager] Init failed: {e.Message}");
            GamepadSender = null;
            RawSender = null;
        }
        finally
        {
            CanRead = true;
        }
    }

    private static IGamepadSender? CreateSender()
    {
        try
        {
            return AppConfig.Current.DropdownState.GamepadSendMode switch
            {
                GamepadSendMode.ViGEm => new GamepadSenderViGEm(),
                GamepadSendMode.VJoy => new GamepadSenderVJoy(),
                GamepadSendMode.XInputHook => CreateXInputHook(),
                GamepadSendMode.Internal => new GamepadSenderInternal(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            // VJoy / Internal constructors can throw on missing drivers. Surface the failure so
            // the user sees it via the diagnostic panel + CanSend stays false instead of
            // crashing the whole UI bootstrap.
            Console.WriteLine($"[GamepadManager] Sender constructor failed for {AppConfig.Current.DropdownState.GamepadSendMode}: {ex.Message}");
            return null;
        }
    }

    private static IGamepadSender? CreateXInputHook()
    {
        // Best-effort spawn. Everything inside is wrapped so a missing process, missing
        // XInputEmu.exe, elevation refusal, or even XInputEmu crashing on bad bitness
        // (its bundled XInputHook.dll is x86-only — fails for any 64-bit game) all just log
        // the underlying error and return null. Init() handles null sender gracefully.
        try
        {
            var process = ProcessModel.FindProcessByTitle(AppConfig.Current.DropdownState.GamepadProcess);
            if (process == null)
            {
                Console.WriteLine("[GamepadSenderXInputEmu] Target process not found — check GamepadProcess setting.");
                return null;
            }
            var xInputEmuProcess = Process.GetProcesses().FirstOrDefault(p =>
            {
                try { return Path.GetFileName(p.MainModule.FileName) == "XInputEmu.exe"; }
                catch { return false; }
            });
            if (xInputEmuProcess != null) xInputEmuProcess.Kill();
            var fileName = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                                        "Resources", "XInputEmu", "XInputEmu.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = $"{process.Id}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(fileName)
            };
            Process.Start(startInfo);
            return new GamepadSenderXInputEmu();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GamepadSenderXInputEmu] Setup failed: {ex.Message}");
            return null;
        }
    }

    public static void Dispose()
    {
        if (_controllerHidden)
            GamepadReader?.Controller.Show();
        // GamepadReader?.Dispose();
        GamepadSender?.Dispose();
        GamepadSender = null;
        RawSender = null;
        CanRead = false;
    }

}