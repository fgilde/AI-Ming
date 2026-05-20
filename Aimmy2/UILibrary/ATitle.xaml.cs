using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aimmy2.Config;
using Aimmy2.Extensions;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ATitle.xaml
    /// </summary>
    public partial class ATitle : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty IsExpanderProperty =
            DependencyProperty.Register(nameof(IsExpander), typeof(bool), typeof(ATitle),
                new PropertyMetadata(false));

        public bool IsExpander
        {
            get => (bool)GetValue(IsExpanderProperty);
            set => SetValue(IsExpanderProperty, value);
        }

        private string _titleText = "Title";
        private bool _isExpanded = true;

        public ATitle() : this("Title", false) { }

        public ATitle(string text, bool minimizable = false)
        {
            InitializeComponent();
            DataContext = this;

            _titleText = text;
            LabelTitle.Content = text;
            IsExpander = minimizable;

            if (minimizable)
            {
                ChevronContainer.Visibility = Visibility.Visible;
                this.InitWith(async _ =>
                {
                    await Task.Delay(50);
                    var initiallyExpanded = !AppConfig.Current.MinimizeState.IsMinimized(_titleText);
                    ApplyState(initiallyExpanded, animate: false);
                });
            }
        }

        private void HeaderBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsExpander) return;
            var nowExpanded = !_isExpanded;
            AppConfig.Current.MinimizeState.SetMinimized(_titleText, !nowExpanded);
            ApplyState(nowExpanded, animate: true);
        }

        private void ApplyState(bool expanded, bool animate)
        {
            _isExpanded = expanded;
            var duration = TimeSpan.FromMilliseconds(animate ? 220 : 0);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var chevronAnim = new DoubleAnimation
            {
                To = expanded ? 0 : -90,
                Duration = duration,
                EasingFunction = ease
            };
            ChevronRotate.BeginAnimation(RotateTransform.AngleProperty, chevronAnim);

            if (Parent is not Panel parent)
                return;

            var index = parent.Children.IndexOf(this);
            for (var i = index + 1; i < parent.Children.Count; i++)
            {
                if (parent.Children[i] is ATitle)
                    break; // stop at the next section header
                if (parent.Children[i] is FrameworkElement fe)
                    AnimateChild(fe, expanded, duration, ease);
            }
        }

        private static void AnimateChild(FrameworkElement element, bool expanded, TimeSpan duration, IEasingFunction ease)
        {
            var scale = EnsureLayoutScaleTransform(element);

            if (expanded)
            {
                element.Visibility = Visibility.Visible;
            }

            var anim = new DoubleAnimation
            {
                To = expanded ? 1 : 0,
                Duration = duration,
                EasingFunction = ease
            };
            if (!expanded)
            {
                anim.Completed += (_, _) =>
                {
                    if (Math.Abs(scale.ScaleY) < 0.001)
                        element.Visibility = Visibility.Collapsed;
                };
            }
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

            var fade = new DoubleAnimation
            {
                To = expanded ? 1 : 0,
                Duration = duration,
                EasingFunction = ease
            };
            element.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private static ScaleTransform EnsureLayoutScaleTransform(FrameworkElement element)
        {
            if (element.LayoutTransform is ScaleTransform existing)
                return existing;
            var scale = new ScaleTransform(1, 1);
            element.LayoutTransform = scale;
            return scale;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
