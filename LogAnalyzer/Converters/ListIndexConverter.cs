using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace LogAnalyzer.Converters;

public class ListIndexConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == null || values[1] == null)
            return string.Empty;

        try
        {
            var item = values[0];
            var itemsSource = values[1];

            // Fallback: IList (am schnellsten)
            if (itemsSource is IList list)
            {
                var index = list.IndexOf(item);
                return index >= 0 ? (index + 1).ToString() : string.Empty;
            }

            // Fallback: IEnumerable (funktioniert auch mit ICollectionView über implizite Konvertierung)
            if (itemsSource is IEnumerable enumerable)
            {
                int index = 0;
                foreach (var enumItem in enumerable)
                {
                    if (ReferenceEquals(enumItem, item))
                    {
                        return (index + 1).ToString();
                    }
                    index++;
                }
            }
        }
        catch
        {
            // Fehler bei Index-Berechnung ignorieren
        }

        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
