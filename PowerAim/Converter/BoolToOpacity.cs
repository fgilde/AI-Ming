using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;
using PowerAim.Types;
using PowerAim.Config;

namespace PowerAim.Converter;

public class BoolToOpacity : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? 1 : 0.35;
        }
        return 1;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}