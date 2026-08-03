using LogAnalyzer.ViewModels;
using LogAnalyzer.Services.LiveIpc;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LogAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel? _mainViewModel;
        private readonly LiveToolPipeServer _liveToolPipeServer;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += MainWindow_DataContextChanged;
            Closed += MainWindow_Closed;
            DataContext = new MainViewModel(Services.AppServices.AppSettings);
            AttachMainViewModel(DataContext as MainViewModel);
            _liveToolPipeServer = new LiveToolPipeServer(HandleLiveToolRequestAsync);
            _liveToolPipeServer.Start();
            RebuildLogListsHost();
        }

        private async void MainWindow_Closed(object? sender, EventArgs e)
        {
            await _liveToolPipeServer.StopAsync();
        }

        private async Task<object> HandleLiveToolRequestAsync(LivePipeRequest request)
        {
            if (string.Equals(request.Command, "load_files", StringComparison.Ordinal))
            {
                var actionResult = await Dispatcher.InvokeAsync(async () =>
                {
                    if (_mainViewModel is null)
                    {
                        throw new InvalidOperationException("MainViewModel is not available.");
                    }

                    if (request.ListIndex is null)
                    {
                        throw new InvalidOperationException("load_files requires listIndex.");
                    }

                    return await _mainViewModel.LoadFilesIntoListAsync(request.ListIndex.Value, request.FilePaths);
                }).Task.Unwrap();

                return actionResult;
            }

            return await Dispatcher.InvokeAsync<object>(() =>
            {
                if (_mainViewModel is null)
                {
                    throw new InvalidOperationException("MainViewModel is not available.");
                }

                if (string.Equals(request.Command, "get_loaded_entries", StringComparison.Ordinal))
                {
                    var maxPerList = request.MaxEntriesPerList ?? 500;
                    var maxTotal = request.MaxTotalEntries ?? 5000;
                    return _mainViewModel.GetLoadedEntriesSnapshot(maxPerList, maxTotal);
                }

                if (string.Equals(request.Command, "get_open_files", StringComparison.Ordinal))
                {
                    return _mainViewModel.GetOpenFilesSnapshot();
                }

                if (string.Equals(request.Command, "get_selected_entry", StringComparison.Ordinal))
                {
                    return _mainViewModel.GetSelectedEntrySnapshot();
                }

                if (string.Equals(request.Command, "select_entry", StringComparison.Ordinal))
                {
                    if (request.ListIndex is null || request.LineNumber is null)
                    {
                        throw new InvalidOperationException("select_entry requires listIndex and lineNumber.");
                    }

                    var success = _mainViewModel.TrySelectEntry(request.ListIndex.Value, request.LineNumber.Value);
                    return new
                    {
                        success,
                        listIndex = request.ListIndex.Value,
                        lineNumber = request.LineNumber.Value
                    };
                }

                if (string.Equals(request.Command, "set_filter_text", StringComparison.Ordinal))
                {
                    if (request.ListIndex is null)
                    {
                        throw new InvalidOperationException("set_filter_text requires listIndex.");
                    }

                    return _mainViewModel.SetFilterText(request.ListIndex.Value, request.FilterText);
                }

                if (string.Equals(request.Command, "set_time_filter", StringComparison.Ordinal))
                {
                    if (request.ListIndex is null)
                    {
                        throw new InvalidOperationException("set_time_filter requires listIndex.");
                    }

                    return _mainViewModel.SetTimeFilter(
                        request.ListIndex.Value,
                        request.FromDate,
                        request.ToDate,
                        request.FromTime,
                        request.ToTime);
                }

                throw new InvalidOperationException($"Unknown command: {request.Command}");
            }).Task;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachMainViewModel(e.NewValue as MainViewModel);
            RebuildLogListsHost();
        }

        private void AttachMainViewModel(MainViewModel? vm)
        {
            _mainViewModel?.Lists.CollectionChanged -= Lists_CollectionChanged;

            _mainViewModel = vm;

            _mainViewModel?.Lists.CollectionChanged += Lists_CollectionChanged;
        }

        private void Lists_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildLogListsHost();
        }

        private void RebuildLogListsHost()
        {
            if (LogListsHost is null)
            {
                return;
            }

            LogListsHost.Children.Clear();
            LogListsHost.ColumnDefinitions.Clear();

            var lists = _mainViewModel?.Lists;
            if (lists is null || lists.Count == 0)
            {
                return;
            }

            var logListTemplate = TryFindResource("LogListTemplate") as DataTemplate;

            for (var i = 0; i < lists.Count; i++)
            {
                var contentColumnIndex = i * 2;
                LogListsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var presenter = new ContentPresenter
                {
                    Content = lists[i],
                    ContentTemplate = logListTemplate
                };

                var container = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x00, 0x00)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(2),
                    Margin = new Thickness(2, 0, 2, 0),
                    Child = presenter
                };

                Grid.SetColumn(container, contentColumnIndex);
                LogListsHost.Children.Add(container);

                if (i >= lists.Count - 1)
                {
                    continue;
                }

                var splitterColumnIndex = contentColumnIndex + 1;
                LogListsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var splitter = new GridSplitter
                {
                    Width = 6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ResizeDirection = GridResizeDirection.Columns,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    Background = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x00, 0x00))
                };
                Grid.SetColumn(splitter, splitterColumnIndex);
                LogListsHost.Children.Add(splitter);
            }
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SettingsToggle.IsChecked != true)
            {
                return;
            }

            if (SettingsPane.IsMouseOver || SettingsToggle.IsMouseOver)
            {
                return;
            }

            SettingsToggle.IsChecked = false;
        }

        private void BottomTabs_Loaded(object sender, RoutedEventArgs e)
        {
            SelectFirstVisibleBottomTab();

            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.PropertyChanged -= MainViewModel_PropertyChanged;
                vm.PropertyChanged += MainViewModel_PropertyChanged;
            }
        }

        private void BottomTab_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SelectFirstVisibleBottomTab();
        }

    private void SearchResultsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || SearchResultsListView.SelectedItem is not Models.LogFileEntry entry)
        {
            return;
        }

        vm.NavigateToSearchResult(entry);
        e.Handled = true;
    }

    private void RuleMatchResultsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || RuleMatchResultsListView.SelectedItem is not Models.LogFileEntry entry)
        {
            return;
        }

        vm.NavigateToSearchResult(entry);
        e.Handled = true;
    }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MainViewModel.ShowSearchResultsTab)
                || e.PropertyName == nameof(ViewModels.MainViewModel.ShowRuleMatchesTab))
            {
                SelectFirstVisibleBottomTab();
            }
        }

        private void SelectFirstVisibleBottomTab()
        {
            if (!IsLoaded)
            {
                return;
            }

            if (BottomTabs is null || !BottomTabs.IsVisible)
            {
                return;
            }

            if (BottomTabs.SelectedItem is TabItem selectedTab && selectedTab.Visibility == Visibility.Visible)
            {
                return;
            }

            if (PatternMatchTab is not null && PatternMatchTab.Visibility == Visibility.Visible)
            {
                BottomTabs.SelectedItem = PatternMatchTab;
                return;
            }

            if (LiveChartTab is not null && LiveChartTab.Visibility == Visibility.Visible)
            {
                BottomTabs.SelectedItem = LiveChartTab;
                return;
            }

            if (SearchTab is not null && SearchTab.Visibility == Visibility.Visible)
            {
                BottomTabs.SelectedItem = SearchTab;
                return;
            }

            if (RuleMatchesTab is not null && RuleMatchesTab.Visibility == Visibility.Visible)
            {
                BottomTabs.SelectedItem = RuleMatchesTab;
            }
        }

    }
}