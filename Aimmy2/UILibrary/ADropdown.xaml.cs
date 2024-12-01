using Aimmy2.Class;
using Aimmy2.Config;
using Aimmy2.UILibrary;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for ADropdown.xaml
    /// </summary>
    public partial class ADropdown : UserControl
    {
        static ADropdown()
        {
            FrameworkPropertyMetadata backgroundMetadata = new FrameworkPropertyMetadata(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C")));

            FrameworkPropertyMetadata borderBrushMetadata = new FrameworkPropertyMetadata(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FFFFFFF")));

            BackgroundProperty.OverrideMetadata(typeof(ADropdown), backgroundMetadata);
            BorderBrushProperty.OverrideMetadata(typeof(ADropdown), borderBrushMetadata);
        }

        private string? main_dictionary_path { get; set; }

        public ADropdown(string title, string? dictionary_path = null)
        {
            InitializeComponent();
            DropdownTitle.Content = title;
            main_dictionary_path = dictionary_path;
        }

        internal ADropdown AsSimple(bool keepLabel = false)
        {
            if(!keepLabel)
                DropdownTitle.Visibility = Visibility.Collapsed;
            Margin = new Thickness(-11, 0, -11, 0);
            BorderBrush = Brushes.Transparent;
            Background = Brushes.Transparent;
            return this;
        }

        private void DropdownBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItemContent = ((ComboBoxItem)DropdownBox.SelectedItem)?.Content?.ToString();
            if (selectedItemContent != null && main_dictionary_path != null)
            {
                AppConfig.Current.DropdownState[main_dictionary_path] = selectedItemContent;
            }
        }
    }
}