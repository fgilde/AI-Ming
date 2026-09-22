using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Core.Jev;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>Where a Jev setup run currently is. The UI localizes these; this layer stays language-free.</summary>
public enum JevSetupPhase { CheckingPython, Downloading, Extracting, CreatingEnvironment, InstallingTorch, InstallingServer, Starting, Done, Failed }

/// <summary>A setup progress tick: phase, 0..1 fraction (-1 = indeterminate), and the latest tool output line.</summary>
public readonly record struct JevSetupProgress(JevSetupPhase Phase, double Fraction, string? Detail);

/// <summary>
///     One-click local Jev: makes sure a Python exists, fetches <c>simple-jev</c>, builds a virtual
///     environment, installs PyTorch + the hf-server, and starts it — then the app talks to it over the
///     normal <see cref="JevDecisionClient"/> HTTP contract.
///     <para>
///     Deliberately git-free: the repo is pulled as a GitHub source zip, so the user needs nothing but a
///     Python interpreter. Everything lands under <see cref="InstallDir"/>; deleting that folder fully
///     undoes the setup.
///     </para>
/// </summary>
public static class JevServerSetup
{
    private const string SourceZipUrl = "https://github.com/featherless-ai/simple-jev/archive/refs/heads/main.zip";

    /// <summary>Python download page, for the manual fallback when no interpreter is found.</summary>
    public const string PythonDownloadUrl = "https://www.python.org/downloads/windows/";

    /// <summary>Everything we install lives here — one folder to delete for a clean slate.</summary>
    public static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerAim", "simple-jev");

    private static string RepoDir => Path.Combine(InstallDir, "repo");
    private static string VenvDir => Path.Combine(InstallDir, "venv");
    private static string VenvPython => Path.Combine(VenvDir, "Scripts", "python.exe");
    private static string ServerScript => Path.Combine(RepoDir, "hf-server", "hf_server.py");

    /// <summary>True when the source tree is present.</summary>
    public static bool IsDownloaded => File.Exists(ServerScript);

    /// <summary>True when the environment exists and the server can be launched from it.</summary>
    public static bool IsInstalled => IsDownloaded && File.Exists(VenvPython) && File.Exists(MarkerFile);

    private static string MarkerFile => Path.Combine(InstallDir, ".installed");

    // The server we spawned, so the UI can stop it again. A server started outside PowerAim is not tracked
    // (and not killed) — we only own what we launched.
    private static Process? _server;

    /// <summary>True when THIS app started the server and it is still alive.</summary>
    public static bool IsServerRunning => _server is { HasExited: false };

    // =============================================================== Python discovery ====

    /// <summary>
    ///     Find a usable interpreter: the <c>py</c> launcher first (it resolves the newest install), then
    ///     <c>python</c> from PATH. Returns the command plus the leading args needed to invoke it.
    /// </summary>
    public static (string Exe, string[] Args, Version Version)? FindPython()
    {
        foreach (var (exe, args) in new[] { ("py", new[] { "-3" }), ("python", Array.Empty<string>()) })
        {
            var output = RunQuick(exe, [.. args, "--version"]);
            var version = PythonVersion.Parse(output);
            if (PythonVersion.IsSupported(version)) return (exe, args, version!);
        }
        return null;
    }

