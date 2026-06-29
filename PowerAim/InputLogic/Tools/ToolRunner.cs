using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PowerAim.Config;

namespace PowerAim.InputLogic.Tools;

/// <summary>
///     Runs a <see cref="CustomTool"/>'s action sequence once, on a background thread. Resolves the
///     tool's <see cref="ToolOption"/>s into <c>{token}</c> substitutions, then dispatches each
///     <see cref="ToolAction"/> to the matching input/exe primitive. Honours cancellation between (and
///     during delays of) actions so a re-press can cancel-and-restart a running tool. Any button/key a
///     run latched with a standalone Down is force-released in a finally, so cancelling mid-hold can't
///     leave the mouse button stuck (desktop drag-lock) or a key held.
/// </summary>
public static class ToolRunner
{
    public static async Task RunAsync(CustomTool tool, CancellationToken ct)
    {
        var vars = BuildVars(tool);
        var held = new HeldInput();
        try
        {
            foreach (var action in tool.Actions.ToArray())
            {
                ct.ThrowIfCancellationRequested();
                try { await ExecuteAsync(action, vars, ct, held); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Console.WriteLine($"[ToolRunner] {action.GetType().Name} failed: {ex.Message}"); }
            }
        }
        finally
        {
            // Release anything still held — on cancel (re-press / stop) or a sequence that ended on a Down.
            await held.ReleaseAllAsync();
        }
    }

    private static Dictionary<string, string> BuildVars(CustomTool tool)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in tool.Options.ToArray())   // snapshot: Options is a UI-thread-owned collection
            if (!string.IsNullOrWhiteSpace(o.Name)) d[o.Name] = o.EffectiveValue;
        return d;
    }

    /// <summary>Replace <c>{Token}</c> with the option's value; unknown tokens are left literal.</summary>
    private static string Subst(string? template, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template)) return template ?? "";
        return Regex.Replace(template, @"\{(\w+)\}",
            m => vars.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }

    private static async Task ExecuteAsync(ToolAction action, Dictionary<string, string> vars, CancellationToken ct, HeldInput held)
    {
        switch (action)
        {
            case DelayAction d:
                if (int.TryParse(Subst(d.Milliseconds, vars), out var ms) && ms > 0)
                    await Task.Delay(ms, ct);
                break;

            case MoveMouseAction mv:
                int.TryParse(Subst(mv.X, vars), out var x);
                int.TryParse(Subst(mv.Y, vars), out var y);
                if (mv.Relative) InputSender.Move(x, y);   // relative delta through the active backend
                else SetCursorPos(x, y);                   // absolute screen pixels
                break;

            case ClickAction c:
                await ClickAsync(c);
                held.MarkMouse(c.Button, c.Mode);
                break;

            case SendKeysAction sk:
            {
                // Mirror ActionTrigger.SendActionsAsync: Simultaneous = all keys at once (each still
                // honours its own recorded MinTime delay), Sequential = one after another.
                var keys = sk.Keys.ToArray().Where(k => k is { IsValid: true }).ToArray();
                if (sk.ExecutionMode == TriggerExecutionMode.Simultaneous)
                {
                    await Task.WhenAll(keys.Select(async key =>
                    {
                        if (key.MinTime > 0) await Task.Delay(TimeSpan.FromSeconds(key.MinTime), ct);
                        await InputSender.SendKeyAsync(key, sk.Mode);
                    }));
                }
                else
                {
                    foreach (var key in keys)
                    {
                        if (key.MinTime > 0) await Task.Delay(TimeSpan.FromSeconds(key.MinTime), ct);
                        await InputSender.SendKeyAsync(key, sk.Mode);
                    }
                }
                foreach (var key in keys) held.MarkKey(key, sk.Mode);
                break;
            }

            case RunExeAction r:
                await RunExeAsync(Subst(r.Path, vars), Subst(r.Args, vars), r.AsAdmin, r.WaitForExit, ct);
                break;
        }
    }

    private static async Task ClickAsync(ClickAction c)
    {
        if (c.Button == ToolMouseButton.Left)
        {
            switch (c.Mode)
            {
                case KeyPressState.Down: MouseManager.LeftDown(); break;
                case KeyPressState.Up: MouseManager.LeftUp(); break;
                default: await MouseManager.DoTriggerClick(); break;
            }
            return;
        }

        // Right / middle have no backend-routed primitive — use raw SendInput.
        (uint down, uint up) = c.Button == ToolMouseButton.Right
            ? (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP)
            : (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP);
        if (c.Mode != KeyPressState.Up) mouse_event(down, 0, 0, 0, UIntPtr.Zero);
        if (c.Mode == KeyPressState.DownAndUp) await Task.Delay(15);
        if (c.Mode != KeyPressState.Down) mouse_event(up, 0, 0, 0, UIntPtr.Zero);
    }

    private static async Task RunExeAsync(string path, string args, bool asAdmin, bool wait, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var psi = new ProcessStartInfo { FileName = path, Arguments = args ?? "", UseShellExecute = true };
        if (asAdmin) psi.Verb = "runas";
        var p = Process.Start(psi);
        if (wait && p != null) await p.WaitForExitAsync(ct);
    }

    /// <summary>Tracks input a run latched with a standalone Down so the finally can release it.</summary>
    private sealed class HeldInput
    {
        private bool _left, _right, _middle;
        private readonly List<StoredInputBinding> _keys = new();

        public void MarkMouse(ToolMouseButton b, KeyPressState mode)
        {
            if (mode == KeyPressState.DownAndUp) return;   // nothing left held
            var down = mode == KeyPressState.Down;
            switch (b)
            {
                case ToolMouseButton.Left: _left = down; break;
                case ToolMouseButton.Right: _right = down; break;
                case ToolMouseButton.Middle: _middle = down; break;
            }
        }

        public void MarkKey(StoredInputBinding key, KeyPressState mode)
        {
            if (mode == KeyPressState.Down) _keys.Add(key);
            else if (mode == KeyPressState.Up) _keys.RemoveAll(k => ReferenceEquals(k, key));
            // DownAndUp leaves nothing held.
        }

        public async Task ReleaseAllAsync()
        {
            try
            {
                if (_left) MouseManager.LeftUp();
                if (_right) mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                if (_middle) mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);
                foreach (var k in _keys.ToArray())
                    if (k is { IsValid: true })
                        await InputSender.SendKeyAsync(k, KeyPressState.Up);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ToolRunner] release-held failed: {ex.Message}");
            }
        }
    }

    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010,
                       MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040;
}
