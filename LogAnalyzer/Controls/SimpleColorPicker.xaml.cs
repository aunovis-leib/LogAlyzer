using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LogAnalyzer.Controls;

public partial class SimpleColorPicker : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(string),
            typeof(SimpleColorPicker),
            new FrameworkPropertyMetadata("#FFFF00", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public SimpleColorPicker()
    {
        InitializeComponent();
    }

    private void OpenColorDialog_Click(object sender, RoutedEventArgs e)
    {
        var pickerWindow = new SimpleColorPickerWindow { Owner = Window.GetWindow(this), SelectedColor = SelectedColor };
        if (pickerWindow.ShowDialog() == true)
        {
            SelectedColor = pickerWindow.SelectedColor;
        }
    }
}
