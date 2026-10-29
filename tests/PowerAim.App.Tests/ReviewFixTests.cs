using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PowerAim.AI;
using PowerAim.AI.Aiming;
using PowerAim.AI.AutoPlay;
using PowerAim.AI.Decisions;
using PowerAim.Config;
using PowerAim.Input;

namespace PowerAim.App.Tests;

public class ActuatorReleaseTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    private static ReflexCommand Holding(ControlRole role)
    {
        var cmd = new ReflexCommand();
        cmd.Hold.Add(role);
        return cmd;
    }

    [Fact]
    public async Task With_key_jitter_a_release_never_overtakes_its_press()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var input = new RecordingInput();
            var actuator = new AutoPlayActuator(input, new Random(seed));
            var profile = AutoPlayFixtures.Fps();
            profile.KeyDelayJitterMs = 25;
            var map = AutoPlayActionMap.Build(profile);

            actuator.Apply(Holding(ControlRole.Shoot), map, profile, T0);
            actuator.Apply(new ReflexCommand(), map, profile, T0.AddMilliseconds(16));
            // The jittered sends run on the thread pool; under a busy test run 120 ms was not always enough.
            for (int waited = 0; input.Keys.Count < 2 && waited < 2000; waited += 10) await Task.Delay(10);

            Assert.Equal([("K", KeyPressState.Down), ("K", KeyPressState.Up)], input.Keys);
        }
    }

    [Fact]
    public async Task Release_all_waits_for_a_pending_jittered_press()
    {
        var input = new RecordingInput();
        var actuator = new AutoPlayActuator(input, new Random(1));
        var profile = AutoPlayFixtures.Fps();
        profile.KeyDelayJitterMs = 25;
        var map = AutoPlayActionMap.Build(profile);

        actuator.Apply(Holding(ControlRole.MoveForward), map, profile, T0);
        actuator.ReleaseAll();
        for (int waited = 0; input.Keys.Count < 2 && waited < 2000; waited += 10) await Task.Delay(10);

        Assert.Equal(KeyPressState.Up, input.Keys.Last().State);
        Assert.Equal(2, input.Keys.Count);
    }

    [Fact]
    public void Release_goes_to_the_keys_that_were_pressed_even_after_an_edit()
    {
        var input = new RecordingInput();
        var actuator = new AutoPlayActuator(input, new Random(1));
        var profile = AutoPlayFixtures.Fps();
        var map = AutoPlayActionMap.Build(profile);

        actuator.Apply(Holding(ControlRole.MoveForward), map, profile, T0);
        map.Get(ControlRole.MoveForward)!.Keys = [Keys.Up];
        actuator.ReleaseAll();

        Assert.Equal(("W", KeyPressState.Up), input.Keys.Last());
    }

    [Fact]
    public void Release_all_from_another_thread_while_frames_run_does_not_throw()
    {
        var actuator = new AutoPlayActuator(new RecordingInput(), new Random(1));
        var profile = AutoPlayFixtures.Fps();
        var map = AutoPlayActionMap.Build(profile);
        var stop = DateTime.UtcNow.AddMilliseconds(300);

        var frames = Task.Run(() =>
        {
            int i = 0;
            while (DateTime.UtcNow < stop)
                actuator.Apply(Holding(i++ % 2 == 0 ? ControlRole.MoveLeft : ControlRole.MoveRight), map, profile, T0.AddMilliseconds(i));
        });
        while (DateTime.UtcNow < stop) actuator.ReleaseAll();
        frames.Wait();
    }
}

public class StrategistStopTests
{
    private sealed class SlowModel(Task<AutoPlayIntent?> answer) : IDecisionModel
    {
        public string Source => "Slow";
        public string? LastError => null;
        public double? LastLatencyMs => null;
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<AutoPlayIntent?> DecideAsync(DecisionRequest request, CancellationToken ct) => answer; // ignores ct, like Ollama
    }

    public StrategistStopTests()
        => typeof(AppConfig).GetProperty(nameof(AppConfig.Current))!.SetValue(null, new AppConfig());

    [Fact]
    public async Task An_answer_arriving_after_stop_does_not_steer_the_bot()
    {
        var answer = new TaskCompletionSource<AutoPlayIntent?>();
        var profile = new AutoPlayProfile { DecisionModelId = DecisionModelRegistry.JevId };
        var s = new AutoPlayStrategist(() => profile,
            p => new DecisionRequest(p, new DecisionSituation("fps", default, 0, null, null, false, []), [], [], () => null),
            _ => new SlowModel(answer.Task));
        using var cts = new CancellationTokenSource();

        var step = s.StepAsync(cts.Token);
        cts.Cancel();
        answer.SetResult(new AutoPlayIntent("engage", "left", null, DateTime.Now));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => step);
        Assert.Equal("default", s.Intent.Mode);
    }
}

public class ReviewSelectionTests
{
    private static Prediction Det(float x) => new()
    {
        Confidence = 0.9f,
        Rectangle = new RectangleF(x - 20, 290, 40, 80),
        TranslatedRectangle = new RectangleF(x - 20, 290, 40, 80),
    };

    [Fact]
    public void A_target_the_rule_rejects_is_not_re_adopted()
    {
        var tracker = new TargetTracker();
        IReadOnlyList<TargetTrack> tracks = [];
        for (int i = 0; i < 4; i++) tracks = tracker.Update([Det(320), Det(380)], 1.0 / 60);
        var selector = new TargetSelector();
        int first = selector.Select(tracks, 320, 320, 0.25, 6, 160)!.Id;

        var next = selector.Select(tracks, 320, 320, 0.25, 6, 160, t => t.Id == first ? double.NaN : 1);

        Assert.NotNull(next);
        Assert.NotEqual(first, next!.Id);
    }

    [Fact]
    public void Changing_a_built_in_models_name_from_a_profile_makes_an_own_entry()
    {
        var models = new ObservableCollection<DecisionModelDefinition>(DecisionModelRegistry.CreateBuiltIns());
        var builtIn = models.Single(m => m.Id == DecisionModelRegistry.OllamaVisionId);

        var own = DecisionModelRegistry.WithModelName(models, builtIn, "llava:7b", "Ollama vision");
        var again = DecisionModelRegistry.WithModelName(models, builtIn, "llava:7b", "Ollama vision");

        Assert.Equal("moondream", builtIn.Model);       // the shared default is untouched
        Assert.Same(own, again);                          // reused, not duplicated
        Assert.Equal(("Ollama vision: llava:7b", "llava:7b", false), (own.Name, own.Model, own.IsBuiltIn));
        Assert.Same(own, DecisionModelRegistry.WithModelName(models, own, "llava:13b", "x"));
        Assert.Equal("llava:13b", own.Model);             // an own entry is edited in place
    }

    [Fact]
    public void Demonstration_store_tracks_unsaved_changes_and_survives_a_corrupt_file()
    {
        var store = new DemonstrationStore();
        Assert.False(store.IsDirty);
        store.Add(new Demonstration("p", new float[DecisionFeatures.Length], ["jump"]));
        Assert.True(store.IsDirty);

        var path = Path.Combine(Path.GetTempPath(), $"demos_{Guid.NewGuid():N}.json");
        try
        {
            store.Save(path);
            Assert.False(store.IsDirty);
            File.WriteAllText(path, "{ not json");
            Assert.False(store.Load(path));
            Assert.Equal(1, store.Count);
        }
        finally { File.Delete(path); }
    }
}
