using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Newtonsoft.Json;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
///     In-memory ring buffer of the last few seconds of captured frames + detections. Used by the
///     "save the play I just made" button — flush <see cref="ExportAsync"/> writes the contents to
///     a timestamped folder as a PNG sequence plus a JSON sidecar with the predictions.
///     <para>
///     Frames are JPEG-encoded on insert so the buffer stays compact even at 30+ fps. Tune the
///     quality with <see cref="ReplaySettings.JpegQuality"/>; default 70 yields ~50–200KB per
///     frame at 640×640.
///     </para>
/// </summary>
public sealed class ReplayBuffer : INotifyPropertyChanged
{
    private static readonly Lazy<ReplayBuffer> _lazy = new(() => new ReplayBuffer());
    public static ReplayBuffer Instance => _lazy.Value;

    private readonly LinkedList<ReplayFrame> _frames = new();
    private readonly object _lock = new();
    private int _droppedSinceLastExport;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int FrameCount
    {
        get { lock (_lock) return _frames.Count; }
    }

    public int DroppedSinceLastExport => _droppedSinceLastExport;

    /// <summary>
    ///     Push the latest captured frame + predictions into the ring. Cheap when
    ///     <see cref="ReplaySettings.Enabled"/> is false (returns immediately).
    /// </summary>
    public void Push(Bitmap? frame, Prediction[] predictions)
    {
        var settings = AppConfig.Current?.ReplaySettings;
        if (settings == null || !settings.Enabled || frame == null) return;

        int targetCount = EstimatedCapacity(settings);
        byte[] jpeg;
        try { jpeg = EncodeJpeg(frame, settings.JpegQuality); }
        catch { return; }

        var entry = new ReplayFrame
        {
            CapturedAt = DateTime.UtcNow,
            JpegBytes = jpeg,
            Width = frame.Width,
            Height = frame.Height,
            Predictions = predictions?.Select(p => new ReplayPrediction
            {
                ClassId = p.ClassId,
                ClassName = p.ClassName,
                Confidence = p.Confidence,
                X = p.Rectangle.X,
                Y = p.Rectangle.Y,
                Width = p.Rectangle.Width,
                Height = p.Rectangle.Height,
                CenterXTranslated = p.CenterXTranslated,
                CenterYTranslated = p.CenterYTranslated
            }).ToArray() ?? []
        };

        lock (_lock)
        {
            _frames.AddLast(entry);
            while (_frames.Count > targetCount)
            {
                _frames.RemoveFirst();
                _droppedSinceLastExport++;
            }
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameCount)));
    }

    /// <summary>Flush the current buffer to a timestamped folder under the export root.</summary>
    public Task<string?> ExportAsync()
    {
        return Task.Run<string?>(() =>
        {
            ReplayFrame[] snapshot;
            lock (_lock) snapshot = _frames.ToArray();
            if (snapshot.Length == 0) return null;

            var settings = AppConfig.Current?.ReplaySettings;
            string root = string.IsNullOrEmpty(settings?.ExportFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PowerAim", "replays")
                : settings.ExportFolder;
            Directory.CreateDirectory(root);

            string folder = Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(folder);

            // Write frames + collect metadata.
            var metaFrames = new List<object>(snapshot.Length);
            for (int i = 0; i < snapshot.Length; i++)
            {
                var f = snapshot[i];
                string name = $"frame_{i:D4}.jpg";
                File.WriteAllBytes(Path.Combine(folder, name), f.JpegBytes);
                metaFrames.Add(new
                {
                    file = name,
                    capturedAt = f.CapturedAt,
                    width = f.Width,
                    height = f.Height,
                    predictions = f.Predictions
                });
            }

            var meta = new
            {
                createdAt = DateTime.UtcNow,
                frameCount = snapshot.Length,
                jpegQuality = settings?.JpegQuality ?? 70,
                droppedBeforeExport = _droppedSinceLastExport,
                frames = metaFrames
            };
            File.WriteAllText(Path.Combine(folder, "annotations.json"),
                JsonConvert.SerializeObject(meta, Formatting.Indented));

            _droppedSinceLastExport = 0;
            return folder;
        });
    }

    /// <summary>Drop all buffered frames and reset the dropped-counter.</summary>
    public void Clear()
    {
        lock (_lock) _frames.Clear();
        _droppedSinceLastExport = 0;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameCount)));
    }

    private static int EstimatedCapacity(ReplaySettings settings)
    {
        // Inference loop is uncapped — assume 30 fps and let the user tune via BufferSeconds.
        int est = settings.BufferSeconds * 30;
        return Math.Clamp(est, 10, 1000);
    }

    private static byte[] EncodeJpeg(Bitmap bmp, int quality)
    {
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
        var p = new EncoderParameters(1);
        p.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
        using var ms = new MemoryStream();
        bmp.Save(ms, codec, p);
        return ms.ToArray();
    }
}

/// <summary>One frame in the replay buffer. Serialized into the export JSON via the metadata sidecar.</summary>
public class ReplayFrame
{
    public DateTime CapturedAt { get; init; }
    [JsonIgnore]
    public byte[] JpegBytes { get; init; } = [];
    public int Width { get; init; }
    public int Height { get; init; }
    public ReplayPrediction[] Predictions { get; init; } = [];
}

/// <summary>JSON-friendly projection of <see cref="Prediction"/>.</summary>
public class ReplayPrediction
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public float Confidence { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float CenterXTranslated { get; set; }
    public float CenterYTranslated { get; set; }
}
