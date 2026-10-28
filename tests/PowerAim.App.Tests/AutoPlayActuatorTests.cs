using System.Collections.ObjectModel;
using System.Windows.Forms;
using Nextended.UI.Input;
using PowerAim.AI.AutoPlay;
using PowerAim.Config;
using PowerAim.Input;

namespace PowerAim.App.Tests;

internal sealed class RecordingInput : IAutoPlayInput
{
    public List<(string Key, KeyPressState State)> Keys { get; } = new();
    public List<(int Dx, int Dy)> Moves { get; } = new();
    public void Send(StoredInputBinding key, KeyPressState state) => Keys.Add((key.Key, state));
    public void Move(int dx, int dy) => Moves.Add((dx, dy));
}

internal static class AutoPlayFixtures
{
    public static AutoPlayAction Action(string name, Keys key, AutoPlayActionType type = AutoPlayActionType.Continuous,
        string? description = null, double duration = 0.1) =>
        new() { Name = name, Description = description ?? "", Keys = [key], ActionType = type, Duration = duration };

    public static AutoPlayProfile Profile(params AutoPlayAction[] actions) => new()
    {
        Name = "test",
        Enabled = true,
        Actions = new ObservableCollection<AutoPlayAction>(actions),
    };

    public static AutoPlayProfile Fps() => Profile(
        Action("move_forward", System.Windows.Forms.Keys.W),
        Action("move_backward", System.Windows.Forms.Keys.S),
        Action("move_left", System.Windows.Forms.Keys.A),
        Action("move_right", System.Windows.Forms.Keys.D),
        Action("sprint", System.Windows.Forms.Keys.ShiftKey, AutoPlayActionType.Modifier),
        Action("jump", System.Windows.Forms.Keys.Space, AutoPlayActionType.Instant, "hops over obstacles"),
        Action("shoot", System.Windows.Forms.Keys.K),
        Action("aim", System.Windows.Forms.Keys.L, AutoPlayActionType.Modifier),
        Action("reload", System.Windows.Forms.Keys.R, AutoPlayActionType.Instant),
        Action("grenade", System.Windows.Forms.Keys.G, AutoPlayActionType.Instant, "throws a grenade at a group"),
        Action("ping", System.Windows.Forms.Keys.Z, AutoPlayActionType.Instant, "marks the enemy for the team"));
}

public class AutoPlayActionMapTests
{
    [Fact]
    public void Roles_by_name_and_the_rest_as_abilities()
    {
        var map = AutoPlayActionMap.Build(AutoPlayFixtures.Fps());

        Assert.Equal("move_left", map.Get(ControlRole.MoveLeft)!.Name);
        Assert.Equal("shoot", map.Get(ControlRole.Shoot)!.Name);
        Assert.Equal(["grenade", "ping"], map.Tactical.Select(a => a.Name));
        // Models may trigger everything except the per-frame controls: jump and reload included.
        Assert.Equal(["jump", "reload", "grenade", "ping"], map.Strategic.Select(a => a.Name));
        Assert.Equal("reload", map.RoleKey(map.Get(ControlRole.Reload)!));
    }

    [Fact]
    public void One_action_fills_at_most_one_role()
    {
        var map = AutoPlayActionMap.Build(AutoPlayFixtures.Profile(AutoPlayFixtures.Action("fire_left", Keys.Q)));
        Assert.NotNull(map.Get(ControlRole.MoveLeft) ?? map.Get(ControlRole.Shoot));
        Assert.False(map.Get(ControlRole.MoveLeft) != null && map.Get(ControlRole.Shoot) != null);
    }
}

public class AutoPlayActuatorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void Presses_new_holds_once_and_releases_dropped_ones()
    {
        var input = new RecordingInput();
        var actuator = new AutoPlayActuator(input, new Random(1));
        var profile = AutoPlayFixtures.Fps();
        var map = AutoPlayActionMap.Build(profile);
        var cmd = new ReflexCommand();

        cmd.Hold.Add(ControlRole.MoveForward);
        actuator.Apply(cmd, map, profile, T0);
        actuator.Apply(cmd, map, profile, T0.AddMilliseconds(16));
        Assert.Equal([("W", KeyPressState.Down)], input.Keys);

        cmd.Hold.Clear();
        actuator.Apply(cmd, map, profile, T0.AddMilliseconds(32));
        Assert.Equal(("W", KeyPressState.Up), input.Keys.Last());
    }

    [Fact]
    public void Taps_release_after_their_duration_and_release_all_leaves_nothing_down()
    {
        var input = new RecordingInput();
        var actuator = new AutoPlayActuator(input, new Random(1));
        var profile = AutoPlayFixtures.Fps();
        var map = AutoPlayActionMap.Build(profile);
        var cmd = new ReflexCommand();
        cmd.Tap.Add(ControlRole.Jump);

        actuator.Apply(cmd, map, profile, T0);
        Assert.Equal(("Space", KeyPressState.Down), input.Keys.Single());

        actuator.Apply(new ReflexCommand(), map, profile, T0.AddMilliseconds(150));
        Assert.Equal(("Space", KeyPressState.Up), input.Keys.Last());

        var hold = new ReflexCommand();
        hold.Hold.Add(ControlRole.Shoot);
        actuator.Apply(hold, map, profile, T0.AddSeconds(1));
        actuator.ReleaseAll();
        Assert.Equal(("K", KeyPressState.Up), input.Keys.Last());
    }

    [Fact]
    public void Mouse_moves_are_scaled_by_the_profile()
    {
        var input = new RecordingInput();
        var actuator = new AutoPlayActuator(input, new Random(1));
        var profile = AutoPlayFixtures.Fps();
        profile.MouseSensScale = 2.0;
        var cmd = new ReflexCommand { MouseDx = 10, MouseDy = -3 };

        actuator.Apply(cmd, AutoPlayActionMap.Build(profile), profile, T0);

        Assert.Equal((20, -6), input.Moves.Single());
    }
}

