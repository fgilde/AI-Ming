using System.Collections.Concurrent;
using Gma.System.MouseKeyHook;
using PowerAim.InputLogic;
using PowerAim.InputLogic.Contracts;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace PowerAim.InputLogic
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

        // Live device-qualified set of currently-held raw inputs — the source of truth for matching
        // both singles AND chords. ConcurrentDictionary so the kb/mouse-hook thread and the gamepad
        // (UI-thread) callback can mutate it without corrupting a plain HashSet.
        private static readonly ConcurrentDictionary<string, byte> _heldKeys = new();
        // When each currently-held chord first became fully held (for combo MinTime). Self-managed in IsHoldingBindingFor.
        private static readonly ConcurrentDictionary<StoredInputBinding, DateTime> _comboHeldSince = new();
        // Recording accumulator — press order preserved so a modifier held first is sent first.
        private readonly List<StoredInputBinding> _recordPressed = new();
        private bool _recordStarted;

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
            if (binding is not { IsValid: true }) return false;

            if (binding.IsCombo)
            {
                // A chord counts as held only while ALL its components are in the live held set.
                if (!IsHeldNow(binding)) { _comboHeldSince.TryRemove(binding, out _); return false; }
                if (duration == null || duration <= TimeSpan.Zero) return true;
                // Lazily stamp when the chord first became fully held — works whether or not the chord
                // is registered in `bindings` (TriggerKeys / AimKeyBindings query this path directly).
                var since = _comboHeldSince.GetOrAdd(binding, _ => DateTime.Now);
                return (DateTime.Now - since) >= duration;
            }

            var holdingEntry = holdingBindings.FirstOrDefault(h => h.Binding.Equals(binding));
            if (holdingEntry.Binding is { IsValid: true } && holdingEntry.IsHolding)
            {
                if (duration == null || duration <= TimeSpan.Zero)
                    return true;

                return (DateTime.Now - holdingEntry.StartTime) >= duration;
            }

            return false;
        }

        // Device-qualified key for one raw input ("Keyboard:X", "Mouse:Left", "Gamepad:RT") — the
        // qualifier stops keyboard "A" colliding with gamepad "A".
        private static string Qual(StoredInputBinding single) => $"{single.DeviceName}:{single.Key}";

        // True when the binding is fully held right now: a single → its key in the held set; a combo
        // → every component key in the held set.
        private static bool IsHeldNow(StoredInputBinding b) =>
            b.IsCombo
                ? b.Components!.All(c => _heldKeys.ContainsKey(Qual(c)))
                : _heldKeys.ContainsKey(Qual(b));

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
            _recordPressed.Clear();
            _recordStarted = false;
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
            if (e.IsStickEvent) return; // sticks are never bindings
            OnRawInput(new StoredInputBinding(e), e.IsPressed == true);
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

        // All four global hooks (and the gamepad callback) funnel into one pipeline so single- and
        // chord-matching live in exactly one place.
        private void GlobalHookKeyDown(object sender, KeyEventArgs e) => OnRawInput(new StoredInputBinding(e), true);
        private void GlobalHookKeyUp(object sender, KeyEventArgs e) => OnRawInput(new StoredInputBinding(e), false);
        private void GlobalHookMouseDown(object sender, MouseEventArgs e) => OnRawInput(new StoredInputBinding(e), true);
        private void GlobalHookMouseUp(object sender, MouseEventArgs e) => OnRawInput(new StoredInputBinding(e), false);

        // ===== Unified raw-input pipeline (single + combo) =====
        private void OnRawInput(StoredInputBinding raw, bool pressed)
        {
            if (raw is not { IsValid: true }) return;
            if (settingBindingId != null) { HandleRecording(raw, pressed); return; }

            // Per-input hold tracking + OnKeyPressed/Released. Singles only (raw is always a single
            // here) — feeds the MultiKeyChanger sequence recorder and the single path of
            // IsHoldingBindingFor. Semantics unchanged vs the old per-handler code.
            UpdateHoldingState(raw, pressed);

            // The live device-qualified held set drives matching for singles AND chords.
            var qual = Qual(raw);
            if (pressed) _heldKeys[qual] = 0; else _heldKeys.TryRemove(qual, out _);

            // Edge-detect every registered binding against the new held set: rising → Pressed, falling
            // → Released. A single is just a 1-component chord, so this path covers both uniformly.
            foreach (var kv in bindings.ToArray())
            {
                bool nowHeld = IsHeldNow(kv.Value);
                bool wasHeld = isHolding.TryGetValue(kv.Key, out var st) && st.IsHolding;
                if (nowHeld && !wasHeld)
                {
                    isHolding[kv.Key] = (true, DateTime.Now);
                    InvokeBindingPressed(kv);
                }
                else if (!nowHeld && wasHeld)
                {
                    isHolding[kv.Key] = (false, DateTime.MinValue);
                    InvokeBindingReleased(kv);
                }
            }
        }

        // Commit-on-release recording: accumulate everything pressed during the record session, then on
        // the FIRST genuine release snapshot it all into a single (1 input) or a chord (>=2). Press
        // order is preserved so a modifier held first is sent first when the chord is later replayed.
        private void HandleRecording(StoredInputBinding raw, bool pressed)
        {
            if (pressed)
            {
                if (!_recordPressed.Any(p => Qual(p) == Qual(raw))) _recordPressed.Add(raw);
                _recordStarted = true;
                return;
            }
            // Ignore: (a) releases before anything was pressed — the very click that started recording;
            // (b) releases of an input never pressed this session — e.g. a gamepad trigger's spurious
            // sub-threshold IsPressed=false events.
            if (!_recordStarted || !_recordPressed.Any(p => Qual(p) == Qual(raw))) return;

            var id = settingBindingId!;
            var combo = StoredInputBinding.Combo(_recordPressed.ToList());
            settingBindingId = null;
            _recordStarted = false;
            _recordPressed.Clear();
            bindings[id] = combo;
            OnBindingSet?.Invoke(id, combo);
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
