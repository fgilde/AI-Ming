using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Nextended.UI.WPF.Converters;

namespace Aimmy2.Converter;

public class BoolToVis: IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if(parameter is Visibility vis)
            return value is true ? Visibility.Visible : vis;
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return new BoolToVisibilityConverter().ConvertBack(value, targetType, parameter, culture);
    }
}