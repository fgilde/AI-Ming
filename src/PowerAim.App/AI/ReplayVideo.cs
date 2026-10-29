using System.Diagnostics;
using System.IO;

namespace PowerAim.AI;

/// <summary>Turns an exported replay (frame_NNNN.jpg) into an MP4 with ffmpeg, when ffmpeg is on the PATH.</summary>
public static class ReplayVideo
{
    private static readonly Lazy<bool> Available = new(() => Run("-version", Path.GetTempPath(), 10_000));

    public static bool EncoderAvailable() => Available.Value;

    /// <summary>Capture rate of the exported frames; 30 when the span is unknown.</summary>
    public static double FrameRate(int frames, double spanSeconds)
        => frames > 1 && spanSeconds > 0 ? Math.Clamp((frames - 1) / spanSeconds, 1, 240) : 30;

    /// <summary>
    ///     Encodes the folder's frames to <c>replay.mp4</c> at <paramref name="width"/>×<paramref name="height"/>
    ///     (frames of another size — the FOV changed — are scaled); its path, or null without ffmpeg or on failure.
    /// </summary>
    public static string? TryEncode(string folder, double framesPerSecond, int width, int height)
    {
        if (!EncoderAvailable()) return null;
        var output = Path.Combine(folder, "replay.mp4");
        string fps = framesPerSecond.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        // One fixed, even size for yuv420p.
        int w = Math.Max(2, width / 2 * 2), h = Math.Max(2, height / 2 * 2);
        string args = $"-y -loglevel error -framerate {fps} -i frame_%04d.jpg -vf scale={w}:{h} " +
                      "-c:v libx264 -pix_fmt yuv420p replay.mp4";
        return Run(args, folder, 120_000) && File.Exists(output) ? output : null;
    }

    private static bool Run(string arguments, string workingDirectory, int timeoutMs)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ffmpeg", arguments)
            {
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            });
            if (process == null) return false;
            var stderr = process.StandardError.ReadToEndAsync();
            _ = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs)) { process.Kill(); return false; }
            return process.ExitCode == 0;
        }
        catch
        {
            return false; // not installed / not on the PATH
        }
    }
}
