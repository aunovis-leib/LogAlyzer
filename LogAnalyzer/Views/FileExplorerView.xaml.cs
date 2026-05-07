using LogAnalyzer.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

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
}
