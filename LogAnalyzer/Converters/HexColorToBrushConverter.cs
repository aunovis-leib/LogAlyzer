using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LogAnalyzer.Converters;

public class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo cultureInfo)
    {
        if (value is not string colorString || string.IsNullOrWhiteSpace(colorString))
        {
            return Brushes.Transparent;
        }

        try
        {
            var color = ColorConverter.ConvertFromString(colorString) as Color?;
            if (color.HasValue)
            {
                return new SolidColorBrush(color.Value);
            }
        }
        catch
        {
            // Fallback bei ungültigem Format
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo cultureInfo)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color.ToString();
        }

        return string.Empty;
    }
}
