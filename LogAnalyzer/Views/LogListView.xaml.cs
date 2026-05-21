using LogAnalyzer.Models;
using LogAnalyzer.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LogAnalyzer.Views;

public partial class LogListView : UserControl
{
    private SettingsViewModel? _settingsViewModel;

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

        // Get settings from data context if available
        if (_settingsViewModel == null && DataContext is LogListViewModel vm)
        {
            _settingsViewModel = vm.Settings;
        }

        UpdateSortDescriptions(view, sortBy, _settingsViewModel);
        view.Refresh();
    }

    private static void UpdateSortDescriptions(ICollectionView view, string sortBy, SettingsViewModel? settings)
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
        else if (sortBy == "Date" && settings != null)
        {
            // For initial Date column sort, use the setting
            current = settings.DateSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
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

    private void ListViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem item || item.DataContext is not LogFileEntry entry)
        {
            return;
        }

        if (DataContext is LogListViewModel vm && vm.SelectEntryCommand.CanExecute(entry))
        {
            vm.SelectEntryCommand.Execute(entry);
            e.Handled = true;
        }
    }

    private void DetailTextBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    // Programmatically select an entry and scroll it into view
    public void SelectAndScrollTo(LogFileEntry entry)
    {
        if (entry == null) return;

        // Update selection via binding and control
        LogsListView.SelectedItem = entry;

        void Scroll()
        {
            LogsListView.ScrollIntoView(entry);
            var container = LogsListView.ItemContainerGenerator.ContainerFromItem(entry) as ListViewItem;
            container?.BringIntoView();
        }

        if (LogsListView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            Scroll();
        }
        else
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new System.Action(Scroll));
        }
    }
}
