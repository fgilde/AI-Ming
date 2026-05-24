using System.Windows.Forms;
using PowerAim.AILogic;
using PowerAim.Config;
using PowerAim.InputLogic;
using InputLogic;

namespace PowerAim.AILogic.Actions;

/// <summary>
///     Side-channel observer that watches the user's input and the current detection state, then
///     pushes (state → action) tuples into <see cref="AutoPlayLearningModel"/>.
///     <para>
///     Implemented as a <see cref="BaseAction"/> so it benefits from the same lifecycle the rest
///     of the pipeline already uses — when <see cref="ToggleState.GlobalActive"/> drops, this
///     action stops sampling too.
///     </para>
///     Sampling is gated by <see cref="AutoPlayLearningSettings.SampleIntervalMs"/> to avoid an
///     unbounded write rate at high inference FPS.
/// </summary>
public class AutoPlayLearningAction : BaseAction
{
    private DateTime _lastSample = DateTime.MinValue;

    public override bool Active =>
        base.Active &&
        AppConfig.Current.AutoPlayLearningSettings?.Recording == true;

    public override Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active) return Task.CompletedTask;

        var settings = AppConfig.Current.AutoPlayLearningSettings;
        var now = DateTime.UtcNow;
        if ((now - _lastSample).TotalMilliseconds < settings.SampleIntervalMs) return Task.CompletedTask;
        _lastSample = now;

        string state = DiscretizeState(predictions);
        string? action = DiscretizeUserAction();
        if (action != null) AutoPlayLearningModel.Instance.Record(state, action);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Cluster the current detection picture into a few coarse buckets. The grain is
    ///     intentionally low: we want a model that generalizes, not one that overfits exact
    ///     pixel coords.
    /// </summary>
    private static string DiscretizeState(Prediction[] predictions)
    {
        if (predictions == null || predictions.Length == 0) return "no_enemy";

        int img = PowerAim.AILogic.PredictionLogic.CurrentImageSize;
        float cx = img / 2.0f;
        float cy = img / 2.0f;

        // Closest prediction.
        Prediction? closest = null;
        float bestDist = float.MaxValue;
        foreach (var p in predictions)
        {
            float px = p.Rectangle.X + p.Rectangle.Width / 2f;
            float py = p.Rectangle.Y + p.Rectangle.Height / 2f;
            float dx = px - cx, dy = py - cy;
            float d = dx * dx + dy * dy;
            if (d < bestDist) { bestDist = d; closest = p; }
        }
        if (closest == null) return "no_enemy";

        float ccx = closest.Rectangle.X + closest.Rectangle.Width / 2f;
        float ccy = closest.Rectangle.Y + closest.Rectangle.Height / 2f;

        string horizontal =
            ccx < cx - img * 0.15 ? "left" :
            ccx > cx + img * 0.15 ? "right" :
            "centre";
        string size =
            closest.Rectangle.Width >= img * 0.45 ? "close" :
            closest.Rectangle.Width >= img * 0.15 ? "mid" :
            "far";
        string crowd = predictions.Length switch
        {
            1 => "solo",
            2 => "pair",
            _ => "many"
        };
        return $"{horizontal}_{size}_{crowd}";
    }

    /// <summary>
    ///     Approximate what the user is doing right now, as a single dominant action name. Picks
    ///     the first matching binding rather than emitting all held keys — keeps the table dense.
    /// </summary>
    private static string? DiscretizeUserAction()
    {
        if (IsHolding(MouseButtons.Left)) return "shoot";
        if (IsHolding(MouseButtons.Right)) return "aim";
        if (IsHolding(Keys.W)) return "move_forward";
        if (IsHolding(Keys.S)) return "move_backward";
        if (IsHolding(Keys.A)) return "move_left";
        if (IsHolding(Keys.D)) return "move_right";
        if (IsHolding(Keys.Space)) return "jump";
        if (IsHolding(Keys.ControlKey)) return "crouch";
        if (IsHolding(Keys.ShiftKey)) return "sprint";
        if (IsHolding(Keys.R)) return "reload";
        return null;
    }

    private static bool IsHolding(MouseButtons b) => InputBindingManager.IsHoldingBinding(b);
    private static bool IsHolding(Keys k) => InputBindingManager.IsHoldingBinding(k);

    public override Task OnPause()  => Task.CompletedTask;
    public override Task OnResume() => Task.CompletedTask;
    public override void Dispose() { /* no resources */ }
}
