using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace LogAnalyzer.Views;

public partial class LogListView : UserControl
{
    public LogListView()
    {
        InitializeComponent();
    }

    private void GridViewColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader header)
            return;

        var sortBy = header.Tag as string;
        var listView = GetListView();
        if (listView == null || string.IsNullOrEmpty(sortBy))
            return;

        var view = CollectionViewSource.GetDefaultView(listView.ItemsSource);
        if (view == null)
            return;

        UpdateSortDescriptions(view, sortBy);
        view.Refresh();
    }

    private static void UpdateSortDescriptions(ICollectionView view, string sortBy)
    {
        var current = ListSortDirection.Ascending;
        if (view.SortDescriptions.Count > 0)
        {
            var existing = view.SortDescriptions[0];
            if (existing.PropertyName == sortBy)
            {
                current = existing.Direction == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            view.SortDescriptions.Clear();
        }
        view.SortDescriptions.Add(new SortDescription(sortBy, current));
    }

    private ListView? GetListView()
    {
        if (Content is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is ListView lv) return lv;
            }
        }
        return null;
    }
}
