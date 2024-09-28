using System.Windows;
using System.Windows.Data;
using Aimmy2.Config;
using Nextended.Core.Extensions;

namespace Aimmy2.Converter;

public class ShowHelpTextsVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return AppConfig.Current?.ToggleState?.ShowHelpTexts == true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