    private static string? RunQuick(string exe, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) return null;
            // Older pythons print the banner on stderr — read both.
            string outText = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(5000);
            return outText;
        }
        catch { return null; }
    }

    // =============================================================== Setup ====

    /// <summary>
    ///     Full setup, skipping whatever is already done. Throws with a user-facing message on failure;
    ///     the caller shows it. Safe to re-run — each step is idempotent.
    /// </summary>
    public static async Task SetUpAsync(bool useCuda, IProgress<JevSetupProgress>? progress, CancellationToken ct = default)
    {
        progress?.Report(new(JevSetupPhase.CheckingPython, -1, null));
        var python = FindPython()
            ?? throw new InvalidOperationException(
                $"No Python {PythonVersion.Minimum} or newer found. Install Python and re-run the setup.");
        progress?.Report(new(JevSetupPhase.CheckingPython, 1, $"Python {python.Version}"));

        Directory.CreateDirectory(InstallDir);

        if (!IsDownloaded)
        {
            var zip = Path.Combine(InstallDir, "simple-jev.zip");
            await DownloadAsync(SourceZipUrl, zip, progress, ct);

            progress?.Report(new(JevSetupPhase.Extracting, -1, null));
            await Task.Run(() => ExtractRepo(zip, RepoDir), ct);
            try { File.Delete(zip); } catch { /* leftover zip is harmless */ }

            if (!IsDownloaded)
                throw new InvalidOperationException("simple-jev downloaded but hf-server/hf_server.py is missing — the repository layout changed.");
        }

        if (!File.Exists(VenvPython))
        {
            progress?.Report(new(JevSetupPhase.CreatingEnvironment, -1, null));
            await RunAsync(python.Exe, [.. python.Args, "-m", "venv", VenvDir], InstallDir, progress, JevSetupPhase.CreatingEnvironment, ct);
            if (!File.Exists(VenvPython))
                throw new InvalidOperationException("Creating the Python virtual environment failed — see the log above.");
        }

        // PyTorch first and explicitly: the default index gives a CPU-only wheel, which would silently make
        // every decision 10x slower on a machine that has a perfectly good GPU.
        progress?.Report(new(JevSetupPhase.InstallingTorch, -1, null));
        string[] torchArgs = useCuda
            ? ["-m", "pip", "install", "torch", "--index-url", "https://download.pytorch.org/whl/cu124"]
            : ["-m", "pip", "install", "torch"];
        await RunAsync(VenvPython, torchArgs, InstallDir, progress, JevSetupPhase.InstallingTorch, ct);

        progress?.Report(new(JevSetupPhase.InstallingServer, -1, null));
        await RunAsync(VenvPython, ["-m", "pip", "install", "-e", Path.Combine(RepoDir, "hf-server")], InstallDir, progress, JevSetupPhase.InstallingServer, ct);

        await File.WriteAllTextAsync(MarkerFile, DateTime.UtcNow.ToString("o"), ct);
        progress?.Report(new(JevSetupPhase.Done, 1, null));
    }

    /// <summary>
    ///     Start the local server (no-op if we already run one) and wait until <c>/health</c> answers.
    ///     The model is downloaded from Hugging Face on first start, so the first launch can take a while.
    /// </summary>
    public static async Task<bool> StartServerAsync(IProgress<JevSetupProgress>? progress, CancellationToken ct = default)
    {
        if (IsServerRunning) return true;
        if (!IsInstalled) throw new InvalidOperationException("Jev server is not set up yet — run the setup first.");

        var s = AppConfig.Current?.JevSettings ?? new JevSettings();
        var uri = new Uri(s.BaseUrl);

        progress?.Report(new(JevSetupPhase.Starting, -1, null));
        var psi = new ProcessStartInfo(VenvPython)
        {
            WorkingDirectory = RepoDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in new[]
                 {
                     ServerScript,
                     "--model", s.Model,
                     "--device", s.Device,
                     "--port", uri.Port.ToString(),
                 })
            psi.ArgumentList.Add(a);

        _server = Process.Start(psi);
        if (_server == null) return false;

        // Surface the server's own output (model download progress, load errors) into the app log.
        _server.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine($"[jev] {e.Data}"); };
        _server.ErrorDataReceived  += (_, e) => { if (e.Data != null) Console.WriteLine($"[jev] {e.Data}"); };
        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();

        // First start pulls the model — poll generously, but bail out early if the process dies.
        var client = new JevDecisionClient();
        for (int i = 0; i < 600 && !ct.IsCancellationRequested; i++)
        {
            if (_server.HasExited)
                throw new InvalidOperationException($"The Jev server exited during startup (code {_server.ExitCode}) — see the log.");
            if (await client.IsAvailableAsync(ct))
            {
                progress?.Report(new(JevSetupPhase.Done, 1, null));
                return true;
            }
            progress?.Report(new(JevSetupPhase.Starting, -1, $"waiting for {s.BaseUrl} ({i}s)"));
            await Task.Delay(1000, ct);
        }
        return false;
    }

    /// <summary>Stop the server we started. Servers started outside PowerAim are left alone.</summary>
    public static void StopServer()
    {
        try
        {
            if (_server is { HasExited: false }) _server.Kill(entireProcessTree: true);
        }
        catch { /* already gone */ }
        finally { _server = null; }
    }

    // =============================================================== Helpers ====

    private static async Task DownloadAsync(string url, string dest, IProgress<JevSetupProgress>? progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        long total = resp.Content.Headers.ContentLength ?? -1;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var fs = File.Create(dest);
        var buffer = new byte[1 << 18];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            progress?.Report(new(JevSetupPhase.Downloading, total > 0 ? (double)read / total : -1, null));
        }
    }

    /// <summary>Extract the GitHub source zip, stripping its single top-level "simple-jev-main/" folder.</summary>
    private static void ExtractRepo(string zipPath, string destDir)
    {
        if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
        Directory.CreateDirectory(destDir);

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            // "simple-jev-main/hf-server/hf_server.py" → "hf-server/hf_server.py"
            int slash = entry.FullName.IndexOf('/');
            if (slash < 0) continue;
            var relative = entry.FullName[(slash + 1)..];
            if (string.IsNullOrEmpty(relative)) continue;

            var target = Path.GetFullPath(Path.Combine(destDir, relative));
            // Zip-slip guard: never let an entry escape the destination folder.
            if (!target.StartsWith(Path.GetFullPath(destDir), StringComparison.OrdinalIgnoreCase)) continue;

            if (relative.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    /// <summary>Run a console tool, streaming its output into <paramref name="progress"/>, and throw on a non-zero exit.</summary>
    private static async Task RunAsync(string exe, string[] args, string workingDir,
        IProgress<JevSetupProgress>? progress, JevSetupPhase phase, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {exe}.");
        var tail = new Queue<string>();
        void Line(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            Console.WriteLine($"[jev-setup] {line}");
            progress?.Report(new(phase, -1, line));
            tail.Enqueue(line);
            while (tail.Count > 5) tail.Dequeue();
        }
        p.OutputDataReceived += (_, e) => Line(e.Data);
        p.ErrorDataReceived += (_, e) => Line(e.Data);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(exe)} {string.Join(' ', args.Take(3))} failed (exit {p.ExitCode}): {string.Join(" | ", tail)}");
    }
}
