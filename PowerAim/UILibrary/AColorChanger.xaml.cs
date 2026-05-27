using Nextended.Core;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using PowerAim.Extensions;

namespace PowerAim.UILibrary
{
    /// <summary>
    /// Interaction logic for AColorChanger.xaml
    /// </summary>
    public partial class AColorChanger : INotifyPropertyChanged
    {
        public Color Color
        {
            get;
            set => SetField(ref field, value);
        }

        public string Title
        {
            get;
            set => SetField(ref field, value);
        }

        /// <summary>Common quick-fill colours shown under the picker. A host can override via <see cref="SetSwatches"/>.</summary>
        private static readonly Color[] DefaultSwatches =
        [
            Colors.White, Colors.Black, Colors.Red, Colors.OrangeRed, Colors.Orange, Colors.Gold,
            Colors.Lime, Colors.Green, Colors.Cyan, Colors.DodgerBlue, Colors.Blue, Colors.BlueViolet,
            Colors.Magenta, Colors.DeepPink, Colors.Gray
        ];

        public AColorChanger()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (_, _) => Picker.SetSwatches(DefaultSwatches);
        }

        /// <summary>Replace the quick-fill swatches shown under the picker (e.g. the theme accent palette).</summary>
        public void SetSwatches(IEnumerable<Color> colors) => Picker.SetSwatches(colors);

        public AColorChanger(string title) : this()
        {
            Title = title;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AColorChanger BindTo(Expression<Func<Color>> fn)
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Color = fn.Compile()();

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Color = fn.Compile()();
                    ColorChangingBorder.Background = new SolidColorBrush(Color);
                }
            };

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Color))
                    propertyInfo.SetValue(owner, Color);
            };

            return this;
        }

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

        private void ChangeColorClick(object sender, RoutedEventArgs e)
        {
            PickerPopup.IsOpen = !PickerPopup.IsOpen;
        }
    }
}