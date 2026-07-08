using System.IO;
using System.Runtime.InteropServices;
using PowerAim.InputLogic.Contracts;
using SharpDX.XInput;

/// <summary>
///     Sends input to a ConsoleTuner <b>Titan Two</b> (and, being the same API, a classic CronusMax /
///     Titan One) through its GCAPI native library (<c>gcapi.dll</c>). We build a PS4-style output frame
///     (<c>int8_t[GCAPI_INPUT_TOTAL]</c>: buttons 0/100, triggers 0..100, sticks −100..100) and push it
///     with <c>gcapi_Write</c>.
///     <para>
///     UNTESTED here — it needs the proprietary <c>gcapi.dll</c> present next to the exe AND a real Titan
///     Two attached. Everything is wrapped so that a missing DLL or device leaves the sender completely
///     inert (<see cref="CanWork"/> = false) instead of crashing. The PS4 index map + GCAPI_INPUT_TOTAL
///     below follow the classic <c>gcapi.h</c>; VERIFY them against the exact gcapi.dll you ship, as
///     ConsoleTuner has revised the layout across devices/versions.
///     </para>
/// </summary>
public class GamepadSenderTitanTwo : IGamepadSender
{
    private const string GcApiDll = "gcapi.dll";
    // ConsoleTuner's official name for the same classic direct API. The user may have either file, so we
    // probe both and reroute the [DllImport("gcapi.dll")] calls to whichever one actually loaded.
    private const string GcDApiDll = "gcdapi.dll";
    private static readonly string[] ProbeNames = { GcApiDll, GcDApiDll };
    private const int GcapiInputTotal = 30; // classic gcapi.h; VERIFY for your device/version

    // PS4 output indices (classic gcapi.h). VERIFY against your gcapi.h.
    private const int PS4_PS = 0, PS4_SHARE = 1, PS4_OPTIONS = 2, PS4_R1 = 3, PS4_R2 = 4, PS4_R3 = 5,
        PS4_L1 = 6, PS4_L2 = 7, PS4_L3 = 8, PS4_RX = 9, PS4_RY = 10, PS4_LX = 11, PS4_LY = 12,
        PS4_UP = 13, PS4_DOWN = 14, PS4_LEFT = 15, PS4_RIGHT = 16, PS4_TRIANGLE = 17,
        PS4_CIRCLE = 18, PS4_CROSS = 19, PS4_SQUARE = 20;

    // Handle to the GCAPI DLL once preflighted with NativeLibrary.TryLoad. Kept alive for the process
    // lifetime; the DllImportResolver below routes every [DllImport("gcapi.dll")] to it (works even when
    // the user supplied gcdapi.dll). IntPtr.Zero = not loaded → sender stays fully inert, no P/Invoke.
    private static IntPtr _gcApiLib;
    private static bool _resolverInstalled;

    [DllImport(GcApiDll, CallingConvention = CallingConvention.Cdecl)] private static extern bool gcapi_Load();
    [DllImport(GcApiDll, CallingConvention = CallingConvention.Cdecl)] private static extern bool gcapi_Unload();
    [DllImport(GcApiDll, CallingConvention = CallingConvention.Cdecl)] private static extern bool gcapi_IsConnected();
    [DllImport(GcApiDll, CallingConvention = CallingConvention.Cdecl)] private static extern bool gcapi_Write(sbyte[] output);

    private readonly sbyte[] _output = new sbyte[GcapiInputTotal];
    private readonly HashSet<GamepadButton> _pausedButtons = new();
    private readonly HashSet<GamepadSlider> _pausedSliders = new();
    private readonly HashSet<GamepadAxis> _pausedAxes = new();
    private IGamepadStateSource? _source;
    private bool _isRunning;
    private bool _connected;

    public string LastError { get; private set; } = "";

