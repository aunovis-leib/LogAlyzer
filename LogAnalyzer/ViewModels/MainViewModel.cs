using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using LogAnalyzer.Models;

namespace LogAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Services.AppSettingsManager _appSettings;
    public IReadOnlyList<Models.ParserProfile> Profiles { get; }

    [ObservableProperty]
    private Models.ParserProfile? _selectedProfile;
    public ObservableCollection<LogListViewModel> Lists { get; } = [];
    public LiveChartViewModel ChartVM { get; } = new();

    [ObservableProperty]
    private DateTime? _filterDate = null;

    [ObservableProperty]
    private DateTime? _filterFromDate = null;

    [ObservableProperty]
    private DateTime? _filterToDate = null;

    [ObservableProperty]
    private LogFileEntry? _selectedEntryGlobal;

    public event EventHandler<LogFileEntry?>? SelectedEntryChanged;
    private readonly Dictionary<LogListViewModel, EventHandler<LogFileEntry?>> _selectedEntryHandlers = new();

    public MainViewModel(Services.AppSettingsManager appSettings)
    {
        _appSettings = appSettings;
        Profiles = _appSettings.ParserProfiles;
        SelectedProfile = Profiles.FirstOrDefault();
        var first = new LogListViewModel(_appSettings, SelectedProfile);
        Lists.Add(first);
        SubscribeToList(first);
        Lists.CollectionChanged += Lists_CollectionChanged;
        RefreshChart();
    }

    private void Lists_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        HandleNewItems(e.NewItems);
        HandleOldItems(e.OldItems);
        RefreshChart();
    }

    private void HandleNewItems(System.Collections.IList? newItems)
    {
        if (newItems is null) return;
        foreach (var it in newItems)
        {
            if (it is LogListViewModel vm) SubscribeToList(vm);
        }
    }

    private void HandleOldItems(System.Collections.IList? oldItems)
    {
        if (oldItems is null) return;
        foreach (var it in oldItems)
        {
            if (it is LogListViewModel vm) UnsubscribeFromList(vm);
        }
    }

    [RelayCommand]
    private void AddList()
    {
        var vm = new LogListViewModel(_appSettings, SelectedProfile)
        {
            FilterFromDate = FilterFromDate,
            FilterToDate = FilterToDate
        };
        Lists.Add(vm);
        SubscribeToList(vm);
        RefreshChart();
    }

    partial void OnSelectedProfileChanged(Models.ParserProfile? value)
    {
        foreach (var l in Lists)
        {
            l.SelectedProfile = value;
        }
    }

    partial void OnFilterFromDateChanged(DateTime? value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        foreach (var l in Lists)
        {
            l.FilterFromDate = value;
        }
        RefreshChart();
    }

    partial void OnFilterToDateChanged(DateTime? value)
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

    partial void OnSelectedEntryGlobalChanged(LogFileEntry? value)
    {
        // An alle Listen anwenden
        foreach (var l in Lists)
        {
            l.SelectEntryFromOutside(value);
        }
        // Event benachrichtigen
        SelectedEntryChanged?.Invoke(this, value);
    }

    private void SubscribeToList(LogListViewModel vm)
    {
        vm.EntriesReloaded += EntriesReloaded;
        vm.EntrySelected += OnEntrySelected;
        // Bei Ereignis die Auswahl für diese Instanz setzen
        EventHandler<LogFileEntry?> handler = (sender, entry) => { vm.SelectedEntry = entry; };
        _selectedEntryHandlers[vm] = handler;
        SelectedEntryChanged += handler;
    }

    private void UnsubscribeFromList(LogListViewModel vm)
    {
        vm.EntriesReloaded -= EntriesReloaded;
        vm.EntrySelected -= OnEntrySelected;
        // Vom Ereignis abmelden nur für diese Instanz
        if (_selectedEntryHandlers.Remove(vm, out var handler))
        {
            SelectedEntryChanged -= handler;
        }
    }

    private void EntriesReloaded(object? sender, EventArgs e)
    {
        RefreshChart();
    }

    private void OnEntrySelected(object? sender, LogFileEntry? entry)
    {
        SelectedEntryGlobal = entry;
    }
}
