using System.Windows.Controls;
using LogAnalyzer.ViewModels;
using Microsoft.Win32;
using System.Windows;

namespace LogAnalyzer.Views;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void ChooseExplorerRootFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        var startPath = string.IsNullOrWhiteSpace(vm.ExplorerRootFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : vm.ExplorerRootFolder;

        var dialog = new OpenFolderDialog
        {
            Title = "Select explorer root folder",
            InitialDirectory = startPath,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            vm.ExplorerRootFolder = dialog.FolderName;
        }
    }

    private void SetCurrentExplorerPathAsRoot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel settingsVm)
        {
            return;
        }

        if (Window.GetWindow(this)?.DataContext is not MainViewModel mainVm)
        {
            return;
        }

        var currentPath = mainVm.Lists
            .Select(x => x.FileExplorerVM.CurrentPath)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            settingsVm.ExplorerRootFolder = currentPath;
        }
    }
}
