using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace LogAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<LogListViewModel> Lists { get; } = [];

    [ObservableProperty]
    private System.DateTime? _filterDate = null;

    [ObservableProperty]
    private System.DateTime? _filterFromDate = null;

    [ObservableProperty]
    private System.DateTime? _filterToDate = null;

    public MainViewModel()
    {
        Lists.Add(new LogListViewModel());
    }

    [RelayCommand]
    private void AddList()
    {
        var vm = new LogListViewModel
        {
            FilterFromDate = FilterFromDate,
            FilterToDate = FilterToDate
        };
        Lists.Add(vm);
    }

    partial void OnFilterFromDateChanged(System.DateTime? value)
    {
        foreach (var l in Lists)
        {
            l.FilterFromDate = value;
        }
    }

    partial void OnFilterToDateChanged(System.DateTime? value)
    {
        foreach (var l in Lists)
        {
            l.FilterToDate = value;
        }
    }
}