    public GamepadSenderTitanTwo()
    {
        // The GCAPI DLL is proprietary (ConsoleTuner) and NOT shipped with PowerAim — the user drops
        // their own copy into the GCAPI\ folder next to the app (see GCAPI\README.txt). We probe for it
        // with NativeLibrary.TryLoad, which returns false instead of throwing when the file is absent, so
        // nothing bubbles up and the debugger doesn't break. NOTE: the classic GCAPI DLL is 32-bit and
        // CANNOT load into this 64-bit process — TryLoad then fails and we report a precise bitness hint
        // instead of a misleading "not found".
        try
        {
            if (!TryLoadGcApi(out var presentButUnloadable))
            {
                LastError = presentButUnloadable != null
                    ? $"Found {Path.GetFileName(presentButUnloadable)} but it failed to load — it is almost certainly 32-bit; PowerAim is 64-bit and needs a 64-bit GCAPI DLL."
                    : "No GCAPI DLL found — place a 64-bit gcapi.dll or gcdapi.dll in the GCAPI\\ folder next to the app (see GCAPI\\README.txt).";
                _connected = false;
                return;
            }

            // Route [DllImport("gcapi.dll")] to whichever module loaded (the user may have supplied
            // gcdapi.dll). Registered once per assembly — a second SetDllImportResolver call would throw.
            if (!_resolverInstalled)
            {
                NativeLibrary.SetDllImportResolver(typeof(GamepadSenderTitanTwo).Assembly,
                    (name, _, _) => name == GcApiDll ? _gcApiLib : IntPtr.Zero);
                _resolverInstalled = true;
            }

            _connected = gcapi_Load() && gcapi_IsConnected();
            if (!_connected)
                LastError = "GCAPI DLL loaded, but no Titan Two is connected.";
        }
        catch (Exception ex) { LastError = ex.Message; _connected = false; }
    }

    /// <summary>
    ///     Probe the GCAPI\ folder next to the app, then the app dir itself, for gcapi.dll / gcdapi.dll
    ///     and load the first one that exists. Returns true once a module is loaded (or was already).
    ///     <paramref name="presentButUnloadable"/> is set to the path of a file that EXISTS but refuses
    ///     to load — almost always a 32-bit DLL in this 64-bit process — so the caller can say so
    ///     precisely instead of "file not found".
    /// </summary>
    private static bool TryLoadGcApi(out string? presentButUnloadable)
    {
        presentButUnloadable = null;
        if (_gcApiLib != IntPtr.Zero) return true;

        var baseDir = AppContext.BaseDirectory;
        foreach (var dir in new[] { Path.Combine(baseDir, "GCAPI"), baseDir })
        {
            foreach (var name in ProbeNames)
            {
                var full = Path.Combine(dir, name);
                if (!File.Exists(full)) continue;
                if (NativeLibrary.TryLoad(full, out _gcApiLib)) return true;
                presentButUnloadable = full; // present but won't load (wrong bitness / missing deps)
            }
        }
        return false;
    }

    public bool CanWork => _connected;

    public IGamepadSender SyncWith(IGamepadStateSource? source)
    {
        _source = source;
        if (!_connected || _isRunning) return this;
        _isRunning = true;
        new Thread(SyncLoop) { IsBackground = true, Name = "GamepadSenderTitanTwo-Loop" }.Start();
        return this;
    }

    public IGamepadSender StopSync() { _isRunning = false; return this; }

    public IGamepadSender PauseSync(GamepadButton button) { _pausedButtons.Add(button); return this; }
    public IGamepadSender PauseSync(GamepadSlider slider) { _pausedSliders.Add(slider); return this; }
    public IGamepadSender PauseSync(GamepadAxis axis) { _pausedAxes.Add(axis); return this; }
    public IGamepadSender ResumeSync() { _pausedButtons.Clear(); _pausedSliders.Clear(); _pausedAxes.Clear(); return this; }
    public IGamepadSender ResumeSync(GamepadButton button) { _pausedButtons.Remove(button); return this; }
    public IGamepadSender ResumeSync(GamepadSlider slider) { _pausedSliders.Remove(slider); return this; }
    public IGamepadSender ResumeSync(GamepadAxis axis) { _pausedAxes.Remove(axis); return this; }

