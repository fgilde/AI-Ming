using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PowerAim.Config;

namespace PowerAim.AILogic;

/// <summary>One answered <c>choice</c> question: the picked option key, how concentrated the distribution was, and the full distribution.</summary>
public sealed record JevChoice(string Choice, double Confidence, IReadOnlyDictionary<string, double> Probabilities);

/// <summary>Parsed <c>/v1/systemone</c> response — choice answers by question id, noul (yes-probability) answers by question id.</summary>
public sealed class JevDecision
{
    public Dictionary<string, JevChoice> Choices { get; } = new();
    public Dictionary<string, double> Nouls { get; } = new();
}

/// <summary>
///     Minimal client for the Jev decision contract (<c>POST {BaseUrl}/v1/systemone</c>): a <c>state</c>
///     (any JSON) plus named questions of type <c>choice</c> / <c>noul</c>, answered with probabilities in a
///     single forward pass. Works against the local simple-jev hf-server and the hosted TypeSafe API
///     (same path; the latter needs the bearer key). No streaming, no text generation.
/// </summary>
public sealed class JevDecisionClient
{
    // Process-wide, like OllamaClient — HttpClient is meant to be long-lived.
    private static readonly HttpClient Http = new();

    public string? LastError { get; private set; }

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

    /// <summary>
    ///     Ask the model. <paramref name="questions"/> is the raw questions map (built by the caller with
    ///     anonymous objects: <c>new { type = "choice", instructions = "...", criteria = ... }</c>).
    ///     Returns null on any failure — the caller keeps its previous intent.
    /// </summary>
    public async Task<JevDecision?> DecideAsync(object state, Dictionary<string, object> questions, CancellationToken ct = default)
    {
        var s = Settings;
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
                // Keep the server's validation text — a 422 tells us exactly which question shape it disliked.
                LastError = $"HTTP {(int)resp.StatusCode}: {Truncate(json, 300)}";
                return null;
            }

            LastError = null;
            return Parse(json);
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

    private static JevDecision? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("answers", out var answers)) return null;

        var d = new JevDecision();
        foreach (var q in answers.EnumerateObject())
        {
            var a = q.Value;
            var type = a.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type == "choice" && a.TryGetProperty("choice", out var c))
            {
                var probs = new Dictionary<string, double>();
                if (a.TryGetProperty("probabilities", out var p))
                    foreach (var kv in p.EnumerateObject()) probs[kv.Name] = kv.Value.GetDouble();
                double conf = a.TryGetProperty("confidence", out var cf) ? cf.GetDouble() : 0;
                d.Choices[q.Name] = new JevChoice(c.GetString() ?? "", conf, probs);
            }
            else if (type == "noul" && a.TryGetProperty("noul", out var n))
            {
                d.Nouls[q.Name] = n.GetDouble();
            }
        }
        return d;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
