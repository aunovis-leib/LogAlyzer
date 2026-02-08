using System;
using System.Windows;
using System.Windows.Controls;

namespace LogAnalyzer.Controls;

public partial class DateRangePicker : UserControl
{
    public DateRangePicker()
    {
        InitializeComponent();
    }

    public DateTime? FromDate
    {
        get => (DateTime?)GetValue(FromDateProperty);
        set => SetValue(FromDateProperty, value);
    }

    public static readonly DependencyProperty FromDateProperty =
        DependencyProperty.Register(
            nameof(FromDate),
            typeof(DateTime?),
            typeof(DateRangePicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public DateTime? ToDate
    {
        get => (DateTime?)GetValue(ToDateProperty);
        set => SetValue(ToDateProperty, value);
    }

    public static readonly DependencyProperty ToDateProperty =
        DependencyProperty.Register(
            nameof(ToDate),
            typeof(DateTime?),
            typeof(DateRangePicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
}
