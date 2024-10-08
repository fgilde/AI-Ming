using Gma.System.MouseKeyHook;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Contracts;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace InputLogic
{
    public class InputBindingManager : IDisposable
    {
        public static InputBindingManager? Instance { get; private set; }
        public InputBindingManager()
        {
            Instance = this;
        }

        private IKeyboardMouseEvents? _mEvents;
        private bool _gamepadListen;
        private readonly Dictionary<string, StoredInputBinding> bindings = new();
        private static readonly Dictionary<string, (bool IsHolding, DateTime StartTime)> isHolding = new();
        private string? settingBindingId = null;
        private static readonly List<(StoredInputBinding Binding, bool IsHolding, DateTime StartTime)> holdingBindings = new();

        public event Action<string, StoredInputBinding>? OnBindingSet;

        public event Action<StoredInputBinding>? OnKeyPressed;

        public event Action<StoredInputBinding>? OnKeyReleased;

        public event Action<string>? OnBindingPressed;

        public event Action<string>? OnBindingReleased;
        public event Action<MouseEventArgs>? OnMouseMove;
        public event Action<MouseEventExtArgs>? OnMouseMoveExt;


        public static bool IsHoldingBinding(string bindingId)
        {
            return isHolding.TryGetValue(bindingId, out var holding) && holding.IsHolding;
        }

        public static TimeSpan GetHoldingTime(string bindingId)
        {
            if (isHolding.TryGetValue(bindingId, out var holding) && holding.IsHolding)
            {
                return DateTime.Now - holding.StartTime;
            }
            return TimeSpan.Zero;
        }

        public static bool IsHoldingBinding(StoredInputBinding binding) => IsHoldingBindingFor(binding, null);

        public static bool IsHoldingBindingFor(StoredInputBinding binding, TimeSpan? duration)
        {
            var holdingEntry = holdingBindings.FirstOrDefault(h => h.Binding.Equals(binding));

            if (holdingEntry.Binding is { IsValid: true } && holdingEntry.IsHolding)
            {
                if (duration == null || duration <= TimeSpan.Zero)
                    return true;

                return (DateTime.Now - holdingEntry.StartTime) >= duration;
            }

            return false;
        }

        private void UpdateHoldingState(StoredInputBinding binding, bool isPressed)
        {
            if(isPressed)
                OnKeyPressed?.Invoke(binding);
            else
                OnKeyReleased?.Invoke(binding);

            var index = holdingBindings.FindIndex(h => h.Binding.Equals(binding));

            if (index != -1)
            {
                if (isPressed)
                {
                    holdingBindings[index] = (binding, true, DateTime.Now);
                }
                else
                {
                    holdingBindings[index] = (binding, false, DateTime.MinValue);
                }
            }
            else if (isPressed)
            {
                holdingBindings.Add((binding, true, DateTime.Now));
            }
        }

        public static bool IsHoldingBindingFor(string bindingId, TimeSpan? duration)
        {
            if(duration == null || duration <= TimeSpan.Zero)
                return IsHoldingBinding(bindingId);
            return GetHoldingTime(bindingId) >= duration;
        }

        public void SetupDefault(string bindingId, StoredInputBinding bindingValue)
        {
            if (bindingValue is not { IsValid: true })
                return;
            bindings[bindingId] = bindingValue;
            isHolding[bindingId] = (false, DateTime.MinValue);
            OnBindingSet?.Invoke(bindingId, bindingValue);
            EnsureHookEvents();
        }

        public void StartListeningForBinding(string bindingId)
        {
            settingBindingId = bindingId;
            EnsureHookEvents();
        }

        private void EnsureHookEvents()
        {
            if (_mEvents == null)
            {
                _mEvents = Hook.GlobalEvents();
                _mEvents.KeyDown += GlobalHookKeyDown!;
                _mEvents.MouseDown += GlobalHookMouseDown!;
                _mEvents.KeyUp += GlobalHookKeyUp!;
                _mEvents.MouseUp += GlobalHookMouseUp!;
                _mEvents.MouseMove += MEventsOnMouseMove;
                _mEvents.MouseMoveExt += MEventsOnMouseMoveExt;
            }

            if (!_gamepadListen && GamepadManager.CanRead)
            {
                _gamepadListen = true;
                GamepadManager.GamepadReader.ButtonEvent += GamepadReader_ButtonEvent;
            }
        }

        private void MEventsOnMouseMoveExt(object? sender, MouseEventExtArgs e)
        {
            OnMouseMoveExt?.Invoke(e);
        }

        private void MEventsOnMouseMove(object? sender, MouseEventArgs e)
        {
            OnMouseMove?.Invoke(e);
        }

        private void GamepadReader_ButtonEvent(object? sender, GamepadEventArgs e)
        {
            if (!e.IsStickEvent)
            {
                var pressed = e.IsPressed == true;
                if (settingBindingId != null)
                {
                    bindings[settingBindingId] = new StoredInputBinding(e);
                    OnBindingSet?.Invoke(settingBindingId, bindings[settingBindingId]);
                    settingBindingId = null;
                }
                else
                {
                    UpdateHoldingState(new StoredInputBinding(e), pressed);
                    foreach (var binding in bindings)
                    {
                        if (binding.Value.Matches(e))
                        {
                            if (pressed)
                            {
                                isHolding[binding.Key] = (true, DateTime.Now);
                                InvokeBindingPressed(binding);
                            }
                            else
                            {
                                isHolding[binding.Key] = (false, DateTime.MinValue);
                                InvokeBindingReleased(binding);
                            }
                        }
                    }
                }
            }
        }

        private void InvokeBindingReleased(KeyValuePair<string, StoredInputBinding> binding)
        {
            OnBindingReleased?.Invoke(binding.Key);
        }

        private async void InvokeBindingPressed(KeyValuePair<string, StoredInputBinding> binding)
        {
            if (binding.Value == null || await binding.Value.WaitHoldingFor())
            {
                OnBindingPressed?.Invoke(binding.Key);
            }
        }

        private void GlobalHookKeyDown(object sender, KeyEventArgs e)
        {
            if (settingBindingId != null)
            {
                bindings[settingBindingId] = new StoredInputBinding(e);
                OnBindingSet?.Invoke(settingBindingId, bindings[settingBindingId]);
                settingBindingId = null;
            }
            else
            {
                UpdateHoldingState(new StoredInputBinding(e), true);

                foreach (var binding in bindings)
                {
                    if (binding.Value.Matches(e))
                    {
                        isHolding[binding.Key] = (true, DateTime.Now);
                        InvokeBindingPressed(binding);
                    }
                }
            }
        }

        private void GlobalHookMouseDown(object sender, MouseEventArgs e)
        {
            if (settingBindingId != null)
            {
                bindings[settingBindingId] = new StoredInputBinding(e);
                OnBindingSet?.Invoke(settingBindingId, bindings[settingBindingId]);
                settingBindingId = null;
            }
            else
            {
                UpdateHoldingState(new StoredInputBinding(e), true);

                foreach (var binding in bindings)
                {
                    if (binding.Value.Matches(e))
                    {
                        isHolding[binding.Key] = (true, DateTime.Now);
                        InvokeBindingPressed(binding);
                    }
                }
            }
        }

        private void GlobalHookKeyUp(object sender, KeyEventArgs e)
        {
            UpdateHoldingState(new StoredInputBinding(e), false);

            foreach (var binding in bindings)
            {
                if (binding.Value.Matches(e))
                {
                    isHolding[binding.Key] = (false, DateTime.MinValue);
                    InvokeBindingReleased(binding);
                }
            }
        }

        private void GlobalHookMouseUp(object sender, MouseEventArgs e)
        {
            UpdateHoldingState(new StoredInputBinding(e), false);
            foreach (var binding in bindings)
            {
                if (binding.Value.Matches(e))
                {
                    isHolding[binding.Key] = (false, DateTime.MinValue);
                    InvokeBindingReleased(binding);
                }
            }
        }

        public void StopListening()
        {
            if (_gamepadListen)
            {
                GamepadManager.GamepadReader.ButtonEvent -= GamepadReader_ButtonEvent;
                _gamepadListen = false;
            }
            if (_mEvents != null)
            {
                _mEvents.KeyDown -= GlobalHookKeyDown!;
                _mEvents.MouseDown -= GlobalHookMouseDown!;
                _mEvents.KeyUp -= GlobalHookKeyUp!;
                _mEvents.MouseUp -= GlobalHookMouseUp!;
                _mEvents.Dispose();
                _mEvents = null;
            }
        }

        public void Dispose()
        {
            StopListening();
            bindings.Clear();
        }
    }
}
