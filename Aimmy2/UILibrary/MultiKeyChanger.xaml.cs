using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Aimmy2;
using Aimmy2.InputLogic;
using Aimmy2.Types;
using Aimmy2.UILibrary;
using InputLogic;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for MultiKeyChanger.xaml
    /// </summary>
    public partial class MultiKeyChanger : UserControl
    {
        public InputBindingManager BindingManager => MainWindow.Instance.BindingManager;
        public ObservableCollection<StoredInputBinding> Keys    
        {
            get => (ObservableCollection<StoredInputBinding>)GetValue(KeysProperty);
            set => SetValue(KeysProperty, value);
        }



        public string ErrorMessage
        {
            get { return (string)GetValue(ErrorMessageProperty); }
            set { SetValue(ErrorMessageProperty, value); }
        }

        public static readonly DependencyProperty ErrorMessageProperty =
            DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(MultiKeyChanger), new PropertyMetadata(string.Empty));



        public static readonly DependencyProperty KeysProperty =
            DependencyProperty.Register("Keys", typeof(ObservableCollection<StoredInputBinding>), typeof(MultiKeyChanger), new PropertyMetadata(new ObservableCollection<StoredInputBinding>(), OnKeysChanged));

        private bool _internalChange;
        private static void OnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MultiKeyChanger)d).OnKeysChanged();
        }

        private void OnKeysChanged()
        {
            if (_internalChange) return;
            _internalChange = true;
            ErrorMessage = string.Empty;
            var keys = Keys;
            var invalids = keys.Where(k => !k.IsValid).ToList();
            foreach (var invalid in invalids)
            {
                if (keys.Contains(invalid))
                    keys.Remove(invalid);
            }

            keys.Add(new StoredInputBinding());

            _internalChange = false;
        }



        public MultiKeyChanger()
        {
            InitializeComponent();
        }
        
        private void AKeyChanger_OnKeyBindChanged(object? sender, EventArgs<(AKeyChanger Sender, StoredInputBinding KeyBinding)> e)
        {
            if (e.Value.Sender.Tag is StoredInputBinding binding)
            {
                if (Keys.Contains(binding))
                {
                    if (e.Value.KeyBinding.IsValid && Keys.Any(b => b.Equals(e.Value.KeyBinding)))
                    {
                        OnKeysChanged();
                        ErrorMessage = $"The key {e.Value.KeyBinding.Key} already exists in this list";
                        return;
                    }
                    try
                    {
                        Keys[Keys.IndexOf(binding)] = e.Value.KeyBinding;
                    }
                    catch
                    {
                        Keys.Remove(binding);
                    }
                    OnKeysChanged();
                }
            }
        }

        private void Remove_Key_Click(object sender, MouseButtonEventArgs e)
        {
           if (((FrameworkElement)sender).Tag is StoredInputBinding binding)
               Keys.Remove(binding);
        }
    }
}
