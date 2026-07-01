using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using PowerAim.AILogic;
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
                // Refresh the live target vars so each action sees the current best detection.
                InjectPredictionVars(vars);
                // Per-action guard ("Run only if …"): Op == Always means no guard, so it runs.
                if (!EvaluateCondition(action.Condition, vars)) continue;
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

    /// <summary>
    ///     Replace <c>{Token}</c> with a variable's value; unknown tokens are left literal. The token
    ///     name may contain dots, so dotted runtime vars like <c>{target.x}</c> / <c>{result.status}</c>
    ///     substitute too.
    /// </summary>
    private static string Subst(string? template, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template)) return template ?? "";
        return Regex.Replace(template, @"\{([\w.]+)\}",
            m => vars.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }

    /// <summary>
    ///     Push the latest / best detection into the variable bag so actions can read the target.
    ///     <c>screenX/Y</c> are absolute screen pixels (the centre of the engine's own
    ///     <see cref="Prediction.TranslatedRectangle"/>), and <c>dx/dy</c> are that point relative to the
    ///     current cursor — both rounded to whole pixels so they drop straight into Move-mouse fields.
    /// </summary>
    private static void InjectPredictionVars(Dictionary<string, string> vars)
    {
        var best = DetectionState.Best;
        var ci = CultureInfo.InvariantCulture;
        vars["target.found"] = best != null ? "true" : "false";
        vars["target.confidence"] = best?.Confidence.ToString(ci) ?? "0";
        vars["target.class"] = best?.ClassName ?? "";
        vars["target.x"] = best?.CenterXTranslated.ToString(ci) ?? "";
        vars["target.y"] = best?.CenterYTranslated.ToString(ci) ?? "";
        vars["targets.count"] = DetectionState.Latest.Length.ToString(ci);

        if (best != null)
        {
            // TranslatedRectangle is already in absolute screen pixels (PredictionLogic maps
            // model-space → capture pixels → screen). Its centre is the on-screen target point.
            var r = best.TranslatedRectangle;
            var screenX = (int)Math.Round(r.Left + r.Width / 2f);
            var screenY = (int)Math.Round(r.Top + r.Height / 2f);
            var cursor = PowerAim.Class.Native.NativeAPIMethods.GetCursorPosition();
            vars["target.screenX"] = screenX.ToString(ci);
            vars["target.screenY"] = screenY.ToString(ci);
            vars["target.dx"] = (screenX - cursor.X).ToString(ci);
            vars["target.dy"] = (screenY - cursor.Y).ToString(ci);
        }
        else
        {
            vars["target.screenX"] = "";
            vars["target.screenY"] = "";
            vars["target.dx"] = "0";
            vars["target.dy"] = "0";
        }
    }

    /// <summary>Evaluate an action's optional guard. Both sides go through <see cref="Subst"/> first.</summary>
    private static bool EvaluateCondition(ToolCondition? cond, Dictionary<string, string> vars)
    {
        if (cond == null || cond.Op == ConditionOp.Always) return true;
        var left = Subst(cond.Left, vars).Trim();
        var right = Subst(cond.Right, vars).Trim();
        return cond.Op switch
        {
            ConditionOp.Equals => NumOrTextEqual(left, right),
            ConditionOp.NotEquals => !NumOrTextEqual(left, right),
            ConditionOp.Greater => TryNum(left, out var lg) && TryNum(right, out var rg) && lg > rg,
            ConditionOp.Less => TryNum(left, out var ll) && TryNum(right, out var rl) && ll < rl,
            ConditionOp.Contains => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            ConditionOp.IsTrue => IsTruthy(left),
            ConditionOp.IsFalse => !IsTruthy(left),
            _ => true,
        };
    }

    private static bool NumOrTextEqual(string a, string b)
        => TryNum(a, out var na) && TryNum(b, out var nb)
            ? Math.Abs(na - nb) < 1e-9
            : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool TryNum(string s, out double v)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);

    private static bool IsTruthy(string s)
    {
        s = s.Trim();
        return s.Equals("true", StringComparison.OrdinalIgnoreCase)
               || s == "1"
               || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || s.Equals("on", StringComparison.OrdinalIgnoreCase);
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

            case SetVarAction sv:
                if (!string.IsNullOrWhiteSpace(sv.Name))
                    vars[sv.Name.Trim()] = Subst(sv.Value, vars);
                break;

            case HttpRequestAction h:
                await HttpAsync(h, vars, ct);
                break;
        }
    }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    ///     Send the request, wait for the response, and (if <see cref="HttpRequestAction.StoreVar"/> is set)
    ///     store the body plus <c>{var}.status</c> / <c>{var}.ok</c> so later actions can branch on it.
    /// </summary>
    private static async Task HttpAsync(HttpRequestAction h, Dictionary<string, string> vars, CancellationToken ct)
    {
        var url = Subst(h.Url, vars);
        if (string.IsNullOrWhiteSpace(url)) return;

        var method = h.Method switch
        {
            HttpMethodKind.Post => HttpMethod.Post,
            HttpMethodKind.Put => HttpMethod.Put,
            HttpMethodKind.Delete => HttpMethod.Delete,
            _ => HttpMethod.Get,
        };

        using var req = new HttpRequestMessage(method, url);
        if (h.Method is HttpMethodKind.Post or HttpMethodKind.Put && !string.IsNullOrEmpty(h.Body))
            req.Content = new StringContent(Subst(h.Body, vars), Encoding.UTF8, "application/json");

        // Optional headers: one "Key: Value" per line. Content-* headers belong on the content object.
        foreach (var line in Subst(h.Headers, vars).Split('\n'))
        {
            var t = line.Trim();
            var colon = t.IndexOf(':');
            if (colon <= 0) continue;
            var name = t[..colon].Trim();
            var value = t[(colon + 1)..].Trim();
            if (name.Length == 0) continue;
            if (req.Content != null && name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                req.Content.Headers.Remove(name);
                req.Content.Headers.TryAddWithoutValidation(name, value);
            }
            else
            {
                req.Headers.TryAddWithoutValidation(name, value);
            }
        }

        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!string.IsNullOrWhiteSpace(h.StoreVar))
        {
            var key = h.StoreVar.Trim();
            vars[key] = body;
            vars[key + ".status"] = ((int)resp.StatusCode).ToString(CultureInfo.InvariantCulture);
            vars[key + ".ok"] = resp.IsSuccessStatusCode ? "true" : "false";
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
