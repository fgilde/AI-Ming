using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using OpenCvSharp;
using PowerAim.Config;
using Tesseract;
using Size = OpenCvSharp.Size;

namespace PowerAim.AILogic;

/// <summary>
///     Periodic OCR sampler. Reads the user-defined regions in
///     <see cref="OcrSettings.Regions"/>, binarizes each one with the user-chosen threshold, and
///     pushes the recognized strings into the <see cref="Latest"/> dictionary.
///     <para>
///     The Tesseract engine is lazy-loaded; the first call after the data path is set up will pay
///     the model-load cost. The class is <i>not</i> a singleton — <see cref="Instance"/> exposes
///     the shared instance that <c>AIManager</c> starts.
///     </para>
/// </summary>
public sealed class OcrService : INotifyPropertyChanged, IDisposable
{
    private static readonly Lazy<OcrService> _lazy = new(() => new OcrService());
    public static OcrService Instance => _lazy.Value;

    private TesseractEngine? _engine;
    private string? _engineDataPath;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private readonly ConcurrentDictionary<string, OcrResult> _latest = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Most recent recognized values, keyed by region name.</summary>
    public IReadOnlyDictionary<string, OcrResult> Latest => _latest;

    /// <summary>Last error message from the engine (empty when OK).</summary>
    public string LastError
    {
        get;
        private set { field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastError))); }
    } = "";

    /// <summary>
    ///     Default location PowerAim looks for <c>eng.traineddata</c>. Falls back to the
    ///     <see cref="OcrSettings.TessdataPath"/> override when set.
    /// </summary>
    public static string DefaultTessdataPath
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PowerAim", "tessdata");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    /// <summary>Whether eng.traineddata exists at the resolved tessdata location.</summary>
    public static bool HasTraineddata(string? overridePath = null)
    {
        var path = string.IsNullOrEmpty(overridePath) ? DefaultTessdataPath : overridePath!;
        return File.Exists(Path.Combine(path, "eng.traineddata"));
    }

    /// <summary>Where exactly we would expect eng.traineddata to live for the active config.</summary>
    public static string ResolveTessdataPath()
    {
        var ovrd = AppConfig.Current?.OcrSettings?.TessdataPath;
        return string.IsNullOrEmpty(ovrd) ? DefaultTessdataPath : ovrd!;
    }

    /// <summary>
    ///     Auto-download eng.traineddata from the upstream tessdata_best repo into
    ///     <see cref="ResolveTessdataPath"/>. Idempotent — does nothing if the file already exists.
    /// </summary>
    public static async Task<bool> EnsureTraineddataAsync(IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var folder = ResolveTessdataPath();
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, "eng.traineddata");
        if (File.Exists(target)) return true;

        const string url = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata";
        using var http = new HttpClient();
        try
        {
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? 0L;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(target);
            var buffer = new byte[81920];
            long copied = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                copied += read;
                if (total > 0) progress?.Report((double)copied / total);
            }
            return File.Exists(target);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Start (or restart) the background polling loop.</summary>
    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => Loop(_cts.Token));
    }

    /// <summary>Stop polling and release the underlying Tesseract engine.</summary>
    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
        try { _loopTask?.Wait(2000); } catch { }
        _loopTask = null;
        _engine?.Dispose();
        _engine = null;
        _engineDataPath = null;
    }

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var settings = AppConfig.Current?.OcrSettings;
            if (settings == null || !settings.Enabled || settings.Regions == null || settings.Regions.Count == 0)
            {
                await Task.Delay(500, ct).ContinueWith(_ => { });
                continue;
            }

            try
            {
                EnsureEngine(settings);
                if (_engine == null)
                {
                    await Task.Delay(800, ct).ContinueWith(_ => { });
                    continue;
                }

                foreach (var region in settings.Regions.ToArray())
                {
                    if (ct.IsCancellationRequested) break;
                    if (region == null || !region.Enabled) continue;
                    var value = SampleRegion(region);
                    if (value != null) _latest[region.Name] = value;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }

            int interval = Math.Clamp(settings.IntervalMs, 100, 5000);
            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void EnsureEngine(OcrSettings settings)
    {
        var dataPath = string.IsNullOrEmpty(settings.TessdataPath) ? DefaultTessdataPath : settings.TessdataPath;
        if (_engine != null && _engineDataPath == dataPath) return;

        var trainedFile = Path.Combine(dataPath, "eng.traineddata");
        if (!File.Exists(trainedFile))
        {
            LastError = $"eng.traineddata not found in {dataPath}. Use the 'Download Tesseract data' button.";
            _engine?.Dispose();
            _engine = null;
            return;
        }

        try
        {
            _engine?.Dispose();
            _engine = new TesseractEngine(dataPath, "eng", EngineMode.LstmOnly);
            _engineDataPath = dataPath;
            LastError = "";
        }
        catch (DllNotFoundException ex)
        {
            // The native libleptonica/libtesseract DLLs from the Tesseract NuGet weren't loaded
            // by the runtime. Most common cause on .NET 10 is that runtimes/win-x64/native isn't
            // being probed (RID mismatch or single-file publish with native-libs not extracted).
            LastError = $"Tesseract native DLL missing ({ex.Message}). Check that runtimes\\win-x64\\native\\leptonica-*.dll exists next to the exe.";
            _engine = null;
        }
        catch (TypeInitializationException ex)
        {
            LastError = $"Tesseract type-init failed: {ex.InnerException?.Message ?? ex.Message}";
            _engine = null;
        }
        catch (Exception ex)
        {
            LastError = $"Tesseract init failed: {ex.Message}";
            _engine = null;
        }
    }

    /// <summary>
    ///     Synchronous one-shot engine probe used by the UI's "Test engine" button. Forces an
    ///     <see cref="EnsureEngine"/> pass and returns a human-readable status string. Safe to
    ///     call repeatedly; the engine instance is cached.
    /// </summary>
    public string TestEngine()
    {
        var settings = AppConfig.Current?.OcrSettings;
        if (settings == null) return "OcrSettings missing.";
        EnsureEngine(settings);
        if (_engine == null) return string.IsNullOrEmpty(LastError)
            ? "Engine init returned null without an error."
            : LastError;
        return $"Engine OK. Data path: {_engineDataPath}.";
    }

    private OcrResult? SampleRegion(OcrRegion region)
    {
        var rect = new System.Drawing.Rectangle(region.X, region.Y, region.Width, region.Height);
        Bitmap? bmp = CaptureScreen(rect);
        if (bmp == null) return null;

        try
        {
            using var mat = BitmapToMat(bmp);
            using var gray = new Mat();
            // Bitmap was captured as 32bppArgb → 4-channel BGRA.
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGRA2GRAY);
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, region.Threshold, 255,
                region.Invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary);

            // Upscale 2x so small HUD digits OCR more reliably.
            using var scaled = new Mat();
            Cv2.Resize(binary, scaled, new Size(binary.Cols * 2, binary.Rows * 2), 0, 0, InterpolationFlags.Cubic);

            using var ms = new MemoryStream();
            Cv2.ImEncode(".png", scaled, out var pngBytes);
            using var pix = Pix.LoadFromMemory(pngBytes);

            using var page = _engine!.Process(pix,
                region.Kind == OcrRegionKind.Text ? PageSegMode.Auto : PageSegMode.SingleLine);
            string raw = page.GetText()?.Trim() ?? "";
            string sanitized = SanitizeForKind(raw, region.Kind);
            double? number = TryExtractNumber(sanitized, region.Kind);
            float conf = page.GetMeanConfidence();
            return new OcrResult
            {
                RegionName = region.Name,
                Raw = raw,
                Text = sanitized,
                Number = number,
                Confidence = conf,
                Timestamp = DateTime.UtcNow
            };
        }
        finally
        {
            bmp.Dispose();
        }
    }

    private static string SanitizeForKind(string raw, OcrRegionKind kind)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        raw = raw.Replace("\n", " ").Replace("\r", " ").Trim();
        switch (kind)
        {
            case OcrRegionKind.Number:
                return new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());
            case OcrRegionKind.Health:
                return new string(raw.Where(c => char.IsDigit(c) || c == '/' || c == '.').ToArray());
            default:
                return raw;
        }
    }

    private static double? TryExtractNumber(string sanitized, OcrRegionKind kind)
    {
        if (string.IsNullOrEmpty(sanitized)) return null;
        var s = sanitized;
        if (kind == OcrRegionKind.Health)
        {
            int slash = s.IndexOf('/');
            if (slash > 0) s = s.Substring(0, slash);
        }
        return double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double n) ? n : null;
    }

    /// <summary>
    ///     GDI screen capture for a specific region. The OCR loop runs on its own thread and uses
    ///     this lightweight path instead of going through the DXGI/ICapture singleton, so it can
    ///     read pixels outside the AI's detection box.
    /// </summary>
    private static Bitmap? CaptureScreen(System.Drawing.Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return null;
        try
        {
            var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height), CopyPixelOperation.SourceCopy);
            return bmp;
        }
        catch { return null; }
    }

    /// <summary>
    ///     OpenCvSharp4 in this project doesn't pull the BitmapConverter extension — we wrap the
    ///     LockBits API ourselves. Same pattern as <see cref="Actions.ImageBasedAntiRecoilAction"/>.
    /// </summary>
    private static Mat BitmapToMat(Bitmap bmp)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var mat = Mat.FromPixelData(bmp.Height, bmp.Width, MatType.CV_8UC4, data.Scan0, data.Stride);
            return mat.Clone();
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    public void Dispose() => Stop();
}

/// <summary>One reading from <see cref="OcrService"/>. Stored per region in <see cref="OcrService.Latest"/>.</summary>
public class OcrResult
{
    public string RegionName { get; init; } = "";
    /// <summary>The raw output from Tesseract before any sanitization.</summary>
    public string Raw { get; init; } = "";
    /// <summary>Sanitized text — for Number/Health this only contains digits + delimiters.</summary>
    public string Text { get; init; } = "";
    /// <summary>Parsed numeric value for Number/Health kinds (current value before the slash).</summary>
    public double? Number { get; init; }
    /// <summary>Tesseract's mean confidence (0..1). Useful for filtering noisy reads.</summary>
    public float Confidence { get; init; }
    public DateTime Timestamp { get; init; }
}
