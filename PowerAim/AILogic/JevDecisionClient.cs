using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Core.Jev;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>
///     Minimal client for the Jev decision contract (<c>POST {BaseUrl}/v1/systemone</c>): a <c>state</c>
///     (any JSON) plus named questions of type <c>choice</c> / <c>noul</c>, answered with probabilities in a
///     single forward pass. Works against the local simple-jev hf-server and the hosted TypeSafe API
///     (same path; the latter needs the bearer key). Parsing lives in <see cref="JevResponseParser"/> (Core)
///     so it's unit-testable without WPF.
/// </summary>
public sealed class JevDecisionClient
{
    // Process-wide, like OllamaClient — HttpClient is meant to be long-lived.
    private static readonly HttpClient Http = new();

    public string? LastError { get; private set; }

    /// <summary>Wall-clock time of the last successful decision, for the transparency panel.</summary>
    public double LastLatencyMs { get; private set; }

    private static JevSettings Settings => AppConfig.Current?.JevSettings ?? new JevSettings();

    private static string Url(string path) => Settings.BaseUrl.TrimEnd('/') + path;

    /// <summary>
    ///     Reachability probe. simple-jev exposes <c>GET /health</c>; the hosted API doesn't, so any HTTP
    ///     answer at all (even 404/401) counts as "server is there" — only a connection failure is "down".
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var resp = await Http.GetAsync(Url("/health"), cts.Token);
            LastError = resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode} on /health";
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            return false;
        }
    }

    /// <summary>Ask the model. Returns null on any failure (see <see cref="LastError"/>) — the caller keeps its previous intent.</summary>
    public async Task<JevDecision?> DecideAsync(object state, Dictionary<string, object> questions, CancellationToken ct = default)
    {
        var s = Settings;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var body = JsonSerializer.Serialize(new { model = s.Model, state, questions });
            using var req = new HttpRequestMessage(HttpMethod.Post, Url("/v1/systemone"))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrWhiteSpace(s.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, s.TimeoutSeconds)));
            using var resp = await Http.SendAsync(req, cts.Token);
            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                // Keep the server's validation text — a 422 says exactly which question shape it disliked.
                LastError = $"HTTP {(int)resp.StatusCode}: {Truncate(json, 300)}";
                return null;
            }

            var decision = JevResponseParser.Parse(json);
            if (decision == null) { LastError = "Unparseable response: " + Truncate(json, 200); return null; }

            LastError = null;
            LastLatencyMs = sw.Elapsed.TotalMilliseconds;
            return decision;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LastError = "Request timeout";
            return null;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            return null;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
