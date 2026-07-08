using System.Diagnostics;
using System.IO;
using SharpDX.XInput;
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

    public static string? ReadingControllerId { get; private set; }

    /// <summary>
    ///     Facade for the active reader's button events. Consumers (e.g. InputBindingManager) subscribe
    ///     HERE, not to the reader directly, so swapping the reader (XInput ↔ DirectInput) stays
    ///     transparent and never leaves a handler bound to a disposed reader.
    /// </summary>
    public static event EventHandler<GamepadEventArgs>? ButtonEvent;


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
            SetReader(CreateReader());
            if (GamepadReader is IXInputGamepadReader xir)
            {
                ResolvePreferredSource(); // honour a persisted XInput sync-source choice (by device id)
                ReadingControllerId = xir.Controller.GetControllerId(); // before the virtual pad is created
            }
            else if (GamepadReader is DirectInputGamepadReader dir)
            {
                ReadingControllerId = "dinput:" + dir.InstanceGuid;
            }
        }

        try
        {
            RawSender = CreateSender();
            // Wrap so the debug input visualizer observes every send, for any send mode.
            GamepadSender = RawSender == null ? null : new ReportingGamepadSender(RawSender);
            // Pass the physical controller along, but the sender now tolerates a missing one —
            // it'll still pump direct SetButton/Axis calls so the aim pipeline can drive the
            // virtual pad even when no real controller is plugged in.
            GamepadSender?.SyncWith(GamepadReader as IGamepadStateSource);
            _controllerHidden = GamepadSender != null
                                && (GamepadSender.CanWork)
                                && AppConfig.Current.ToggleState.AutoHideController;
            if (_controllerHidden && GamepadReader is IXInputGamepadReader xh)
                xh.Controller.Hide();
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

    /// <summary>Install a reader, re-pointing the <see cref="ButtonEvent"/> facade at it (detaching the old).</summary>
    private static void SetReader(IGamepadReader reader)
    {
        if (GamepadReader != null) GamepadReader.ButtonEvent -= OnReaderButtonEvent;
        GamepadReader = reader;
        GamepadReader.ButtonEvent += OnReaderButtonEvent;
    }

    private static void OnReaderButtonEvent(object? sender, GamepadEventArgs e) => ButtonEvent?.Invoke(sender, e);

    /// <summary>
    ///     Pick the reader transport: prefer an XInput pad (Xbox, or a DualSense/DS4 exposed as XInput via
    ///     Steam Input / DS4Windows). If no XInput slot is occupied, fall back to a raw DirectInput/HID pad
    ///     (e.g. a PS5 DualSense with no remapper). Defaults to the XInput reader when nothing is attached
    ///     yet — it re-scans slots as pads appear.
    /// </summary>
    private static IGamepadReader CreateReader()
    {
        for (var i = UserIndex.One; i <= UserIndex.Four; i++)
            if (new Controller(i).IsConnected)
                return new GamepadReader();

        var guid = DirectInputGamepadReader.FindFirstDevice();
        return guid is { } g ? new DirectInputGamepadReader(g) : new GamepadReader();
    }

    /// <summary>
    ///     Switch the read/sync source to a specific DirectInput pad (e.g. a raw DualSense the user picked).
    ///     Transparent to consumers thanks to the <see cref="ButtonEvent"/> facade; also re-points the sync
    ///     so the pad can drive the virtual controller.
    /// </summary>
    public static void UseDirectInputSource(Guid instanceGuid)
    {
        var old = GamepadReader;
        SetReader(new DirectInputGamepadReader(instanceGuid));
        if (!ReferenceEquals(old, GamepadReader)) old?.Dispose();
        ReadingControllerId = "dinput:" + instanceGuid;
        GamepadSender?.SyncWith(GamepadReader as IGamepadStateSource);
        if (AppConfig.Current?.ControllerSettings != null)
            AppConfig.Current.ControllerSettings.PreferredSyncDeviceId = ReadingControllerId;
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
                GamepadSendMode.DualShock4 => new GamepadSenderDualShock4(),
                GamepadSendMode.TitanTwo => new GamepadSenderTitanTwo(),
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

    /// <summary>
    ///     Switch which physical XInput slot feeds the sync (mirror into the virtual pad) at runtime.
    ///     No reader teardown / sender restart — the sync loop reads the physical reference fresh each
    ///     tick. The choice is persisted by DEVICE ID so it survives restarts and slot changes.
    /// </summary>
    public static void SetSyncSource(UserIndex slot)
    {
        // Picking an XInput slot: if we were on a DirectInput reader, swap back to the XInput reader first.
        if (GamepadReader is not IXInputGamepadReader)
        {
            var old = GamepadReader;
            SetReader(new GamepadReader());
            if (!ReferenceEquals(old, GamepadReader)) old?.Dispose();
        }
        if (GamepadReader is not IXInputGamepadReader xr) return;
        xr.UseSlot(slot);
        ReadingControllerId = xr.Controller.GetControllerId();
        GamepadSender?.SyncWith(GamepadReader as IGamepadStateSource);
        if (AppConfig.Current?.ControllerSettings != null)
            AppConfig.Current.ControllerSettings.PreferredSyncDeviceId = ReadingControllerId ?? "";
    }

    /// <summary>
    ///     Force the ViGEm virtual pad to disconnect+reconnect — Windows treats it as a fresh plug and
    ///     re-enumerates XInput slots, so it can claim a freed low slot. Uses <see cref="RawSender"/>
    ///     (the unwrapped concrete sender), not the reporting wrapper. No-op for non-ViGEm send modes.
    /// </summary>
    public static bool ReconnectVirtual() => RawSender is global::GamepadSenderViGEm v && v.Reconnect();

    /// <summary>
    ///     If the user previously picked a sync source (persisted by device id), find which XInput slot
    ///     currently holds that device and switch the reader to it. If it isn't present on any slot, the
    ///     auto-scanned source (last-known slot, then scan) is kept.
    /// </summary>
    private static void ResolvePreferredSource()
    {
        var pref = AppConfig.Current?.ControllerSettings?.PreferredSyncDeviceId;
        if (string.IsNullOrEmpty(pref) || GamepadReader is not IXInputGamepadReader xr) return;
        for (var i = UserIndex.One; i <= UserIndex.Four; i++)
        {
            var c = new Controller(i);
            if (c.IsConnected && c.GetControllerId() == pref)
            {
                xr.UseSlot(i);
                return;
            }
        }
    }

    public static void Dispose()
    {
        if (_controllerHidden && GamepadReader is IXInputGamepadReader xs)
            xs.Controller.Show();
        // GamepadReader?.Dispose();
        GamepadSender?.Dispose();
        GamepadSender = null;
        RawSender = null;
        CanRead = false;
    }

}