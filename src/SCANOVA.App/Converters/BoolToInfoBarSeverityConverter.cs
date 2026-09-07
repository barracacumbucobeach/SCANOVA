using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace SCANOVA.App.Converters;

/// <summary>Converte <see cref="bool"/> em <see cref="InfoBarSeverity"/> — verdadeiro vira Success, falso vira Informational (usado no status de licença: ativa/não ativa).</summary>
public sealed class BoolToInfoBarSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? InfoBarSeverity.Success : InfoBarSeverity.Informational;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
