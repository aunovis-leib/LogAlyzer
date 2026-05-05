using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LogAnalyzer.ViewModels;
using LogAnalyzer.Services;

namespace LogAnalyzer.Views;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
    }

    private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
        {
            var selectedItems = ExplorerListView.SelectedItems.Cast<FileSystemItem>();
            vm.OpenSelection(selectedItems);
        }
    }

    private void ExplorerListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is FileExplorerViewModel vm)
        {
            var selectedItems = ExplorerListView.SelectedItems.Cast<FileSystemItem>();
            vm.OpenSelection(selectedItems);
            e.Handled = true;
        }
    }

    private void SaveCurrentPathAsRoot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm || string.IsNullOrWhiteSpace(vm.CurrentPath))
        {
            return;
        }

        var manager = AppSettingsManager.Instance;
        manager.Settings.SettingsView.ExplorerRootFolder = vm.CurrentPath;
        manager.Save();
        vm.SetRootFolder(vm.CurrentPath);
    }
}
