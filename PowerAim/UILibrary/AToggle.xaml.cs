using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using AimmyWPF.Class;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PowerAim.Types;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using System.Reflection;
using System.Windows.Controls;
using PowerAim.Config;
using PowerAim.Converter;
using PowerAim.Extensions;

namespace PowerAim.UILibrary
{
    /// <summary>
    /// Interaction logic for AToggle.xaml
    /// </summary>
    public partial class AToggle : INotifyPropertyChanged
    {




        private static Color EnableColor => ApplicationConstants.AccentColor;
        private static Color DisableColor => ApplicationConstants.Surface3Color;
        private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(260);
        private static readonly Thickness OnPosition = new(0, 0, 3, 0);
        private static readonly Thickness OffPosition = new(0, 0, 22, 0);

        public event EventHandler<EventArgs> Activated;
        public event EventHandler<EventArgs> Deactivated;
        public event EventHandler<EventArgs<bool>> Changed;

        public double EnabledOpacity => IsEnabled ? 1 : 0.35;



        public bool Checked
        {
            get => (bool)GetValue(CheckedProperty);
            set => SetValue(CheckedProperty, value);
        }

        // Using a DependencyProperty as the backing store for Checked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CheckedProperty =
            DependencyProperty.Register(nameof(Checked), typeof(bool), typeof(AToggle), new PropertyMetadata(false, CheckedChanged));

        private static void CheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var toggle = (AToggle)d;
            if ((bool)e.NewValue)
            {
                toggle.EnableSwitch();
            }
            else
            {
                toggle.DisableSwitch();
            }
        }


        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            OnPropertyChanged(nameof(EnabledOpacity));
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(AToggle), new PropertyMetadata("Aim only when Trigger is set"));

        

        public AToggle()
        {
            InitializeComponent();
            // ensure SwitchBorder Background is a mutable SolidColorBrush we can animate
            SwitchBorder.Background = new SolidColorBrush(Checked ? EnableColor : DisableColor);
            ApplicationConstants.StaticPropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ApplicationConstants.AccentColor) && Checked)
                {
                    var current = ((SolidColorBrush)SwitchBorder.Background).Color;
                    SetColorAnimation(current, EnableColor, AnimationDuration);
                }
                else if (args.PropertyName == nameof(ApplicationConstants.Surface3Color) && !Checked)
                {
                    var current = ((SolidColorBrush)SwitchBorder.Background).Color;
                    SetColorAnimation(current, DisableColor, AnimationDuration);
                }
            };
        }


        public AToggle BindTo(Expression<Func<bool>> fn)
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Checked = fn.Compile()();

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Checked = fn.Compile()();
                }
            };

            Changed += (s, e) =>
            {
                propertyInfo.SetValue(owner, e.Value);
            };

            return this;
        }

        public AToggle(string text) : this()
        {
            Text = text;
        }

        public void SetColorAnimation(Color fromColor, Color toColor, TimeSpan duration)
        {
            var animation = new ColorAnimation(fromColor, toColor, duration);
            SwitchBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        public bool ToggleState()
        {
            Checked = !Checked;
            return Checked;
        }

        private void EnableSwitch()
        {
            if (!IsEnabled)
                return;
            var currentColor = ((SolidColorBrush)SwitchBorder.Background).Color;
            SetColorAnimation(currentColor, EnableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, OnPosition);
            Activated?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, new(true));
            OnPropertyChanged(nameof(Checked));
        }

        private void DisableSwitch()
        {
            if (!IsEnabled)
                return;
            var currentColor = ((SolidColorBrush)SwitchBorder.Background).Color;
            SetColorAnimation(currentColor, DisableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, OffPosition);
            Deactivated?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, new(false));
            OnPropertyChanged(nameof(Checked));
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleState();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void BindActiveStateColor(StackPanel panel)
        {
            void SetColor(bool isChecked)
            {
                var title = panel.FindChildren<ATitle>().FirstOrDefault();
                var themeForActive = ThemePalette.ThemeForActive;
                if (title != null)
                {
                    title.LabelTitle.Foreground = new BoolToColorConverter().Convert(isChecked, null, null, CultureInfo.InvariantCulture) as Brush;
                }

                var solidColorBrush = new SolidColorBrush(themeForActive.MainColor)
                {
                    Opacity = (double)new BoolToOpacity().Convert(AppConfig.Current.ToggleState.GlobalActive, null, null, CultureInfo.InvariantCulture)
                };
                panel.Background = isChecked ? solidColorBrush : Brushes.Transparent;
            }
            SetColor(Checked);
            Changed += (s, e) => SetColor(e.Value);
            AppConfig.Current.ToggleState.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppConfig.Current.ToggleState.GlobalActive))
                {
                    SetColor(Checked);
                }
            };
        }
    }
}