    public IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused) PauseSync(button);
        _output[ButtonIndex(button)] = (sbyte)(pressed ? 100 : 0);
        Flush();
        if (gamepadSyncState == GamepadSyncState.Resume) ResumeSync(button);
        return this;
    }

    public IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused) PauseSync(slider);
        _output[slider == GamepadSlider.LeftTrigger ? PS4_L2 : PS4_R2] = (sbyte)(value * 100 / 255);
        Flush();
        if (gamepadSyncState == GamepadSyncState.Resume) ResumeSync(slider);
        return this;
    }

    public IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused) PauseSync(axis);
        _output[AxisIndex(axis)] = (sbyte)Math.Clamp(value * 100 / 32767, -100, 100);
        Flush();
        if (gamepadSyncState == GamepadSyncState.Resume) ResumeSync(axis);
        return this;
    }

    private static int ButtonIndex(GamepadButton b) => b switch
    {
        GamepadButton.A => PS4_CROSS,
        GamepadButton.B => PS4_CIRCLE,
        GamepadButton.X => PS4_SQUARE,
        GamepadButton.Y => PS4_TRIANGLE,
        GamepadButton.LeftShoulder => PS4_L1,
        GamepadButton.RightShoulder => PS4_R1,
        GamepadButton.Back => PS4_SHARE,
        GamepadButton.Start => PS4_OPTIONS,
        GamepadButton.LeftThumb => PS4_L3,
        GamepadButton.RightThumb => PS4_R3,
        GamepadButton.Up => PS4_UP,
        GamepadButton.Down => PS4_DOWN,
        GamepadButton.Left => PS4_LEFT,
        GamepadButton.Right => PS4_RIGHT,
        _ => PS4_CROSS,
    };

    private static int AxisIndex(GamepadAxis a) => a switch
    {
        GamepadAxis.LeftThumbX => PS4_LX,
        GamepadAxis.LeftThumbY => PS4_LY,
        GamepadAxis.RightThumbX => PS4_RX,
        GamepadAxis.RightThumbY => PS4_RY,
        _ => PS4_LX,
    };

    private void Flush()
    {
        if (!_connected) return;
        try { gcapi_Write(_output); }
        catch (Exception ex) { LastError = ex.Message; }
    }

    private void SyncLoop()
    {
        while (_isRunning)
        {
            if (_source is { IsConnected: true })
            {
                State state;
                try { state = _source.GetState(); } catch { Thread.Sleep(10); continue; }
                var b = state.Gamepad.Buttons;

                void Btn(GamepadButton g, GamepadButtonFlags f) { if (!_pausedButtons.Contains(g)) _output[ButtonIndex(g)] = (sbyte)(b.HasFlag(f) ? 100 : 0); }
                Btn(GamepadButton.A, GamepadButtonFlags.A); Btn(GamepadButton.B, GamepadButtonFlags.B);
                Btn(GamepadButton.X, GamepadButtonFlags.X); Btn(GamepadButton.Y, GamepadButtonFlags.Y);
                Btn(GamepadButton.LeftShoulder, GamepadButtonFlags.LeftShoulder); Btn(GamepadButton.RightShoulder, GamepadButtonFlags.RightShoulder);
                Btn(GamepadButton.Back, GamepadButtonFlags.Back); Btn(GamepadButton.Start, GamepadButtonFlags.Start);
                Btn(GamepadButton.LeftThumb, GamepadButtonFlags.LeftThumb); Btn(GamepadButton.RightThumb, GamepadButtonFlags.RightThumb);
                Btn(GamepadButton.Up, GamepadButtonFlags.DPadUp); Btn(GamepadButton.Down, GamepadButtonFlags.DPadDown);
                Btn(GamepadButton.Left, GamepadButtonFlags.DPadLeft); Btn(GamepadButton.Right, GamepadButtonFlags.DPadRight);

                if (!_pausedSliders.Contains(GamepadSlider.LeftTrigger)) _output[PS4_L2] = (sbyte)(state.Gamepad.LeftTrigger * 100 / 255);
                if (!_pausedSliders.Contains(GamepadSlider.RightTrigger)) _output[PS4_R2] = (sbyte)(state.Gamepad.RightTrigger * 100 / 255);
                if (!_pausedAxes.Contains(GamepadAxis.LeftThumbX)) _output[PS4_LX] = (sbyte)Math.Clamp(state.Gamepad.LeftThumbX * 100 / 32767, -100, 100);
                if (!_pausedAxes.Contains(GamepadAxis.LeftThumbY)) _output[PS4_LY] = (sbyte)Math.Clamp(state.Gamepad.LeftThumbY * 100 / 32767, -100, 100);
                if (!_pausedAxes.Contains(GamepadAxis.RightThumbX)) _output[PS4_RX] = (sbyte)Math.Clamp(state.Gamepad.RightThumbX * 100 / 32767, -100, 100);
                if (!_pausedAxes.Contains(GamepadAxis.RightThumbY)) _output[PS4_RY] = (sbyte)Math.Clamp(state.Gamepad.RightThumbY * 100 / 32767, -100, 100);
            }

            Flush();
            Thread.Sleep(5); // GCAPI wants the device fed steadily
        }
    }

    public void Dispose()
    {
        StopSync();
        if (_connected) { try { gcapi_Unload(); } catch { /* already gone */ } }
    }
}
