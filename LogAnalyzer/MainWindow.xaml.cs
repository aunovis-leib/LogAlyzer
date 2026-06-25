using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Collections.Specialized;
using LogAnalyzer.ViewModels;

namespace LogAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel? _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += MainWindow_DataContextChanged;
            DataContext = new MainViewModel(Services.AppServices.AppSettings);
            AttachMainViewModel(DataContext as MainViewModel);
            RebuildLogListsHost();
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachMainViewModel(e.NewValue as MainViewModel);
            RebuildLogListsHost();
        }

        private void AttachMainViewModel(MainViewModel? vm)
        {
            if (_mainViewModel is not null)
            {
                _mainViewModel.Lists.CollectionChanged -= Lists_CollectionChanged;
            }

            _mainViewModel = vm;

            if (_mainViewModel is not null)
            {
                _mainViewModel.Lists.CollectionChanged += Lists_CollectionChanged;
            }
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
                Grid.SetColumn(presenter, contentColumnIndex);
                LogListsHost.Children.Add(presenter);

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

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.ShowSearchResultsTab))
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
        }
    }

    }
}