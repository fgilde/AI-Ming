using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Contracts;
using Aimmy2.Types;
using InputLogic;
using Nextended.Core.Extensions;
using Other;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AKeyChanger.xaml
    /// </summary>
    public partial class AKeyChanger : INotifyPropertyChanged, IDisposable
    {
        private string _text;
        private string _keyConfigPrefix;

        public event EventHandler<EventArgs<(AKeyChanger Sender, string Key)>> GlobalKeyPressed;
        public event EventHandler<EventArgs<(AKeyChanger Sender, StoredInputBinding KeyBinding)>> KeyBindChanged;
        
        public bool CanRemoveBinding
        {
            get => (bool)GetValue(CanRemoveBindingProperty);
            set => SetValue(CanRemoveBindingProperty, value);
        }



        public string InvalidText
        {
            get => (string)GetValue(InvalidTextProperty);
            set => SetValue(InvalidTextProperty, value);
        }

        // Using a DependencyProperty as the backing store for InvalidText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InvalidTextProperty =
            DependencyProperty.Register(nameof(InvalidText), typeof(string), typeof(AKeyChanger), new PropertyMetadata("None"));



        public static readonly DependencyProperty CanRemoveBindingProperty =
            DependencyProperty.Register(nameof(CanRemoveBinding), typeof(bool), typeof(AKeyChanger), new PropertyMetadata(true));
        
        public static readonly DependencyProperty BindingManagerProperty =
            DependencyProperty.Register(nameof(BindingManager), typeof(InputBindingManager), typeof(AKeyChanger),
                new PropertyMetadata(null));

        public static readonly DependencyProperty KeyConfigNameProperty =
            DependencyProperty.Register(nameof(KeyConfigName), typeof(string), typeof(AKeyChanger),
                new PropertyMetadata(null));

        public static readonly DependencyProperty WithBorderProperty =
            DependencyProperty.Register(nameof(WithBorder), typeof(bool), typeof(AKeyChanger),
                new PropertyMetadata(true, WithBorderChanged));

        
        public StoredInputBinding KeyBind
        {
            get => (StoredInputBinding)GetValue(KeyBindProperty);
            set => SetValue(KeyBindProperty, value);
        }

        // Using a DependencyProperty as the backing store for KeyBind.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty KeyBindProperty =
            DependencyProperty.Register(nameof(KeyBind), typeof(StoredInputBinding), typeof(AKeyChanger), new PropertyMetadata(new StoredInputBinding(), HandleKeyBindChanged));

        private static void HandleKeyBindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AKeyChanger)d).SetContent((StoredInputBinding)e.NewValue);
            ((AKeyChanger)d).KeyBindChanged?.Invoke(d, new EventArgs<(AKeyChanger Sender, StoredInputBinding KeyBinding)>(((AKeyChanger)d, (StoredInputBinding)e.NewValue)));
        }


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

        public AKeyChanger(string text, StoredInputBinding keybind) : this()
        {
            _text = text;
            KeyBind = keybind;
            SetContent(text, keybind);
        }

        private void AKeyChanger_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!KeyBind.IsValid)
            {
                KeyBind = AppConfig.Current.BindingSettings[KeyConfigName];
                if(KeyBind.IsValid)
                    SetContent(KeyBind);
            }

            if (BindingManager != null)
            {
                BindingManager.SetupDefault(KeyConfigName, KeyBind);
                BindingManager.OnBindingPressed += OnGlobalKeyHandler;
            }
        }

        private void OnGlobalKeyHandler(string key)
        {
            if (HasKeySet && key == KeyConfigName && !InUpdateMode)
            {
                if(KeyBind.Is<MouseEventArgs>() && (IsMouseOver || ContextMenu.IsOpen))
                    return;
                var args = new EventArgs<(AKeyChanger Sender, string Key)>((this, key));
                GlobalKeyPressed?.Invoke(this, args);
            }
        }

        public event EventHandler<EventArgs> KeyDeleted;

        public bool InUpdateMode { get; private set; }

        public void SetContent(string text, StoredInputBinding keybind)
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

        private void SetContent(string text)
        {
            KeyNotifierLabel.Content = text;
        }

        public void SetContent(StoredInputBinding keybind)
        {
            ToolTip = Locale.KeyChangerToolTip;
            HasKeySet = keybind.IsValid;
            SetDeviceIcon();
            if (!HasKeySet)
            {
                KeyNotifierLabel.Content = InvalidText;
                return;
            }
            if (keybind.Is<GamepadEventArgs>())
            {
                SetDeviceIcon("\uE7FC");
            }
            else if (keybind.Is<MouseEventArgs>())
            {
                SetDeviceIcon("\uF8AF");
            }
            else if (keybind.Is<KeyEventArgs>())
            {
                SetDeviceIcon("\uE765");
            }

            var keyName = KeybindNameManager.ConvertToRegularKey(keybind.Key);
            ToolTip = Locale.KeyChangerToolTipWithBinding.FormatWith(Locale.GetString(keybind.DeviceName), keyName);
            KeyNotifierLabel.Content = keyName;
        }

        private void DeleteBinding_Click(object sender, RoutedEventArgs e)
        {
            if(!string.IsNullOrEmpty(KeyConfigName))
                AppConfig.Current.BindingSettings[KeyConfigName] = StoredInputBinding.Empty;
            KeyBind = StoredInputBinding.Empty;
            KeyDeleted?.Invoke(this, EventArgs.Empty);
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
            MainGrid.ContextMenu = null;
            SetDeviceIcon("\uEA3B", Brushes.Red);
            SetContent("...");
            ToolTip = Locale.KeyChangerToolTipRecording;
            BindingManager.StartListeningForBinding(KeyConfigName);

            // Event handler for setting the binding
            Action<string, StoredInputBinding>? bindingSetHandler = null;
            bindingSetHandler = (bindingId, key) =>
            {
                if (bindingId == KeyConfigName)
                {
                    KeyBind = key;
                    if(!string.IsNullOrEmpty(bindingId))
                        AppConfig.Current.BindingSettings[bindingId] = key;
                    BindingManager.OnBindingSet -= bindingSetHandler; // Unsubscribe after setting
                    Task.Delay(700).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            InUpdateMode = false;
                            MainGrid.ContextMenu = ContextMenu;
                        });
                    });
                }
            };

            BindingManager.OnBindingSet += bindingSetHandler;
        }

        private void SetDeviceIcon(string icon = "", Brush? fg = null)
        {
            KeyDeviceInfo.Text = icon;
            KeyDeviceInfo.FontSize = icon == "\uF8AF" ? 14 : 18;
            KeyDeviceInfo.Foreground = fg ?? Brushes.White;
            KeyDeviceInfo.Visibility = string.IsNullOrEmpty(icon) ? Visibility.Collapsed : Visibility.Visible;
        }

        public void Dispose()
        {
            if (BindingManager != null)
            {
                BindingManager.OnBindingPressed -= OnGlobalKeyHandler;
            }
        }


        private void ContextMenu_OnOpened(object sender, MouseButtonEventArgs e)
        {
            if (!CanRemoveBinding || !KeyBind.IsValid)
            {
                e.Handled = true;
                ContextMenu.IsOpen = false;
            }
        }

    }
}