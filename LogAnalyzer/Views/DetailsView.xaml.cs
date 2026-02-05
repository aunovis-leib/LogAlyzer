using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace LogAnalyzer.Views;

public partial class DetailsView : UserControl
{
    public DetailsView()
    {
        InitializeComponent();
        DataContext = new ViewModels.DetailsViewModel();
    }

    private void GridViewColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is GridViewColumnHeader header)
        {
            var sortBy = header.Tag as string;
            var listView = GetListView();
            if (listView == null || string.IsNullOrEmpty(sortBy)) return;

            var view = CollectionViewSource.GetDefaultView(listView.ItemsSource);
            if (view == null) return;

            var current = ListSortDirection.Ascending;
            if (view.SortDescriptions.Count > 0)
            {
                var existing = view.SortDescriptions[0];
                if (existing.PropertyName == sortBy)
                {
                    current = existing.Direction == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                    view.SortDescriptions.Clear();
                }
                else
                {
                    view.SortDescriptions.Clear();
                }
            }

            view.SortDescriptions.Add(new SortDescription(sortBy, current));
            view.Refresh();
        }
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
