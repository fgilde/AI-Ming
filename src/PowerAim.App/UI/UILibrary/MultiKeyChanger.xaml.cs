using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim;
using PowerAim.Extensions;
using PowerAim.Input;
using PowerAim.Types;
using PowerAim.UI.UILibrary;

using Nextended.Core;
using Nextended.Core.Extensions;
using static ICSharpCode.AvalonEdit.Document.TextDocumentWeakEventManager;
using Nextended.UI.Input;
using Nextended.UI.WPF.Controls;

namespace PowerAim.UI.UILibrary
{
    /// <summary>
    /// Interaction logic for MultiKeyChanger.xaml
    /// </summary>
    public partial class MultiKeyChanger : INotifyPropertyChanged
    {
        public InputBindingManager BindingManager =>
            MainWindow.Instance?.BindingManager
            ?? throw new InvalidOperationException("MainWindow / BindingManager is not ready.");

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



        public bool AllowDuplicates
        {
            get { return (bool)GetValue(AllowDuplicatesProperty); }
            set { SetValue(AllowDuplicatesProperty, value); }
        }



        public bool CanRecordSequence
        {
            get { return (bool)GetValue(CanRecordSequenceProperty); }
            set { SetValue(CanRecordSequenceProperty, value); }
        }

        
        public static readonly DependencyProperty CanRecordSequenceProperty =
            DependencyProperty.Register(nameof(CanRecordSequence), typeof(bool), typeof(MultiKeyChanger), new PropertyMetadata(false, CanRecordChange));

        private static void CanRecordChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MultiKeyChanger)d).CanRecordChange();
        }

        private void CanRecordChange()
        {
            RecordButton.Visibility = CanRecordSequence ? Visibility.Visible : Visibility.Collapsed;
        }


        public static readonly DependencyProperty AllowDuplicatesProperty =
            DependencyProperty.Register(nameof(AllowDuplicates), typeof(bool), typeof(MultiKeyChanger), new PropertyMetadata(false));


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
            if (AllowDuplicates) return false;
            if (binding is { IsValid: true } && Keys.Any(b => b?.Equals(binding) == true))
            {
                OnKeysChanged();
                ErrorMessage = Locale.KeyDuplicateError.FormatWith(binding.Key);
                return true;
            }

            return false;
        }


        private void AKeyChanger_OnKeyBindChanging(object? sender, KeyBindChangingEventArgs e)
        {
            var changer = (AKeyChanger)sender!;
            ErrorMessage = string.Empty;
            if (HasDuplicate(e.NewValue))
            {
                e.Cancel = true;
                if (changer.Tag is StoredInputBinding { IsValid: true } existing && Keys.Contains(existing))
                {
                    Keys[Keys.IndexOf(existing)] = e.OldValue ?? existing;
                    Task.Delay(100).ContinueWith(_ => changer.Dispatcher.Invoke(() =>
                    {
                        changer.RecordNewBinding();
                        changer.Focus();
                    }));
                }

                return;
            }

            if (changer.Tag is not StoredInputBinding { IsValid: true } binding)
            {
                Keys.Insert(Keys.Count - 1, e.NewValue);

            }
            else
            {
                Keys[Keys.IndexOf(binding)] = e.NewValue;
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

            Keys = new(fn.Compile()());

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Keys = new(fn.Compile()());
                }
            };

            Changed += (s, e) =>
            {
                propertyInfo.SetValue(owner, e.Value);
            };

            return this;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            SetRecordSequence(_recording = !_recording);
            RecordButton.Content = _recording ? Locale.StopRecording : Locale.RecordSequence;
            RecordButton.Foreground = _recording ? Brushes.Red : new SolidColorBrush(ApplicationConstants.Foreground);
        }

        bool _recording = false;
        List<StoredInputBinding> pressed = new();
        private Stopwatch? _timeStopwatch;

        void BindingManagerOnOnKeyPressed(StoredInputBinding obj)
        {
            if (RecordButton.IsMouseOver && obj.Is(MouseButtons.Left))
                return;
            if (!pressed.Contains(obj))
            {
                obj.MinTime = _timeStopwatch != null ? _timeStopwatch.Elapsed.TotalSeconds : 0;
                _timeStopwatch?.Stop();
                _timeStopwatch = Stopwatch.StartNew();
                pressed.Add(obj);
            }
        }

        void BindingManager_OnKeyReleased(StoredInputBinding obj)
        {
            if (pressed.Contains(obj))
            {
                var key = pressed[pressed.IndexOf(obj)];
                Keys.Add(key);
                pressed.Remove(key);
                OnKeysChanged();
            }
        }

        private void SetRecordSequence(bool record)
        {
            if (record)
            {
                BindingManager.OnKeyPressed += BindingManagerOnOnKeyPressed;
                BindingManager.OnKeyReleased += BindingManager_OnKeyReleased;
            }
            else
            {
                _timeStopwatch?.Stop();
                _timeStopwatch = null;
                pressed.Clear();
                BindingManager.OnKeyPressed -= BindingManagerOnOnKeyPressed;
                BindingManager.OnKeyReleased -= BindingManager_OnKeyReleased;
            }

        }

        private void MultiKeyChanger_OnInitialized(object? sender, EventArgs e)
        {
            CanRecordChange();
        }
    }
}