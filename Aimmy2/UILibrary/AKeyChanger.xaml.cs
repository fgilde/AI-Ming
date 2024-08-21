using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aimmy2.Config;
using Aimmy2.InputLogic.Contracts;
using Aimmy2.Types;
using InputLogic;
using Other;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AKeyChanger.xaml
    /// </summary>
    public partial class AKeyChanger : INotifyPropertyChanged, IDisposable
    {
        private string _keyBind;
        private string _text;
        private string _keyConfigPrefix;

        public event EventHandler<EventArgs<(AKeyChanger Sender, string Key)>> GlobalKeyPressed;


        public static readonly DependencyProperty BindingManagerProperty =
            DependencyProperty.Register(nameof(BindingManager), typeof(InputBindingManager), typeof(AKeyChanger),
                new PropertyMetadata(null));

        public static readonly DependencyProperty KeyConfigNameProperty =
            DependencyProperty.Register(nameof(KeyConfigName), typeof(string), typeof(AKeyChanger),
                new PropertyMetadata(null));

        public static readonly DependencyProperty WithBorderProperty =
            DependencyProperty.Register(nameof(WithBorder), typeof(bool), typeof(AKeyChanger),
                new PropertyMetadata(true, WithBorderChanged));

        private static void WithBorderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var kc = d as AKeyChanger;
            if (kc == null)
                return;
            kc.MainBorder.BorderThickness = (bool)e.NewValue ? new Thickness(1, 0, 1, 0) : new Thickness(0);
            kc.Background = (bool)e.NewValue
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C"))
                : new SolidColorBrush(Colors.Transparent);
        }

        public bool WithBorder
        {
            get => (bool)GetValue(WithBorderProperty);
            set => SetValue(WithBorderProperty, value);
        }



        public InputBindingManager? BindingManager
        {
            get => (InputBindingManager?)GetValue(BindingManagerProperty);
            set => SetValue(BindingManagerProperty, value);
        }

        public string KeyConfigName
        {
            get
            {
                var keyConfigName = (string)GetValue(KeyConfigNameProperty);
                return GetCode(!string.IsNullOrEmpty(keyConfigName) ? keyConfigName : Text);
            }
            set => SetValue(KeyConfigNameProperty, value);
        }



        public string KeyConfigPrefix
        {
            get => _keyConfigPrefix;
            set => SetField(ref _keyConfigPrefix, value);
        }



        public string KeyBind
        {
            get => _keyBind;
            set
            {
                if (SetField(ref _keyBind, value))
                    SetContent(KeyBind);
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                if (SetField(ref _text, value))
                    SetContent(Text, KeyBind);
            }
        }

        public AKeyChanger()
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C"));
            InitializeComponent();
        }

        public AKeyChanger(string text, string keybind) : this()
        {
            _text = text;
            _keyBind = keybind;
            SetContent(text, keybind);
        }

        private void AKeyChanger_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(KeyBind))
            {
                KeyBind = AppConfig.Current.BindingSettings[KeyConfigName]?.ToString();
                SetContent(KeyBind ?? string.Empty);
            }

            if (BindingManager != null && !string.IsNullOrEmpty(KeyBind))
            {
                BindingManager.SetupDefault(KeyConfigName, KeyBind);
                BindingManager.OnBindingPressed += OnGlobalKeyHandler;
            }
        }

        private void OnGlobalKeyHandler(string key)
        {
            if (HasKeySet && key == KeyConfigName && !InUpdateMode)
            {
                var args = new EventArgs<(AKeyChanger Sender, string Key)>((this, key));
                GlobalKeyPressed?.Invoke(this, args);
            }
        }

        public event EventHandler<EventArgs> KeyDeleted;

        public bool InUpdateMode { get; set; }

        public void SetContent(string text, string keybind)
        {
            KeyChangerTitle.Content = text;
            SetContent(keybind);
        }

        public bool HasKeySet { get; private set; }

        public bool ShowTitle
        {
            get => Dispatcher.Invoke(() => KeyChangerTitle.Visibility == Visibility.Visible);
            set => Dispatcher.Invoke(() =>
                KeyChangerTitle.Visibility = value ? Visibility.Visible : Visibility.Collapsed);
        }

        public void SetContent(string keybind)
        {
            HasKeySet = !string.IsNullOrWhiteSpace(keybind);
            if (GamepadEventArgs.IsGamepadKey(keybind))
            {
                KeyNotifierLabel.Content = GamepadEventArgs.GetButtonName(keybind);
                GamepadInfo.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                KeyNotifierLabel.Content = KeybindNameManager.ConvertToRegularKey(keybind);
                GamepadInfo.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void DeleteBinding_Click(object sender, RoutedEventArgs e)
        {
            AppConfig.Current.BindingSettings[KeyConfigName] = "";
            SetContent("");
            KeyDeleted?.Invoke(this, EventArgs.Empty);
        }

        private void ContextMenu_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = InUpdateMode;
        }

        private string GetCode(string keyConfigName)
        {
            return string.IsNullOrWhiteSpace(KeyConfigPrefix) ? keyConfigName : CodeFor(keyConfigName, KeyConfigPrefix);
        }

        public static string CodeFor(string title, string prefix = "DYN") =>
            $"{prefix}_{title.Replace(" ", "_").ToUpper()}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void Reader_OnClick(object sender, RoutedEventArgs e)
        {
            if (InUpdateMode || BindingManager == null)
                return;
            InUpdateMode = true;
            SetContent("...");
            ToolTip = "Press any key to set the binding";
            BindingManager.StartListeningForBinding(KeyConfigName);

            // Event handler for setting the binding
            Action<string, string>? bindingSetHandler = null;
            bindingSetHandler = (bindingId, key) =>
            {
                if (bindingId == KeyConfigName)
                {
                    SetContent(key);
                    ToolTip = string.Empty;
                    AppConfig.Current.BindingSettings[bindingId] = key;
                    BindingManager.OnBindingSet -= bindingSetHandler; // Unsubscribe after setting
                    Task.Delay(300).ContinueWith(_ => InUpdateMode = false);
                }
            };

            BindingManager.OnBindingSet += bindingSetHandler;
        }

        public void Dispose()
        {
            if (BindingManager != null)
            {
                BindingManager.OnBindingPressed -= OnGlobalKeyHandler;
            }
        }
    }
}