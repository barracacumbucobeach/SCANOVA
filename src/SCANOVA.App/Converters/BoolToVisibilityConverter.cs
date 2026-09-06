using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SCANOVA.App.Converters;

/// <summary>Converte <see cref="bool"/> em <see cref="Visibility"/>. Passe "Invert" como parâmetro para inverter.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
