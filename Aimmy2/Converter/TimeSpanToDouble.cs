using System.Globalization;
using System.Windows.Data;

namespace Aimmy2.Converter;

public class TimeSpanToDouble: IValueConverter // Based on our configs we convert it from and to seconds
{
    // Converts a TimeSpan to a double representing seconds
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            return timeSpan.TotalSeconds;
        }

        throw new ArgumentException("Value must be a TimeSpan", nameof(value));
    }

    // Converts a double representing seconds back to a TimeSpan
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        throw new ArgumentException("Value must be a double", nameof(value));
    }
}