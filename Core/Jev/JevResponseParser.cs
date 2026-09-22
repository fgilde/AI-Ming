using System.Text.Json;

namespace Core.Jev;

/// <summary>Turns a <c>/v1/systemone</c> JSON body into a <see cref="JevDecision"/>. Tolerant: unknown question types are skipped, malformed JSON → null.</summary>
public static class JevResponseParser
{
    public static JevDecision? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("answers", out var answers) ||
                answers.ValueKind != JsonValueKind.Object)
                return null;

            var d = new JevDecision();
            foreach (var q in answers.EnumerateObject())
            {
                var a = q.Value;
                if (a.ValueKind != JsonValueKind.Object) continue;
                var type = a.TryGetProperty("type", out var t) ? t.GetString() : null;

                if (type == "choice" && a.TryGetProperty("choice", out var c))
                {
                    var probs = new Dictionary<string, double>();
                    if (a.TryGetProperty("probabilities", out var p) && p.ValueKind == JsonValueKind.Object)
                        foreach (var kv in p.EnumerateObject())
                            if (kv.Value.TryGetDouble(out var v)) probs[kv.Name] = v;
                    double conf = a.TryGetProperty("confidence", out var cf) && cf.TryGetDouble(out var cv) ? cv : 0;
                    d.Choices[q.Name] = new JevChoice(c.GetString() ?? "", conf, probs);
                }
                else if (type == "noul" && a.TryGetProperty("noul", out var n) && n.TryGetDouble(out var nv))
                {
                    d.Nouls[q.Name] = nv;
                }
            }
            return d;
        }
    }
}
