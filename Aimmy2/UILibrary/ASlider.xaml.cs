using Nextended.Core;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Channels;
using System.Windows.Controls;
using System.Windows.Input;
using Aimmy2.Extensions;
using System.Numerics;
using System.Windows.Media;
using System.Windows;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Controls.Primitives;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ASlider.xaml
    /// </summary>
    public partial class ASlider : UserControl
    {
        static ASlider()
        {
            FrameworkPropertyMetadata backgroundMetadata = new FrameworkPropertyMetadata(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C")));

            FrameworkPropertyMetadata borderBrushMetadata = new FrameworkPropertyMetadata(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FFFFFFF")));

            BackgroundProperty.OverrideMetadata(typeof(ASlider), backgroundMetadata);
            BorderBrushProperty.OverrideMetadata(typeof(ASlider), borderBrushMetadata);
        }

        public double Steps
        {
            get => (double)GetValue(StepsProperty);
            set => SetValue(StepsProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }


        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public double Value
        {
            get => Slider.Value;
            set => Slider.Value = value;
        }

        public double TickFrequency
        {
            get => Slider.TickFrequency;
            set => Slider.TickFrequency = value;
        }
        public double Minimum
        {
            get => Slider.Minimum;
            set => Slider.Minimum = value;
        }
        public double Maximum
        {
            get => Slider.Maximum;
            set => Slider.Maximum = value;
        }

        private static void LabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ASlider)d).OnTextOrLabelChanged();
        private static void TextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ASlider)d).OnTextOrLabelChanged();

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(ASlider), new PropertyMetadata(string.Empty, LabelChanged));
        
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ASlider), new PropertyMetadata(string.Empty, TextChanged));

        public static readonly DependencyProperty StepsProperty =
            DependencyProperty.Register(nameof(Steps), typeof(double), typeof(ASlider), new PropertyMetadata(0.01d));

        private void OnTextOrLabelChanged()
        {
            SliderTitle.Text = Label;
            AdjustNotifier.Content = $"{Slider.Value:F2} {Text}";
        }

        [Category("Behavior")]
        public event RoutedPropertyChangedEventHandler<double> ValueChanged
        {
            add => this.AddHandler(RangeBase.ValueChangedEvent, (Delegate)value);
            remove => this.RemoveHandler(RangeBase.ValueChangedEvent, (Delegate)value);
        }

        public ASlider()
        {
            InitializeComponent();
            SubtractOne.Click += (s, e) => UpdateSliderValue(-Steps);
            AddOne.Click += (s, e) => UpdateSliderValue(Steps);
            Slider.ValueChanged += (s, e) =>
            {
                AdjustNotifier.Content = $"{Slider.Value:F2} {Text}";
                RaiseEvent(new RoutedPropertyChangedEventArgs<double>(e.OldValue, e.NewValue, RangeBase.ValueChangedEvent));
            };
        }

        public ASlider(string label, string text, double steps): this()
        {
            Label = label;
            Text = text;
            Steps = steps;
            OnTextOrLabelChanged();
        }

        public ASlider BindTo<T>(Expression<Func<T>> fn) where T : struct, INumber<T>
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();
   
            Slider.Value = Convert.ToDouble(fn.Compile()());

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Slider.Value = Convert.ToDouble(fn.Compile()());
                }
            };

            Slider.ValueChanged += (s, e) =>
            {
                propertyInfo.SetValue(owner, T.CreateChecked(e.NewValue));
            };

            return this;
        }


        private void UpdateSliderValue(double change)
        {
            Slider.Value = Math.Round(Slider.Value + change, 2);
        }
    }
}