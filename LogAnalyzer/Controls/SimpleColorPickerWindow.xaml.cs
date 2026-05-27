using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LogAnalyzer.Controls;

public partial class SimpleColorPickerWindow : Window
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(string),
            typeof(SimpleColorPickerWindow),
            new FrameworkPropertyMetadata("#FFFF00", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SimpleColorPickerWindow window && e.NewValue is string hexColor)
        {
            window.UpdateHueFromHex(hexColor);
        }
    }

    private bool _isUpdatingColor = false;

    public SimpleColorPickerWindow()
    {
        InitializeComponent();
        HueSlider.Value = 0;
        UpdateGradientArea();
    }

    private void UpdateGradientArea()
    {
        _isUpdatingColor = true;

        var hue = HueSlider.Value;
        var hsv = new HSVColor(hue, 100, 100);
        var color = hsv.ToColor();

        ColorGradientArea.Background = new SolidColorBrush(color);

        _isUpdatingColor = false;
    }

    private void UpdateHueFromHex(string hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor) || !hexColor.StartsWith('#') || hexColor.Length != 7)
            return;

        try
        {
            var r = int.Parse(hexColor.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            var g = int.Parse(hexColor.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            var b = int.Parse(hexColor.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);

            var rgb = Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
            var hsv = HSVColor.FromColor(rgb);
            HueSlider.Value = hsv.H;
        }
        catch
        {
            // Invalid hex, ignore
        }
    }

    private void ColorArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ColorArea_MouseMove(sender, e);
    }

    private void ColorArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(ColorGradientArea);
        var width = ColorGradientArea.ActualWidth;
        var height = ColorGradientArea.ActualHeight;

        var saturation = (pos.X / width) * 100;
        var brightness = 100 - (pos.Y / height) * 100;

        saturation = Math.Max(0, Math.Min(100, saturation));
        brightness = Math.Max(0, Math.Min(100, brightness));

        var hue = HueSlider.Value;
        var hsv = new HSVColor(hue, saturation, brightness);
        var color = hsv.ToColor();

        SelectedColor = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
    }

    private void HueSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        HueSlider_PreviewMouseMove(sender, e);
    }

    private void HueSlider_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        UpdateGradientArea();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// Represents a color in HSV (Hue, Saturation, Value) color space
/// </summary>
public class HSVColor
{
    public double H { get; set; } // 0-360
    public double S { get; set; } // 0-100
    public double V { get; set; } // 0-100

    public HSVColor(double h, double s, double v)
    {
        H = h;
        S = s;
        V = v;
    }

    public Color ToColor()
    {
        var h = H;
        var s = S / 100.0;
        var v = V / 100.0;

        var c = v * s;
        var hPrime = h / 60.0;
        var x = c * (1.0 - Math.Abs(hPrime % 2.0 - 1.0));
        var m = v - c;

        double r, g, b;

        if (hPrime < 1)
        {
            r = c;
            g = x;
            b = 0;
        }
        else if (hPrime < 2)
        {
            r = x;
            g = c;
            b = 0;
        }
        else if (hPrime < 3)
        {
            r = 0;
            g = c;
            b = x;
        }
        else if (hPrime < 4)
        {
            r = 0;
            g = x;
            b = c;
        }
        else if (hPrime < 5)
        {
            r = x;
            g = 0;
            b = c;
        }
        else
        {
            r = c;
            g = 0;
            b = x;
        }

        return Color.FromArgb(
            255,
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255)
        );
    }

    public static HSVColor FromColor(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(Math.Max(r, g), b);
        var min = Math.Min(Math.Min(r, g), b);
        var c = max - min;

        double h = 0;
        if (c > 0)
        {
            if (max == r)
            {
                h = 60.0 * (((g - b) / c) % 6.0);
            }
            else if (max == g)
            {
                h = 60.0 * (((b - r) / c) + 2.0);
            }
            else
            {
                h = 60.0 * (((r - g) / c) + 4.0);
            }

            if (h < 0)
                h += 360;
        }

        var s = max > 0 ? (c / max) * 100.0 : 0;
        var v = max * 100.0;

        return new HSVColor(h, s, v);
    }
}
