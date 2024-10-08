using System.Globalization;
using System.Windows.Data;

namespace Aimmy2.Converter;

public class AppendTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string paramText = !string.IsNullOrEmpty(parameter?.ToString()) ? $" ({parameter})" : "";
        return $"{value}{paramText}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string paramText = !string.IsNullOrEmpty(parameter?.ToString()) ? $" ({parameter})" : "";
        return value?.ToString()?.Replace(paramText, "");
    }
}