using System;
using System.Globalization;
using System.Windows.Data;
using LogAlyzer.Models;

namespace LogAlyzer.Converters;

public class LogTypeStringToNullableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogType type) return type.ToString();
        return "Alle";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value?.ToString();
        if (string.Equals(s, "Alle", StringComparison.OrdinalIgnoreCase)) return null!;
        if (Enum.TryParse<LogType>(s, true, out var type)) return type;
        return null!;
    }
}
