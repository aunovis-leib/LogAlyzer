using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using LogAnalyzer.ViewModels;

namespace LogAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<LogListViewModel> Lists { get; } = [];
    public LiveChartViewModel ChartVM { get; } = new();

    [ObservableProperty]
    private System.DateTime? _filterDate = null;

    [ObservableProperty]
    private System.DateTime? _filterFromDate = null;

    [ObservableProperty]
    private System.DateTime? _filterToDate = null;

    public MainViewModel()
    {
        var first = new LogListViewModel();
        Lists.Add(first);
        SubscribeToList(first);
        Lists.CollectionChanged += (s, e) =>
        {
            if (e.NewItems is not null)
            {
                foreach (var it in e.NewItems)
                {
                    if (it is LogListViewModel vm) SubscribeToList(vm);
                }
            }
            if (e.OldItems is not null)
            {
                foreach (var it in e.OldItems)
                {
                    if (it is LogListViewModel vm) UnsubscribeFromList(vm);
                }
            }
            RefreshChart();
        };
        RefreshChart();
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
        SubscribeToList(vm);
        RefreshChart();
    }

    partial void OnFilterFromDateChanged(System.DateTime? value)
    {
        foreach (var l in Lists)
        {
            l.FilterFromDate = value;
        }
        RefreshChart();
    }

    partial void OnFilterToDateChanged(System.DateTime? value)
    {
        foreach (var l in Lists)
        {
            l.FilterToDate = value;
        }
        RefreshChart();
    }

    private void RefreshChart()
    {
        var allEntries = Lists.SelectMany(l => l.LogFilesEntries).ToList();
        ChartVM.UpdateFromEntries(allEntries, FilterFromDate, FilterToDate);
    }

    private void SubscribeToList(LogListViewModel vm)
    {
        vm.EntriesReloaded += EntriesReloaded;
    }

    private void UnsubscribeFromList(LogListViewModel vm)
    {
        vm.EntriesReloaded -= EntriesReloaded;
    }

    private void EntriesReloaded(object? sender, System.EventArgs e)
    {
        RefreshChart();
    }
}
