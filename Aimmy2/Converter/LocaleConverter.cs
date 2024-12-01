using System.Windows.Data;
using Nextended.Core.Extensions;

namespace Aimmy2.Converter;

public class LocaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (parameter is string propertyName)
        {
            if(propertyName.Contains(","))
            {
                var split = propertyName.Split(',');
                return Locale.GetString(split[0]).FormatWith(split.Skip(1).Select(n => Locale.GetString(n)).ToArray());
            }
            return Locale.GetString(propertyName);
        }

        return value ?? parameter;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
