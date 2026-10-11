using System;

namespace PowerAim.AI.Aiming;

/// <summary>
/// NSA (Noise Scale Adaptive) constant-velocity Kalman filter for bounding boxes
/// in (cx, cy, aspect, height) + velocity space. Ported from DigitalAI StrongSORT.
/// </summary>
public sealed class NsaKalmanFilter
{
    private const float StdWeightPosition = 1f / 20f;
    private const float StdWeightVelocity = 1f / 160f;

    private readonly float[,] _f = new float[8, 8];
    private readonly float[,] _ft = new float[8, 8];
    private readonly float[,] _scratchPh = new float[8, 4];
    private readonly float[,] _scratchK = new float[8, 4];
    private readonly float[,] _scratch4 = new float[4, 4];
    private readonly float[,] _scratch8 = new float[8, 8];
    private readonly float[] _scratch4v = new float[4];

    public NsaKalmanFilter()
    {
        for (int i = 0; i < 8; i++)
        {
            _f[i, i] = 1f;
            _ft[i, i] = 1f;
        }
        for (int i = 0; i < 4; i++)
        {
            _f[i, i + 4] = 1f;
            _ft[i + 4, i] = 1f;
        }
    }

    public void Initiate(float[] measurement4, out float[] mean, out float[,] cov)
    {
        mean = new float[8];
        cov = new float[8, 8];
        for (int i = 0; i < 4; i++)
            mean[i] = measurement4[i];

        float h = Math.Max(measurement4[3], 1f);
        Span<float> std = stackalloc float[8]
        {
            2 * StdWeightPosition * h,
            2 * StdWeightPosition * h,
            1e-2f,
            2 * StdWeightPosition * h,
            10 * StdWeightVelocity * h,
            10 * StdWeightVelocity * h,
            1e-5f,
            10 * StdWeightVelocity * h
        };
        for (int i = 0; i < 8; i++)
            cov[i, i] = std[i] * std[i];
    }

