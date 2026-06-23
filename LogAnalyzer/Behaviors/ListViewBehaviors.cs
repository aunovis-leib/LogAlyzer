using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LogAnalyzer.Behaviors;

public static class ListViewBehaviors
{
    public static readonly DependencyProperty ScrollToItemProperty = DependencyProperty.RegisterAttached(
        "ScrollToItem",
        typeof(object),
        typeof(ListViewBehaviors),
        new PropertyMetadata(null, OnScrollToItemChanged));

    public static void SetScrollToItem(DependencyObject element, object value)
    {
        element.SetValue(ScrollToItemProperty, value);
    }

    public static object GetScrollToItem(DependencyObject element)
    {
        return element.GetValue(ScrollToItemProperty);
    }

    public static readonly DependencyProperty FillRemainingColumnIndexProperty = DependencyProperty.RegisterAttached(
        "FillRemainingColumnIndex",
        typeof(int),
        typeof(ListViewBehaviors),
        new PropertyMetadata(-1, OnFillRemainingColumnPropertyChanged));

    public static void SetFillRemainingColumnIndex(DependencyObject element, int value)
    {
        element.SetValue(FillRemainingColumnIndexProperty, value);
    }

    public static int GetFillRemainingColumnIndex(DependencyObject element)
    {
        return (int)element.GetValue(FillRemainingColumnIndexProperty);
    }

    public static readonly DependencyProperty FillRemainingColumnMinWidthProperty = DependencyProperty.RegisterAttached(
        "FillRemainingColumnMinWidth",
        typeof(double),
        typeof(ListViewBehaviors),
        new PropertyMetadata(120d, OnFillRemainingColumnPropertyChanged));

    public static void SetFillRemainingColumnMinWidth(DependencyObject element, double value)
    {
        element.SetValue(FillRemainingColumnMinWidthProperty, value);
    }

    public static double GetFillRemainingColumnMinWidth(DependencyObject element)
    {
        return (double)element.GetValue(FillRemainingColumnMinWidthProperty);
    }

    private static void OnScrollToItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListView listView || e.NewValue is null)
            return;

        var item = e.NewValue;

        void Scroll()
        {
            listView.SelectedItem = item;
            listView.ScrollIntoView(item);
            var container = listView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
            container?.BringIntoView();
        }

        if (listView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            Scroll();
        }
        else
        {
            // Defer until UI is ready
            listView.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new System.Action(Scroll));
        }
    }

    private static void OnFillRemainingColumnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListView listView)
        {
            return;
        }

        listView.Loaded -= ListViewOnLoaded;
        listView.SizeChanged -= ListViewOnSizeChanged;

        if (GetFillRemainingColumnIndex(listView) < 0)
        {
            return;
        }

        listView.Loaded += ListViewOnLoaded;
        listView.SizeChanged += ListViewOnSizeChanged;
    }

    private static void ListViewOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListView listView)
        {
            UpdateFillColumnWidth(listView);
        }
    }

    private static void ListViewOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is ListView listView)
        {
            UpdateFillColumnWidth(listView);
        }
    }

    private static void UpdateFillColumnWidth(ListView listView)
    {
        if (listView.View is not GridView gridView)
        {
            return;
        }

        var fillColumnIndex = GetFillRemainingColumnIndex(listView);
        if (fillColumnIndex < 0 || fillColumnIndex >= gridView.Columns.Count)
        {
            return;
        }

        var scrollViewer = FindVisualChild<ScrollViewer>(listView);
        var viewportWidth = scrollViewer?.ViewportWidth;
        if (viewportWidth is null || double.IsNaN(viewportWidth.Value) || viewportWidth.Value <= 0)
        {
            return;
        }

        var fillColumn = gridView.Columns[fillColumnIndex];
        var otherColumnsWidth = 0d;

        for (var i = 0; i < gridView.Columns.Count; i++)
        {
            if (i == fillColumnIndex)
            {
                continue;
            }

            var column = gridView.Columns[i];
            if (!double.IsNaN(column.ActualWidth) && column.ActualWidth > 0)
            {
                otherColumnsWidth += column.ActualWidth;
            }
            else if (!double.IsNaN(column.Width) && column.Width > 0)
            {
                otherColumnsWidth += column.Width;
            }
        }

        var availableWidth = viewportWidth.Value - otherColumnsWidth - 8;
        var minWidth = GetFillRemainingColumnMinWidth(listView);
        fillColumn.Width = availableWidth > minWidth ? availableWidth : minWidth;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }

            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
