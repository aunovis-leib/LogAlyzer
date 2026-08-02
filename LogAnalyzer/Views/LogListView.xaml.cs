using LogAnalyzer.Models;
using LogAnalyzer.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Collections;
using System.Globalization;

namespace LogAnalyzer.Views;

public partial class LogListView : UserControl
{
    private SettingsViewModel? _settingsViewModel;
    private LogListViewModel? _viewModel;
    private string? _currentCsvSortColumn;
    private ListSortDirection _currentCsvSortDirection = ListSortDirection.Ascending;

    public LogListView()
    {
        InitializeComponent();
        DataContextChanged += LogListView_DataContextChanged;
    }

    private void LogListView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CsvColumnsChanged -= OnCsvColumnsChanged;
        }

        _viewModel = DataContext as LogListViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CsvColumnsChanged += OnCsvColumnsChanged;
            RebuildColumns(_viewModel);
        }
    }

    private void OnCsvColumnsChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null)
        {
            RebuildColumns(_viewModel);
        }
    }

    /// <summary>
    /// Rebuilds the GridView columns. The fixed "#" (LineNumber) and "Date" columns are always kept.
    /// In CSV mode the remaining columns are generated dynamically from the profile configuration;
    /// otherwise the default Type/Text columns are restored.
    /// </summary>
    private void RebuildColumns(LogListViewModel vm)
    {
        var gridView = LogGridView;
        if (gridView is null)
        {
            return;
        }

        // Keep the first two fixed columns (# and Date), drop the rest.
        while (gridView.Columns.Count > 2)
        {
            gridView.Columns.RemoveAt(gridView.Columns.Count - 1);
        }

        // Reset any active custom (CSV) sort; the fixed columns use SortDescriptions.
        _currentCsvSortColumn = null;
        if (LogsListView?.ItemsSource is not null
            && CollectionViewSource.GetDefaultView(LogsListView.ItemsSource) is ListCollectionView lcv
            && lcv.CustomSort != null)
        {
            lcv.CustomSort = null;
        }

        if (vm.IsCsvMode)
        {
            foreach (var columnName in vm.CsvDisplayColumns)
            {
                gridView.Columns.Add(CreateDynamicColumn(columnName));
            }
        }
        else
        {
            gridView.Columns.Add(TypeColumn);
            gridView.Columns.Add(TextColumn);
        }
    }

    private GridViewColumn CreateDynamicColumn(string columnName)
    {
        var header = new GridViewColumnHeader
        {
            Content = columnName,
            Tag = columnName
        };
        header.Click += GridViewColumnHeader_Click;

        return new GridViewColumn
        {
            Header = header,
            DisplayMemberBinding = new Binding($"[{columnName}]")
        };
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

        // Dynamic CSV columns cannot be sorted via SortDescriptions (indexer path),
        // so use a custom comparer on the underlying ListCollectionView.
        if (_viewModel?.IsCsvMode == true
            && _viewModel.CsvDisplayColumns.Contains(sortBy)
            && view is ListCollectionView csvView)
        {
            var direction = ListSortDirection.Ascending;
            if (string.Equals(_currentCsvSortColumn, sortBy, System.StringComparison.Ordinal))
            {
                direction = _currentCsvSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }

            _currentCsvSortColumn = sortBy;
            _currentCsvSortDirection = direction;

            csvView.SortDescriptions.Clear();
            csvView.CustomSort = new CsvFieldComparer(sortBy, direction);
            return;
        }

        // Fixed columns: clear any active custom sort before using SortDescriptions.
        if (view is ListCollectionView lcv && lcv.CustomSort != null)
        {
            lcv.CustomSort = null;
            _currentCsvSortColumn = null;
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

    private void ListViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem item)
        {
            return;
        }

        if (!item.IsSelected)
        {
            item.IsSelected = true;
        }

        item.Focus();
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

    private void ApplyGlobalSearchFromSelection_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LogListViewModel vm)
        {
            return;
        }

        var selectedEntries = LogsListView.SelectedItems
            .Cast<object>()
            .OfType<LogFileEntry>()
            .ToList();

        object? parameter = selectedEntries.Count switch
        {
            0 => LogsListView.SelectedItem as LogFileEntry,
            1 => selectedEntries[0],
            _ => selectedEntries
        };

        if (vm.ApplyGlobalSearchTextCommand.CanExecute(parameter))
        {
            vm.ApplyGlobalSearchTextCommand.Execute(parameter);
        }
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

    /// <summary>
    /// Sorts <see cref="LogFileEntry"/> instances by a dynamic CSV field value.
    /// Values that parse as numbers are compared numerically, otherwise case-insensitively.
    /// </summary>
    private sealed class CsvFieldComparer : IComparer
    {
        private readonly string _column;
        private readonly int _sign;

        public CsvFieldComparer(string column, ListSortDirection direction)
        {
            _column = column;
            _sign = direction == ListSortDirection.Ascending ? 1 : -1;
        }

        public int Compare(object? x, object? y)
        {
            var a = (x as LogFileEntry)?[_column] ?? string.Empty;
            var b = (y as LogFileEntry)?[_column] ?? string.Empty;

            if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out var da)
                && double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out var db))
            {
                return _sign * da.CompareTo(db);
            }

            return _sign * string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
