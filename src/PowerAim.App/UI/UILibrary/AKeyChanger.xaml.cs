using System.Windows;
using Nextended.UI.Input;
using Nextended.UI.WPF.Controls;
using PowerAim.Config;

namespace PowerAim.UI.UILibrary
{
    /// <summary>
    ///     The app's key-bind editor: a <see cref="KeyBindChanger"/> plus app policy — user edits are
    ///     persisted in <see cref="AppConfig.BindingSettings"/> under <see cref="BindingId"/> (and loaded
    ///     from there when no binding is given), presses honour the "keybinds only while Global Active"
    ///     gate, and texts/colors follow the app locale and theme.
    /// </summary>
    public partial class AKeyChanger : IDisposable
    {
        private string _text = string.Empty;
        private string? _keyConfigPrefix;

        /// <summary>The binding was pressed globally (and passed the Global Active gate).</summary>
        public event EventHandler? GlobalKeyPressed;

        /// <summary>A user edit is about to be applied; set Cancel to reject it.</summary>
        public event EventHandler<KeyBindChangingEventArgs>? KeyBindChanging;

        public event EventHandler<KeyBindChangedEventArgs>? KeyBindChanged;

        public AKeyChanger()
        {
            InitializeComponent();
            Changer.EmptyToolTip = Locale.KeyChangerToolTip;
            Changer.RecordingToolTip = Locale.KeyChangerToolTipRecording;
            Changer.BoundToolTipFormat = Locale.KeyChangerToolTipWithBinding;
            Changer.DeviceNameProvider = binding => Locale.GetString(binding.DeviceName);
            Changer.DeleteBindingText = Locale.DeleteBinding;
            Changer.MinTimeText = $"{Locale.MinTimeTriggerKey} ({Locale.Seconds})";

            Changer.BindingPressed += OnBindingPressed;
            Changer.KeyBindChanging += (_, e) => KeyBindChanging?.Invoke(this, e);
            Changer.KeyBindChanged += OnKeyBindChanged;
        }

        public AKeyChanger(string text, StoredInputBinding keybind) : this()
        {
            Text = text;
            KeyBind = keybind;
        }

        public static readonly DependencyProperty KeyBindProperty =
            DependencyProperty.Register(nameof(KeyBind), typeof(StoredInputBinding), typeof(AKeyChanger), new PropertyMetadata(null));

        public StoredInputBinding? KeyBind
        {
            get => (StoredInputBinding?)GetValue(KeyBindProperty);
            set => SetValue(KeyBindProperty, value);
        }

        public static readonly DependencyProperty BindingManagerProperty =
            DependencyProperty.Register(nameof(BindingManager), typeof(InputBindingManager), typeof(AKeyChanger), new PropertyMetadata(null));

        public InputBindingManager? BindingManager
        {
            get => (InputBindingManager?)GetValue(BindingManagerProperty);
            set => SetValue(BindingManagerProperty, value);
        }

        public static readonly DependencyProperty KeyConfigNameProperty =
            DependencyProperty.Register(nameof(KeyConfigName), typeof(string), typeof(AKeyChanger),
                new PropertyMetadata(null, (d, _) => ((AKeyChanger)d).UpdateBindingId()));

        /// <summary>Name the binding is stored under (falls back to <see cref="Text"/>); see <see cref="BindingId"/>.</summary>
        public string? KeyConfigName
        {
            get => (string?)GetValue(KeyConfigNameProperty);
            set => SetValue(KeyConfigNameProperty, value);
        }

        /// <summary>Namespace for <see cref="KeyConfigName"/>, e.g. "AIM_PROFILE" → "AIM_PROFILE_&lt;NAME&gt;".</summary>
        public string? KeyConfigPrefix
        {
            get => _keyConfigPrefix;
            set { _keyConfigPrefix = value; UpdateBindingId(); }
        }

        /// <summary>Title left of the bind button (hidden while empty).</summary>
        public string Text
        {
            get => _text;
            set { _text = value ?? string.Empty; Changer.Text = _text; UpdateBindingId(); }
        }

        /// <summary>
        ///     When true, this key changer always fires its binding, ignoring the
        ///     <see cref="ToggleState.RequireGlobalActiveForKeybinds"/> gate. Set this on the
        ///     Global Active toggle's changer so the user can always switch Global Active back on
        ///     by hotkey even while the gate would otherwise suppress every other keybind.
        /// </summary>
        public bool IgnoreGlobalActiveGate { get; set; }

        public string InvalidText { get => Changer.InvalidText; set => Changer.InvalidText = value; }
        public bool CanRemoveBinding { get => Changer.CanRemoveBinding; set => Changer.CanRemoveBinding = value; }
        public bool CanEditMinTime { get => Changer.CanEditMinTime; set => Changer.CanEditMinTime = value; }
        public bool WithBorder { get => Changer.WithBorder; set => Changer.WithBorder = value; }

        /// <summary>Settings key / binding-manager id: [prefix_]NAME, empty when neither name nor text is set.</summary>
        public string BindingId
        {
            get
            {
                var name = !string.IsNullOrEmpty(KeyConfigName) ? KeyConfigName : Text;
                return string.IsNullOrWhiteSpace(KeyConfigPrefix) || string.IsNullOrEmpty(name) ? name : CodeFor(name, KeyConfigPrefix);
            }
        }

        public static string CodeFor(string title, string prefix = "DYN") =>
            $"{prefix}_{title.Replace(" ", "_").ToUpper()}";

        public bool InUpdateMode => Changer.InUpdateMode;

        public void RecordNewBinding() => Changer.RecordNewBinding();

        // An empty id lets the control generate its own (and nothing is persisted).
        private void UpdateBindingId() => Changer.KeyConfigName = BindingId is { Length: > 0 } id ? id : null;

        private void AKeyChanger_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (KeyBind is not { IsValid: true } && BindingId.Length > 0 && AppConfig.Current?.BindingSettings[BindingId] is { } stored)
                KeyBind = stored;
        }

        private void OnKeyBindChanged(object? sender, KeyBindChangedEventArgs e)
        {
            if (e.IsUserChange && BindingId.Length > 0 && AppConfig.Current != null)
                AppConfig.Current.BindingSettings[BindingId] = e.NewValue ?? StoredInputBinding.Empty;
            KeyBindChanged?.Invoke(this, e);
        }

        private void OnBindingPressed(object? sender, EventArgs e)
        {
            // Optional "keybinds only while Global Active" gate, read live at press time. The Global
            // Active toggle's own changer opts out via IgnoreGlobalActiveGate.
            if (!IgnoreGlobalActiveGate
                && AppConfig.Current?.ToggleState is { RequireGlobalActiveForKeybinds: true, GlobalActive: false })
                return;
            GlobalKeyPressed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Changer.Dispose();
    }
}