public class AutoPlayTacticsTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void Model_listed_actions_all_fire_once_per_intent()
    {
        var map = AutoPlayActionMap.Build(AutoPlayFixtures.Fps());
        var tactics = new AutoPlayTactics(new Random(1));
        var intent = new AutoPlayIntent("default", null, null, T0, Actions: ["ping", "jump", "unknown"]);

        var first = tactics.Next(intent, map, ready: false, inCombat: true, _ => false, T0);
        var again = tactics.Next(intent, map, ready: false, inCombat: true, _ => false, T0.AddSeconds(1));

        Assert.Equal(["ping", "jump"], first.Select(a => a.Name));
        Assert.Empty(again);
    }

    [Fact]
    public void A_new_intent_may_repeat_an_action_but_not_within_the_minimum_gap()
    {
        var map = AutoPlayActionMap.Build(AutoPlayFixtures.Fps());
        var tactics = new AutoPlayTactics(new Random(1));
        tactics.Next(new AutoPlayIntent("default", null, null, T0, Actions: ["ping"]), map, false, false, _ => false, T0);

        var soon = tactics.Next(new AutoPlayIntent("default", null, null, T0, Actions: ["ping"]), map, false, false, _ => false, T0.AddMilliseconds(100));
        var later = tactics.Next(new AutoPlayIntent("default", null, null, T0, Actions: ["ping"]), map, false, false, _ => false, T0.AddSeconds(1));

        Assert.Empty(soon);
        Assert.Single(later);
    }

    [Fact]
    public void Hint_wins_then_cooldown_blocks_the_next_pick()
    {
        var map = AutoPlayActionMap.Build(AutoPlayFixtures.Fps());
        var tactics = new AutoPlayTactics(new Random(1));
        var intent = new AutoPlayIntent("tactical", null, "reload", T0);

        Assert.Equal("reload", tactics.Next(intent, map, true, true, _ => false, T0).Single().Name);
        Assert.Empty(tactics.Next(intent, map, true, true, _ => false, T0.AddSeconds(1)));
    }

    [Fact]
    public void Without_a_hint_picks_a_free_form_ability_only_when_ready()
    {
        var map = AutoPlayActionMap.Build(AutoPlayFixtures.Fps());
        var tactics = new AutoPlayTactics(new Random(1));

        Assert.Empty(tactics.Next(AutoPlayIntent.Default, map, false, false, _ => false, T0));
        var pick = tactics.Next(AutoPlayIntent.Default, map, true, false, _ => false, T0).Single();
        Assert.Contains(pick.Name, new[] { "grenade", "ping" });
    }
}

public class AutoPlayCoordinationTests
{
    private static ActionTrigger Trigger(Keys action, bool needsDetection = true, Keys? triggerKey = null) => new()
    {
        Name = "t",
        Enabled = true,
        NeedsDetection = needsDetection,
        Actions = [action],
        TriggerKeys = triggerKey is { } k ? [k] : [],
    };

    [Fact]
    public void An_always_on_detection_trigger_on_the_shoot_key_owns_firing()
    {
        var shoot = AutoPlayFixtures.Action("shoot", Keys.K);
        Assert.True(AutoPlayCoordination.TriggerOwnsFire([Trigger(Keys.K)], shoot, _ => true));
    }

    [Theory]
    [InlineData(Keys.J, true, false, true)]   // different key
    [InlineData(Keys.K, false, false, true)]  // not detection driven
    [InlineData(Keys.K, true, true, true)]    // needs a held trigger key: never fires while the bot plays
    [InlineData(Keys.K, true, false, false)]  // inactive
    public void Other_triggers_do_not(Keys key, bool needsDetection, bool hasTriggerKey, bool active)
    {
        var shoot = AutoPlayFixtures.Action("shoot", Keys.K);
        var trigger = Trigger(key, needsDetection, hasTriggerKey ? Keys.F : null);
        Assert.False(AutoPlayCoordination.TriggerOwnsFire([trigger], shoot, _ => active));
    }
}
