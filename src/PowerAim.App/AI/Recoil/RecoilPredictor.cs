using System.Collections.Concurrent;
using System.IO;
using System.Numerics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PowerAim.AI;
using PowerAim.AI.Contracts;

namespace PowerAim.AI.Recoil;

/// <summary>
///     Feed-forward neural recoil predictor. Anticipates the next-frame kick from:
///     burst index, last-4 residual (dx,dy) pairs, and a weapon-class embedding.
///     <para>
///     Preferred path: load a small GRU ONNX model via <see cref="OnnxModelSessionFactory"/>
///     (input shape [1, 4, 3] = seq of [residualX, residualY, burstNorm], optional class id;
///     output [1, 2] = predicted kickX/kickY). If no model is present, a deterministic
///     analytic prior is used so the feature still works out of the box.
///     </para>
/// </summary>
public sealed class RecoilPredictor : IDisposable
{
    public static RecoilPredictor Instance { get; } = new();

    private const int HistoryLen = 4;
    private const string DefaultModelRelative = "models/recoil_predictor.onnx";

    private InferenceSession? _session;
    private SessionOptions? _ownedOptions;
    private string? _inputName;
    private string? _outputName;
    private bool _triedLoad;
    private readonly object _gate = new();

    // Analytic fallback: vertical-dominant, rising then plateau (AK-like prior).
    // Values are "screen residual px per frame" at unit strength.
    private static readonly float[] PriorY =
    {
        0.0f, 3.2f, 4.1f, 4.8f, 5.2f, 5.0f, 4.6f, 4.2f,
        3.8f, 3.5f, 3.2f, 3.0f, 2.8f, 2.6f, 2.5f, 2.4f,
        2.3f, 2.2f, 2.1f, 2.0f, 1.9f, 1.9f, 1.8f, 1.8f,
        1.7f, 1.7f, 1.6f, 1.6f, 1.5f, 1.5f
    };
    private static readonly float[] PriorX =
    {
        0.0f, 0.2f, -0.4f, 0.6f, -0.5f, 0.8f, -0.7f, 0.5f,
        -0.4f, 0.3f, -0.3f, 0.2f, -0.2f, 0.15f, -0.15f, 0.1f,
        -0.1f, 0.08f, -0.08f, 0.05f, -0.05f, 0.04f, -0.04f, 0.03f,
        -0.03f, 0.02f, -0.02f, 0.02f, -0.01f, 0.01f
    };

    private readonly ConcurrentDictionary<string, int> _classIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Try load ONNX model once. Safe to call repeatedly.</summary>
    public void EnsureLoaded()
    {
        if (_triedLoad) return;
        lock (_gate)
        {
            if (_triedLoad) return;
            _triedLoad = true;
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.GetFullPath(Path.Combine(baseDir, DefaultModelRelative));
                if (!File.Exists(path))
                    return; // analytic fallback only

                var result = OnnxModelSessionFactory.Load(path, OnnxExecutionProvider.Cpu);
                _session = result.Session;
                _ownedOptions = result.OwnedSessionOptions;
                _inputName = result.InputName;
                _outputName = result.OutputNames.Count > 0
                    ? result.OutputNames[0]
                    : _session.OutputMetadata.Keys.First();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[RecoilPredictor] ONNX load failed: " + ex.Message);
                _session = null;
            }
        }
    }

    /// <summary>
    ///     Predict next-frame kick in residual pixel space (same units as phase-correlate residual).
    /// </summary>
    /// <param name="burstFrame">0-based bullets in current continuous LMB hold.</param>
    /// <param name="residualHistory">Newest-last list of (rx, ry); up to 4 entries.</param>
    /// <param name="weaponClass">Detection class name or empty.</param>
    public Vector2 Predict(int burstFrame, IReadOnlyList<(float rx, float ry)> residualHistory, string? weaponClass)
    {
        EnsureLoaded();
        burstFrame = Math.Max(0, burstFrame);

        if (_session != null && _inputName != null && _outputName != null)
        {
            try
            {
                return PredictOnnx(burstFrame, residualHistory, weaponClass);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[RecoilPredictor] ONNX infer failed: " + ex.Message);
            }
        }

        return PredictAnalytic(burstFrame, residualHistory, weaponClass);
    }

    private Vector2 PredictOnnx(int burstFrame, IReadOnlyList<(float rx, float ry)> residualHistory, string? weaponClass)
    {
        // Input tensor [1, 4, 3]: pad older frames with zeros; channels = rx, ry, burstNorm
        float burstNorm = Math.Clamp(burstFrame / 30f, 0f, 1.5f);
        var tensor = new DenseTensor<float>(new[] { 1, HistoryLen, 3 });
        int n = residualHistory.Count;
        for (int i = 0; i < HistoryLen; i++)
        {
            int src = n - HistoryLen + i;
            float rx = 0, ry = 0;
            if (src >= 0)
            {
                rx = residualHistory[src].rx;
                ry = residualHistory[src].ry;
            }
            tensor[0, i, 0] = rx;
            tensor[0, i, 1] = ry;
            tensor[0, i, 2] = burstNorm;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName!, tensor)
        };

        using var results = _session!.Run(inputs);
        var outT = results.First().AsEnumerable<float>().ToArray();
        float kx = outT.Length > 0 ? outT[0] : 0;
        float ky = outT.Length > 1 ? outT[1] : 0;
        return new Vector2(kx, ky);
    }

    private Vector2 PredictAnalytic(int burstFrame, IReadOnlyList<(float rx, float ry)> residualHistory, string? weaponClass)
    {
        int idx = Math.Min(burstFrame, PriorY.Length - 1);
        float py = PriorY[idx];
        float px = PriorX[idx];

        // Blend recent residual trend into the prior (online adaptation without training).
        if (residualHistory.Count >= 2)
        {
            var a = residualHistory[^2];
            var b = residualHistory[^1];
            float trendY = b.ry - a.ry;
            float trendX = b.rx - a.rx;
            py = py * 0.65f + b.ry * 0.25f + trendY * 0.10f;
            px = px * 0.65f + b.rx * 0.25f + trendX * 0.10f;
        }
        else if (residualHistory.Count == 1)
        {
            py = py * 0.7f + residualHistory[0].ry * 0.3f;
            px = px * 0.7f + residualHistory[0].rx * 0.3f;
        }

        // Class-scale heuristics for common FPS naming.
        float scale = ClassScale(weaponClass);
        return new Vector2(px * scale, py * scale);
    }

    private static float ClassScale(string? weaponClass)
    {
        if (string.IsNullOrWhiteSpace(weaponClass)) return 1f;
        var w = weaponClass.ToLowerInvariant();
        if (w.Contains("awp") || w.Contains("sniper") || w.Contains("scout")) return 0.35f;
        if (w.Contains("pistol") || w.Contains("usp") || w.Contains("glock") || w.Contains("deagle")) return 0.55f;
        if (w.Contains("smg") || w.Contains("mp") || w.Contains("mac") || w.Contains("ump") || w.Contains("p90")) return 0.75f;
        if (w.Contains("ak") || w.Contains("galil") || w.Contains("sg")) return 1.15f;
        if (w.Contains("m4") || w.Contains("aug") || w.Contains("famas") || w.Contains("rifle")) return 1.0f;
        return 1f;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            try { _session?.Dispose(); } catch { /* ignore */ }
            _session = null;
            try { _ownedOptions?.Dispose(); } catch { /* ignore */ }
            _ownedOptions = null;
        }
    }
}
