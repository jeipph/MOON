using System.Globalization;
using System.Windows.Data;

namespace Moon.App.Converters;

/// <summary>
/// Para grupos de opciones (RadioButton): marcado si el valor (enum o texto) es igual al parámetro.
/// Al marcar uno, devuelve ese valor; al desmarcarse no cambia nada (WPF desmarca por su cuenta).
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string name && string.Equals(value.ToString(), name, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is not string name)
            return Binding.DoNothing;
        return targetType.IsEnum ? Enum.Parse(targetType, name) : name;
    }
}
