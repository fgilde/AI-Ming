using System.ComponentModel;
using System.Drawing;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using InputLogic;
using System.Windows.Forms;

namespace Aimmy2.AILogic.Actions;

/// <summary>
/// AutoPlay action that automatically plays FPS games.
/// - Uses the existing AimAssist (AimingAction) for aiming at enemies
/// - Handles movement, shooting, and camera control
/// - Ollama is optional for smarter decisions
/// </summary>
public class AutoPlayGameAction : BaseAction
{
    // Keys being held
    private readonly HashSet<StoredInputBinding> _heldKeys = new();
    private readonly HashSet<string> _activeActionNames = new();

    // Mouse state
    private bool _isAiming = false;      // Right mouse (triggers AimAssist)
    private bool _isShooting = false;    // Left mouse

    // Exploration state
    private DateTime _lastDirectionChange = DateTime.MinValue;
    private DateTime _lastLookChange = DateTime.MinValue;
    private string _exploreDirection = "forward";
    private int _lookDirection = 0;
    private int _ticksWithoutEnemy = 0;
    private Random _random = new();

    private static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        System.Diagnostics.Debug.WriteLine($"[AutoPlay {timestamp}] {message}");
    }

    public AutoPlayGameAction()
    {
        AppConfig.Current.ToggleState.PropertyChanged += OnToggleStateChanged;
        Log("AutoPlayGameAction initialized - using AimAssist for aiming");
    }

    private void OnToggleStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppConfig.Current.ToggleState.AutoPlay))
        {
            if (!AppConfig.Current.ToggleState.AutoPlay)
            {
                Log("AutoPlay disabled");
                ReleaseEverything();
            }
            else
            {
                Log("AutoPlay enabled - LET'S PLAY!");
            }
        }
    }

    public override bool Active => base.Active && AppConfig.Current.ToggleState.AutoPlay;

    public override async Task ExecuteAsync(Prediction[] predictions)
    {
        if (!Active)
        {
            ReleaseEverything();
            return;
        }

        var profile = GetActiveProfile();
        if (profile == null)
            return;

        var screen = Screen.PrimaryScreen;
        if (screen == null) return;

        var centerX = screen.Bounds.Width / 2f;
        var centerY = screen.Bounds.Height / 2f;

        var actions = new List<string>();

        if (predictions.Length > 0)
        {
            // ====== ENEMY DETECTED! ======
            _ticksWithoutEnemy = 0;

            // Find closest enemy to crosshair
            var closest = predictions
                .OrderBy(p => GetDistanceToCenter(p, centerX, centerY))
                .First();

            var distance = GetDistanceToCenter(closest, centerX, centerY);

            // HOLD RIGHT MOUSE - This triggers AimAssist to aim at the enemy!
            if (!_isAiming)
            {
                StartAiming();
                Log(">>> AIM - Enemy detected, AimAssist will track");
            }

            // If enemy is close to crosshair - SHOOT!
            if (distance < 180)
            {
                if (!_isShooting)
                {
                    StartShooting();
                    Log(">>> FIRE! Enemy in crosshair");
                }
            }
            else
            {
                // Enemy visible but not quite in crosshair yet
                // AimAssist is moving the crosshair, just wait or strafe
                if (_isShooting && distance > 250)
                {
                    StopShooting();
                }

                // Strafe towards enemy if needed
                var distX = closest.CenterXTranslated - centerX;
                if (Math.Abs(distX) > 200)
                {
                    actions.Add(distX < 0 ? "move_left" : "move_right");
                }

                // Move closer if enemy is far
                if (distance > 500)
                {
                    actions.Add("move_forward");
                    actions.Add("sprint");
                }
            }
        }
        else
        {
            // ====== NO ENEMIES - EXPLORE! ======
            _ticksWithoutEnemy++;

            // Stop combat actions
            if (_isAiming)
            {
                StopAiming();
            }
            if (_isShooting)
            {
                StopShooting();
            }

            // Change direction every few seconds
            if ((DateTime.Now - _lastDirectionChange).TotalSeconds > 2.0 || _ticksWithoutEnemy > 40)
            {
                _lastDirectionChange = DateTime.Now;
                _ticksWithoutEnemy = 0;

                var patterns = new[] { "forward", "forward", "forward", "forward_left", "forward_right", "left", "right" };
                _exploreDirection = patterns[_random.Next(patterns.Length)];
                Log($">>> Exploring: {_exploreDirection}");
            }

            // Look around to find enemies (move camera)
            if ((DateTime.Now - _lastLookChange).TotalSeconds > 0.8)
            {
                _lastLookChange = DateTime.Now;

                // Random look direction with bias forward
                var lookOptions = new[] { -1, -1, 0, 0, 0, 1, 1 };
                _lookDirection = lookOptions[_random.Next(lookOptions.Length)];

                if (_lookDirection != 0)
                {
                    int moveX = _lookDirection * _random.Next(40, 100);
                    int moveY = _random.Next(-20, 20);
                    MouseManager.Move(moveX, moveY);
                }
            }

            // Movement
            actions.Add("move_forward");
            actions.Add("sprint");

            if (_exploreDirection.Contains("left"))
                actions.Add("move_left");
            else if (_exploreDirection.Contains("right"))
                actions.Add("move_right");
            else if (_exploreDirection == "left")
            {
                actions.Remove("move_forward");
                actions.Add("move_left");
            }
            else if (_exploreDirection == "right")
            {
                actions.Remove("move_forward");
                actions.Add("move_right");
            }

            // Occasional jump
            if (_random.Next(100) < 2)
            {
                actions.Add("jump");
            }
        }

        // Apply movement actions (keyboard only, mouse handled separately)
        await ApplyMovementActions(actions, profile);
    }

    private double GetDistanceToCenter(Prediction p, float centerX, float centerY)
    {
        var dx = p.CenterXTranslated - centerX;
        var dy = p.CenterYTranslated - centerY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void StartAiming()
    {
        if (!_isAiming)
        {
            _isAiming = true;
            // Hold right mouse button - AimAssist checks this!
            MouseManager.SendMouseEvent(MouseButtons.Right, 0, 0, 0, KeyPressState.Down);
        }
    }

    private void StopAiming()
    {
        if (_isAiming)
        {
            _isAiming = false;
            MouseManager.SendMouseEvent(MouseButtons.Right, 0, 0, 0, KeyPressState.Up);
        }
    }

    private void StartShooting()
    {
        if (!_isShooting)
        {
            _isShooting = true;
            MouseManager.LeftDown();
        }
    }

    private void StopShooting()
    {
        if (_isShooting)
        {
            _isShooting = false;
            MouseManager.LeftUp();
        }
    }

    private async Task ApplyMovementActions(List<string> actionNames, AutoPlayProfile profile)
    {
        var newActionSet = new HashSet<string>(actionNames);

        // Find actions to stop
        var toStop = _activeActionNames.Where(a => !newActionSet.Contains(a)).ToList();

        // Find actions to start
        var toStart = actionNames.Where(a => !_activeActionNames.Contains(a)).ToList();

        // Release stopped actions
        foreach (var actionName in toStop)
        {
            var action = profile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action != null)
            {
                foreach (var key in action.Keys.Where(k => k.IsValid && k.MouseEventArgs == null))
                {
                    if (_heldKeys.Remove(key))
                    {
                        await InputSender.SendKeyAsync(key, KeyPressState.Up);
                    }
                }
            }
            _activeActionNames.Remove(actionName);
        }

        // Press new actions
        foreach (var actionName in toStart)
        {
            var action = profile.Actions.FirstOrDefault(a => a.Name == actionName);
            if (action == null) continue;

            _activeActionNames.Add(actionName);

            // Handle based on action type
            if (action.ActionType == AutoPlayActionType.Instant || action.ActionType == AutoPlayActionType.Toggle)
            {
                // Quick tap
                foreach (var key in action.Keys.Where(k => k.IsValid && k.MouseEventArgs == null))
                {
                    await InputSender.SendKeyAsync(key, KeyPressState.Down);
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(action.Duration, 0.05)));
                foreach (var key in action.Keys.Where(k => k.IsValid && k.MouseEventArgs == null))
                {
                    await InputSender.SendKeyAsync(key, KeyPressState.Up);
                }
                _activeActionNames.Remove(actionName);
            }
            else
            {
                // Hold down
                foreach (var key in action.Keys.Where(k => k.IsValid && k.MouseEventArgs == null))
                {
                    if (_heldKeys.Add(key))
                    {
                        await InputSender.SendKeyAsync(key, KeyPressState.Down);
                    }
                }
            }
        }
    }

    private AutoPlayProfile? GetActiveProfile()
    {
        return AppConfig.Current.AutoPlayProfiles.FirstOrDefault(p => p.IsActive);
    }

    private void ReleaseEverything()
    {
        // Release all keyboard keys
        foreach (var key in _heldKeys.ToList())
        {
            _ = InputSender.SendKeyAsync(key, KeyPressState.Up);
        }
        _heldKeys.Clear();
        _activeActionNames.Clear();

        // Release mouse buttons
        StopAiming();
        StopShooting();

        Log("Released everything");
    }

    public override Task OnPause()
    {
        ReleaseEverything();
        return base.OnPause();
    }

    public override void Dispose()
    {
        ReleaseEverything();
        AppConfig.Current.ToggleState.PropertyChanged -= OnToggleStateChanged;
        base.Dispose();
    }
}