    public void Predict(ref float[] mean, ref float[,] cov)
    {
        float h = Math.Max(mean[3], 1f);
        Span<float> stdPos = stackalloc float[4]
        {
            StdWeightPosition * h, StdWeightPosition * h, 1e-2f, StdWeightPosition * h
        };
        Span<float> stdVel = stackalloc float[4]
        {
            StdWeightVelocity * h, StdWeightVelocity * h, 1e-5f, StdWeightVelocity * h
        };

        for (int i = 0; i < 4; i++)
            mean[i] += mean[i + 4];

        MulInto(_f, cov, _scratch8);
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                float s = 0;
                for (int k = 0; k < 8; k++)
                    s += _scratch8[i, k] * _ft[k, j];
                cov[i, j] = s;
            }
        }
        for (int i = 0; i < 4; i++)
        {
            cov[i, i] += stdPos[i] * stdPos[i];
            cov[i + 4, i + 4] += stdVel[i] * stdVel[i];
        }
    }

    public void Update(ref float[] mean, ref float[,] cov, float[] measurement4, float confidence)
    {
        ProjectInto(mean, cov, confidence, _scratch4v, _scratch4);

        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 4; j++)
                _scratchPh[i, j] = cov[i, j];

        SolveGainInto(_scratchPh, _scratch4, _scratchK);

        Span<float> innov = stackalloc float[4];
        for (int i = 0; i < 4; i++)
            innov[i] = measurement4[i] - _scratch4v[i];

        for (int i = 0; i < 8; i++)
        {
            float s = 0;
            for (int j = 0; j < 4; j++)
                s += _scratchK[i, j] * innov[j];
            mean[i] += s;
        }

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                float s = 0;
                for (int a = 0; a < 4; a++)
                {
                    float row = 0;
                    for (int b = 0; b < 4; b++)
                        row += _scratchK[i, b] * _scratch4[b, a];
                    s += row * _scratchK[j, a];
                }
                cov[i, j] -= s;
            }
        }
    }

    public float GatingDistance(float[] mean, float[,] cov, float[] measurement4)
    {
        Project(mean, cov, null, out float[] projMean, out float[,] projCov);
        Span<float> d = stackalloc float[4];
        for (int i = 0; i < 4; i++)
            d[i] = measurement4[i] - projMean[i];

        if (!TryCholesky(projCov, out float[,] L))
            return float.PositiveInfinity;

        Span<float> z = stackalloc float[4];
        for (int i = 0; i < 4; i++)
        {
            float s = d[i];
            for (int j = 0; j < i; j++)
                s -= L[i, j] * z[j];
            z[i] = s / L[i, i];
        }
        float dist = 0;
        for (int i = 0; i < 4; i++)
            dist += z[i] * z[i];
        return dist;
    }

    public void Project(float[] mean, float[,] cov, float? confidence,
        out float[] projMean, out float[,] projCov)
    {
        float h = Math.Max(mean[3], 1f);
        Span<float> std = stackalloc float[4]
        {
            StdWeightPosition * h, StdWeightPosition * h, 1e-1f, StdWeightPosition * h
        };

        projMean = new float[4];
        for (int i = 0; i < 4; i++)
            projMean[i] = mean[i];

        projCov = new float[4, 4];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                projCov[i, j] = cov[i, j];

        float nsa = 1f;
        if (confidence is float c)
            nsa = Math.Max(1f - c, 0.05f);

        for (int i = 0; i < 4; i++)
            projCov[i, i] += nsa * std[i] * std[i];
    }

    private static void MulInto(float[,] a, float[,] b, float[,] result)
    {
        int r = result.GetLength(0);
        int n = a.GetLength(1);
        int c = result.GetLength(1);
        for (int i = 0; i < r; i++)
        {
            for (int j = 0; j < c; j++)
            {
                float s = 0;
                for (int k = 0; k < n; k++)
                    s += a[i, k] * b[k, j];
                result[i, j] = s;
            }
        }
    }

    private static void ProjectInto(float[] mean, float[,] cov, float? confidence,
        float[] projMean, float[,] projCov)
    {
        float h = Math.Max(mean[3], 1f);
        Span<float> std = stackalloc float[4]
        {
            StdWeightPosition * h, StdWeightPosition * h, 1e-1f, StdWeightPosition * h
        };

        for (int i = 0; i < 4; i++)
            projMean[i] = mean[i];

        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                projCov[i, j] = cov[i, j];

        float nsa = 1f;
        if (confidence is float c)
            nsa = Math.Max(1f - c, 0.05f);

        for (int i = 0; i < 4; i++)
            projCov[i, i] += nsa * std[i] * std[i];
    }

    private void SolveGainInto(float[,] ph, float[,] s, float[,] k)
    {
        if (!TryInvert4(s, out float[,] inv))
        {
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 4; j++)
                    k[i, j] = 0f;
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                float sum = 0;
                for (int n = 0; n < 4; n++)
                    sum += ph[i, n] * inv[n, j];
                k[i, j] = sum;
            }
        }
    }

    private static bool TryCholesky(float[,] a, out float[,] L)
    {
        L = new float[4, 4];
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                float sum = a[i, j];
                for (int k = 0; k < j; k++)
                    sum -= L[i, k] * L[j, k];
                if (i == j)
                {
                    if (sum <= 1e-12f) return false;
                    L[i, j] = MathF.Sqrt(sum);
                }
                else L[i, j] = sum / L[j, j];
            }
        }
        return true;
    }

    private static bool TryInvert4(float[,] m, out float[,] inv)
    {
        inv = new float[4, 4];
        var a = new float[4, 8];
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++) a[i, j] = m[i, j];
            a[i, i + 4] = 1f;
        }
        for (int col = 0; col < 4; col++)
        {
            int pivot = col;
            float best = Math.Abs(a[col, col]);
            for (int r = col + 1; r < 4; r++)
            {
                float v = Math.Abs(a[r, col]);
                if (v > best) { best = v; pivot = r; }
            }
            if (best < 1e-12f) return false;
            if (pivot != col)
            {
                for (int j = 0; j < 8; j++)
                    (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
            }
            float div = a[col, col];
            for (int j = 0; j < 8; j++) a[col, j] /= div;
            for (int r = 0; r < 4; r++)
            {
                if (r == col) continue;
                float f = a[r, col];
                for (int j = 0; j < 8; j++)
                    a[r, j] -= f * a[col, j];
            }
        }
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                inv[i, j] = a[i, j + 4];
        return true;
    }
}