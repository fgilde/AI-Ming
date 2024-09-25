using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;
using Aimmy2.Types;

namespace Aimmy2.Converter;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            var themeForActive = ThemePalette.ThemeForActive;
            return b ? new SolidColorBrush(themeForActive.AccentColor) : new SolidColorBrush(ApplicationConstants.Foreground);
        }
        return new SolidColorBrush(ApplicationConstants.Foreground);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}