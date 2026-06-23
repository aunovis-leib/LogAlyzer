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

namespace LogAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ViewModels.MainViewModel(Services.AppServices.AppSettings);
            // Ensure FileExplorerViewModel is initialized in MainViewModel
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