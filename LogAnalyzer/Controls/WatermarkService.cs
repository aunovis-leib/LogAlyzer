using System.Windows;

namespace LogAnalyzer.Controls;

public static class WatermarkService
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(WatermarkService),
            new PropertyMetadata(string.Empty));

    public static void SetPlaceholder(DependencyObject element, string value)
    {
        element.SetValue(PlaceholderProperty, value);
    }

    public static string GetPlaceholder(DependencyObject element)
    {
        return (string)element.GetValue(PlaceholderProperty);
    }
}
