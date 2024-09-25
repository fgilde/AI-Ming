using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using AimmyWPF.Class;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aimmy2.Types;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using System.Reflection;
using System.Windows.Controls;
using Aimmy2.Config;
using Aimmy2.Converter;
using Aimmy2.Extensions;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AToggle.xaml
    /// </summary>
    public partial class AToggle : INotifyPropertyChanged
    {
        static AToggle()
        {
            FrameworkPropertyMetadata backgroundMetadata = new FrameworkPropertyMetadata(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C")));

            FrameworkPropertyMetadata borderBrushMetadata = new FrameworkPropertyMetadata(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FFFFFFF")));

            BackgroundProperty.OverrideMetadata(typeof(AToggle), backgroundMetadata);
            BorderBrushProperty.OverrideMetadata(typeof(AToggle), borderBrushMetadata);
        }




        private static Color EnableColor => ApplicationConstants.AccentColor;
        private static Color DisableColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
        private static TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(500);

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
            ApplicationConstants.StaticPropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ApplicationConstants.AccentColor) && Checked)
                {
                    Color currentColor = (Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty);
                    SetColorAnimation(currentColor, EnableColor, AnimationDuration);
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
            ColorAnimation animation = new ColorAnimation(fromColor, toColor, duration);
            SwitchMoving.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
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
            Color currentColor = (Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty);
            SetColorAnimation(currentColor, EnableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, -1, 0));
            Activated?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, new EventArgs<bool>(true));
            OnPropertyChanged(nameof(Checked));
        }

        private void DisableSwitch()
        {
            if (!IsEnabled)
                return;
            Color currentColor = (Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty);
            SetColorAnimation(currentColor, DisableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, 16, 0));
            Deactivated?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, new EventArgs<bool>(false));
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