namespace PowerAim.Config;

/// <summary>
///     Settings for the Jev-style decision endpoint used by AutoPlay's strategic layer. Jev (TypeSafe AI)
///     is a decision model — typed questions in, calibrated probabilities out, no text generation — and
///     <c>simple-jev</c> is the open-source server that speaks the same contract locally. Both expose
///     <c>POST /v1/systemone</c>, so the same client serves the local server (default) and the hosted
///     API (set <see cref="BaseUrl"/> to https://api.typesafe.ai and fill <see cref="ApiKey"/>).
/// </summary>
public class JevSettings : BaseSettings
{
    /// <summary>Server root. Default is the local simple-jev hf-server.</summary>
    public string BaseUrl
    {
        get;
        set => SetField(ref field, value);
    } = "http://127.0.0.1:8000";

    /// <summary>Bearer token — only needed for the hosted TypeSafe API. Empty for the local server.</summary>
    public string ApiKey
    {
        get;
        set => SetField(ref field, value);
    } = "";

    /// <summary>Model id the server should use. simple-jev: the HF id it was started with; hosted: "jev-latest".</summary>
    public string Model
    {
        get;
        set => SetField(ref field, value);
    } = "Qwen/Qwen3.5-0.8B";

    /// <summary>Torch device the locally managed server runs on — "cuda" or "cpu". Ignored for the hosted API.</summary>
    public string Device
    {
        get;
        set => SetField(ref field, value);
    } = "cuda";

    /// <summary>Per-decision request timeout. Decisions are ~0.1–0.5 s; anything slower is skipped.</summary>
    public int TimeoutSeconds
    {
        get;
        set => SetField(ref field, value);
    } = 5;

    /// <summary>
    ///     Decisions whose <c>confidence</c> is below this are ignored and the heuristic keeps its current
    ///     intent — a flat distribution over the modes shouldn't yank the bot around.
    /// </summary>
    public double MinConfidence
    {
        get;
        set => SetField(ref field, value);
    } = 0.35;
}
