using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;
using Aimmy2.Types;
using Aimmy2.Config;

namespace Aimmy2.Converter;

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