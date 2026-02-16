using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LogAnalyzer.Behaviors
{
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
    }
}
