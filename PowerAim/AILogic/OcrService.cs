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
    private bool _engineDpiHinted;
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
                    var value = SampleRegion(region, settings);
                    if (value == null) continue;

                    // Sticky-last-valid (opt-in via OcrSettings.StickyLastValidMs): if this frame
                    // couldn't extract a Number but the prior reading did AND it's still fresh,
                    // carry the prior numeric value forward. Single bad frames from motion blur /
                    // partial occlusion don't flicker consumers between "value X" and "nothing".
                    // Cannot manufacture wrong values — only holds real prior ones.
                    int stickyMs = settings.StickyLastValidMs;
                    if (stickyMs > 0
                        && (region.Kind == OcrRegionKind.Number || region.Kind == OcrRegionKind.Health)
                        && !value.Number.HasValue
                        && _latest.TryGetValue(region.Name, out var prior)
                        && prior.Number.HasValue
                        && (DateTime.UtcNow - prior.Timestamp).TotalMilliseconds < stickyMs)
                    {
                        value = new OcrResult
                        {
                            RegionName = value.RegionName,
                            Raw = value.Raw,
                            Text = value.Text,
                            Number = prior.Number,
                            Confidence = value.Confidence,
                            Timestamp = value.Timestamp,
                        };
                    }

                    _latest[region.Name] = value;
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
        // Re-init when the data path OR the DPI-hint flag changes — the variable is sticky on the
        // engine instance, so flipping the toggle at runtime needs a fresh engine to take effect.
        if (_engine != null && _engineDataPath == dataPath && _engineDpiHinted == settings.UseUserDefinedDpi) return;

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
            if (settings.UseUserDefinedDpi)
            {
                // 300 is the documented LSTM sweet spot. Tells Tesseract not to auto-rescale.
                _engine.SetVariable("user_defined_dpi", "300");
            }
            _engineDataPath = dataPath;
            _engineDpiHinted = settings.UseUserDefinedDpi;
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

    private OcrResult? SampleRegion(OcrRegion region, OcrSettings settings)
    {
        var rect = new System.Drawing.Rectangle(region.X, region.Y, region.Width, region.Height);
        Bitmap? bmp = CaptureScreen(rect);
        if (bmp == null) return null;

        try
        {
            using var mat = BitmapToMat(bmp);
            using var gray = new Mat();
            if (settings.UseMaxChannelGrayscale)
            {
                // Max-of-channels grayscale (= HSV V channel). For saturated coloured HUD digits
                // (e.g. magenta ammo counters) standard luminance Y collapses to a mid-grey that
                // falls below user thresholds tuned for white text. V keeps any saturated colour
                // at 255 so one threshold works across white / pink / red / green. For unbunten
                // text (R=G=B) V == Y bit-identically — no regression.
                var channels = Cv2.Split(mat);
                try
                {
                    Cv2.Max(channels[0], channels[1], gray);   // gray = max(B, G)
                    Cv2.Max(gray, channels[2], gray);          // gray = max(gray, R)
                }
                finally
                {
                    for (int i = 0; i < channels.Length; i++) channels[i].Dispose();
                }
            }
            else
            {
                // Bitmap was captured as 32bppArgb → 4-channel BGRA. Standard luminance Y.
                Cv2.CvtColor(mat, gray, ColorConversionCodes.BGRA2GRAY);
            }

            // Primary pass with the user-configured fixed threshold.
            var primary = RunOcr(region, gray, useOtsu: false);

            // Otsu fallback (opt-in, off by default). Re-binarises with Tesseract's auto-picked
            // threshold when the primary pass returned no number. Known risk: on noisy / colour-
            // saturated regions Otsu can manufacture spurious multi-digit reads from speckle.
            // Only enable when the primary threshold is *just barely* missing and a re-threshold
            // would reliably recover.
            if (settings.UseOtsuFallback
                && (region.Kind == OcrRegionKind.Number || region.Kind == OcrRegionKind.Health)
                && primary != null
                && !primary.Number.HasValue)
            {
                var otsu = RunOcr(region, gray, useOtsu: true);
                if (otsu != null && otsu.Number.HasValue) return otsu;
            }

            return primary;
        }
        finally
        {
            bmp.Dispose();
        }
    }

    /// <summary>
    ///     Single OCR pass over <paramref name="gray"/>: threshold → 2× upscale → BMP-encode →
    ///     Tesseract. <paramref name="useOtsu"/> swaps the user-configured fixed threshold for
    ///     Otsu's automatic one (Otsu fallback path).
    /// </summary>
    private OcrResult? RunOcr(OcrRegion region, Mat gray, bool useOtsu)
    {
        var settings = AppConfig.Current?.OcrSettings;

        using var binary = new Mat();
        if (useOtsu)
        {
            var flags = (region.Invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) | ThresholdTypes.Otsu;
            Cv2.Threshold(gray, binary, 0, 255, flags);
        }
        else
        {
            Cv2.Threshold(gray, binary, region.Threshold, 255,
                region.Invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary);
        }

        // Upscale 2x so small HUD digits OCR more reliably.
        using var scaled = new Mat();
        Cv2.Resize(binary, scaled, new Size(binary.Cols * 2, binary.Rows * 2), 0, 0, InterpolationFlags.Cubic);

        // BMP encode — pixel-identical to PNG but ~10–50× faster to decode in Leptonica because
        // there's no DEFLATE step. Tesseract sees the exact same bitmap content either way.
        Cv2.ImEncode(".bmp", scaled, out var bmpBytes);
        using var pix = Pix.LoadFromMemory(bmpBytes);

        using var page = _engine!.Process(pix,
            region.Kind == OcrRegionKind.Text ? PageSegMode.Auto : PageSegMode.SingleLine);
        string raw = page.GetText()?.Trim() ?? "";
        string sanitized = SanitizeForKind(raw, region.Kind, settings);
        double? number = TryExtractNumber(sanitized, region.Kind, settings);
        float conf = page.GetMeanConfidence();
        return new OcrResult
        {
            RegionName = region.Name,
            Raw = raw,
            Text = sanitized,
            Number = number,
            Confidence = conf,
            Timestamp = DateTime.UtcNow,
        };
    }

    private static string SanitizeForKind(string raw, OcrRegionKind kind, OcrSettings? settings)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        raw = raw.Replace("\n", " ").Replace("\r", " ").Trim();
        // Optional letter↔digit substitution BEFORE digit-only stripping — without it, a near-miss
        // like "lO" loses both characters; with it, "lO" → "10" survives.
        bool substitute = settings?.SubstituteLettersToDigits == true;
        switch (kind)
        {
            case OcrRegionKind.Number:
                if (substitute) raw = SubstituteLettersToDigits(raw);
                return new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());
            case OcrRegionKind.Health:
                if (substitute) raw = SubstituteLettersToDigits(raw);
                return new string(raw.Where(c => char.IsDigit(c) || c == '/' || c == '.').ToArray());
            default:
                return raw;
        }
    }

    /// <summary>
    ///     Map common letter glyphs that Tesseract confuses with digits, character-by-character.
    ///     Only the conservative mappings — the ones that show up in real HUD-font OCR misses
    ///     (LSTM-mode Tesseract on 12-18 px digits). More aggressive mappings (E→3, A→4, T→7)
    ///     would catch a few more cases but at higher risk of false positives, so they're left
    ///     out. Case-sensitive on purpose: <c>o</c> and <c>O</c> both → 0, but <c>b</c> → 6 while
    ///     <c>B</c> → 8.
    /// </summary>
    private static string SubstituteLettersToDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            sb.Append(c switch
            {
                'O' or 'o' or 'Q' or 'D' => '0',
                'l' or 'I' or 'i' or '|' or '!' or ']' => '1',
                'Z' or 'z' => '2',
                'S' or 's' => '5',
                'b' => '6',
                'B' => '8',
                'g' or 'q' => '9',
                _ => c,
            });
        }
        return sb.ToString();
    }

    private static double? TryExtractNumber(string sanitized, OcrRegionKind kind, OcrSettings? settings)
    {
        if (string.IsNullOrEmpty(sanitized)) return null;
        var s = sanitized;
        if (kind == OcrRegionKind.Health)
        {
            int slash = s.IndexOf('/');
            if (slash > 0) s = s.Substring(0, slash);
            else if (slash == 0 && settings?.StrictNumberParsing == true) return null; // "/100" — no current value
        }

        if (settings?.StrictNumberParsing == true)
        {
            // Require at least one digit and trim stray standalone dots. Without this, inputs
            // like "." or ".." (a stray speckle that survived thresholding) parse cleanly as 0
            // and the consumer treats it as a real reading.
            if (!s.Any(char.IsDigit)) return null;
            s = s.Trim('.');
            if (s.Length == 0) return null;
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
