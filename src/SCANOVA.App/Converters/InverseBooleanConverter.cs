using Microsoft.UI.Xaml.Data;

namespace SCANOVA.App.Converters;

/// <summary>Inverte um <see cref="bool"/> — útil para <c>IsEnabled="{x:Bind ...NotBusy...}"</c> a partir de uma flag "ocupado".</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        !(value is bool b && b);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        !(value is bool b && b);
}
