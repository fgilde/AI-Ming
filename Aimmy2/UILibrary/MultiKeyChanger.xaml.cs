using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Aimmy2;
using Aimmy2.Extensions;
using Aimmy2.InputLogic;
using Aimmy2.Types;
using Aimmy2.UILibrary;
using InputLogic;
using Nextended.Core;
using static ICSharpCode.AvalonEdit.Document.TextDocumentWeakEventManager;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for MultiKeyChanger.xaml
    /// </summary>
    public partial class MultiKeyChanger : INotifyPropertyChanged
    {
        public InputBindingManager BindingManager => MainWindow.Instance.BindingManager;

        public event EventHandler<EventArgs<ObservableCollection<StoredInputBinding>>> Changed;

        public ObservableCollection<StoredInputBinding> Keys
        {
            get => (ObservableCollection<StoredInputBinding>)GetValue(KeysProperty);
            set => SetValue(KeysProperty, value);
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);


        public string ErrorMessage
        {
            get => (string)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        public static readonly DependencyProperty ErrorMessageProperty =
            DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(MultiKeyChanger), new PropertyMetadata(string.Empty, ErrorMessageChange));

        private static void ErrorMessageChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MultiKeyChanger)d).OnPropertyChanged(nameof(HasError));
        }
        
        public static readonly DependencyProperty KeysProperty =
            DependencyProperty.Register(nameof(Keys), typeof(ObservableCollection<StoredInputBinding>), typeof(MultiKeyChanger), new PropertyMetadata(new ObservableCollection<StoredInputBinding>(), OnKeysChanged));

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
            var invalids = keys.Where(k => k is not { IsValid: true }).ToList();
            foreach (var invalid in invalids)
            {
                if (keys.Contains(invalid))
                    keys.Remove(invalid);
            }
            keys.Add(new StoredInputBinding());
            Changed?.Invoke(this, new CancelableEventArgs<ObservableCollection<StoredInputBinding>>(keys));

            _internalChange = false;
        }


        public MultiKeyChanger()
        {
            InitializeComponent();
        }

        private bool HasDuplicate(StoredInputBinding binding)
        {
            if (binding is { IsValid: true } && Keys.Any(b => b?.Equals(binding) == true))
            {
                OnKeysChanged();
                ErrorMessage = $"The key {binding.Key} already exists in this list";
                return true;
            }

            return false;
        }


        private void AKeyChanger_OnKeyBindChanged(object? sender, CancelableEventArgs<(AKeyChanger Sender, StoredInputBinding KeyBinding, StoredInputBinding OldValue)> e)
        {
            ErrorMessage = string.Empty;
            if (HasDuplicate(e.Value.KeyBinding))
            {
                e.Cancel = true;
                if (e.Value.Sender.Tag is StoredInputBinding { IsValid: true } existing && Keys.Contains(existing))
                {
                    Keys[Keys.IndexOf(existing)] = e.Value.OldValue;
                    Task.Delay(100).ContinueWith(_ => e.Value.Sender.Dispatcher.Invoke(() =>
                    {
                        e.Value.Sender.RecordNewBinding();
                        e.Value.Sender.Focus();
                    }));
                    System.Diagnostics.Debug.WriteLine("Duplicate key found");
                }

                return;
            }

            if (e.Value.Sender.Tag is not StoredInputBinding { IsValid: true } binding)
            {
                Keys.Insert(Keys.Count - 1, e.Value.KeyBinding);
              
            }
            else
            {
                Keys[Keys.IndexOf(binding)] = e.Value.KeyBinding;
            }
            OnKeysChanged();
        }
        

        private void Remove_Key_Click(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is StoredInputBinding binding)
            {
                Keys.Remove(binding);
                OnKeysChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public MultiKeyChanger BindTo(Expression<Func<IEnumerable<StoredInputBinding>>> fn)
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Keys = new ObservableCollection<StoredInputBinding>(fn.Compile()());

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Keys = new ObservableCollection<StoredInputBinding>(fn.Compile()());
                }
            };

            Changed += (s, e) =>
            {
                propertyInfo.SetValue(owner, e.Value);
            };

            return this;
        }
    }
}
