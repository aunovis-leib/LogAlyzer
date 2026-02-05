using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace LogAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<LogListViewModel> Lists { get; } = new();

    public MainViewModel()
    {
        Lists.Add(new LogListViewModel());
    }

    [RelayCommand]
    private void AddList()
    {
        Lists.Add(new LogListViewModel());
    }
}
