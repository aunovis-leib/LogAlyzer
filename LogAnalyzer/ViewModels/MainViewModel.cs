using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using System.Collections.ObjectModel;

namespace LogAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Services.AppSettingsManager _appSettings;
    public IReadOnlyList<ParserProfile> Profiles { get; }

    [ObservableProperty]
    private ParserProfile? _selectedProfile;
    public ObservableCollection<LogListViewModel> Lists { get; } = [];
    public LiveChartViewModel ChartVM { get; } = new();
    public SettingsViewModel? SettingsVM { get; private set; } = new();
    public PatternMatchPanelViewModel? PatternMatchPanelVM { get; private set; }
    public event EventHandler<string>? PatternSaved;

    [ObservableProperty]
    private bool _showLiveChart;

    [ObservableProperty]
    private DateTime? _filterDate = null;

    [ObservableProperty]
    private DateTime? _filterFromDate = null;

    [ObservableProperty]
    private DateTime? _filterToDate = null;

    [ObservableProperty]
    private string _globalSearchText = string.Empty;

    public ObservableCollection<LogFileEntry> SearchResults { get; } = [];

    [ObservableProperty]
    private LogFileEntry? _selectedSearchResult;

    [ObservableProperty]
    private bool _isSettingsPaneOpen;

    public bool ShowSearchResultsTab => !string.IsNullOrWhiteSpace(GlobalSearchText);

    [ObservableProperty]
    private LogFileEntry? _selectedEntryGlobal;

    public event EventHandler<LogFileEntry?>? SelectedEntryChanged;
    private readonly Dictionary<LogListViewModel, EventHandler<LogFileEntry?>> _selectedEntryHandlers = new();
    private readonly Dictionary<LogListViewModel, EventHandler<string>> _patternSavedHandlers = new();

    public MainViewModel(Services.AppSettingsManager appSettings)
    {
        _appSettings = appSettings;
        Profiles = _appSettings.ParserProfiles;
        SelectedProfile = Profiles.FirstOrDefault();
        SettingsVM = new SettingsViewModel();
        SettingsVM.PropertyChanged += SettingsVM_PropertyChanged;

        if (App.PatternService is not null)
        {
            PatternMatchPanelVM = new PatternMatchPanelViewModel(App.PatternService);
            PatternMatchPanelVM.MatchSelected += OnPatternMatchSelected;
            App.PatternService.PatternSaved += (_, pattern) =>
            {
                if (!string.IsNullOrWhiteSpace(pattern?.Id))
                {
                    PatternSaved?.Invoke(this, pattern.Id);
                }
            };
        }

        var first = new LogListViewModel(_appSettings, SelectedProfile, SettingsVM);
        ApplyExplorerRootFolder(first);
        Lists.Add(first);
        SubscribeToList(first);
        Lists.CollectionChanged += Lists_CollectionChanged;
        RefreshChart();
    }

    private void SettingsVM_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.ExplorerRootFolder))
        {
            return;
        }

        foreach (var list in Lists)
        {
            ApplyExplorerRootFolder(list);
        }
    }

    private async void SettingsVM_MaxEntriesPerListChanged(object? sender, int maxEntries)
    {
        foreach (var list in Lists)
        {
            await list.ReloadWithNewMaxEntriesAsync();
        }
    }

    private void ApplyExplorerRootFolder(LogListViewModel vm)
    {
        vm.FileExplorerVM.SetRootFolder(SettingsVM?.ExplorerRootFolder);
        if (SettingsVM?.ExplorerRootFolderHistory != null)
        {
            vm.FileExplorerVM.SetExplorerRootFolderHistory(SettingsVM.ExplorerRootFolderHistory);
        }
    }

    private void Lists_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        HandleNewItems(e.NewItems);
        HandleOldItems(e.OldItems);
        RefreshChart();
        RefreshSearchResults();
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
        var vm = new LogListViewModel(_appSettings, SelectedProfile, SettingsVM)
        {
            FilterFromDate = FilterFromDate,
            FilterToDate = FilterToDate
        };
        ApplyExplorerRootFolder(vm);
        Lists.Add(vm);
        SubscribeToList(vm);
        RefreshChart();
    }

    partial void OnSelectedProfileChanged(ParserProfile? value)
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

    partial void OnGlobalSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(ShowSearchResultsTab));
        RefreshSearchResults();
    }

    partial void OnSelectedSearchResultChanged(LogFileEntry? value)
    {
        if (value is null || ReferenceEquals(SelectedEntryGlobal, value))
        {
            return;
        }

        SelectedEntryGlobal = value;
    }

    [RelayCommand]
    private void RunSearch()
    {
        RefreshSearchResults();
    }

    private void RefreshChart()
    {
        var allEntries = Lists.SelectMany(l => l.LogFilesEntries).ToList();
        ChartVM.UpdateFromEntries(allEntries, FilterFromDate, FilterToDate);
    }

    partial void OnSelectedEntryGlobalChanged(LogFileEntry? value)
    {
        foreach (var l in Lists)
        {
            var tolerance = SettingsVM?.SyncTolerance ?? TimeSpan.Zero;
            l.SelectEntryFromOutside(value, tolerance);
        }

        if (value is null)
        {
            if (SelectedSearchResult is not null)
            {
                SelectedSearchResult = null;
            }
        }
        else if (SearchResults.Contains(value) && !ReferenceEquals(SelectedSearchResult, value))
        {
            SelectedSearchResult = value;
        }

        // Event benachrichtigen
        SelectedEntryChanged?.Invoke(this, value);
    }

    private void SubscribeToList(LogListViewModel vm)
    {
        vm.EntriesReloaded += EntriesReloaded;
        vm.EntrySelected += OnEntrySelected;
        vm.TypesChanged += OnListTypesChanged;
        vm.OpenSettingsRequested += OnOpenSettingsRequested;
        EventHandler<string> patternSavedHandler = (_, patternId) => vm.ReapplyPatternToLoadedEntries(patternId);
        _patternSavedHandlers[vm] = patternSavedHandler;
        PatternSaved += patternSavedHandler;
        // Bei Ereignis die Auswahl für diese Instanz setzen
        EventHandler<LogFileEntry?> handler = (sender, entry) => { vm.SelectedEntry = entry; };
        _selectedEntryHandlers[vm] = handler;
        SelectedEntryChanged += handler;
    }

    private void UnsubscribeFromList(LogListViewModel vm)
    {
        vm.EntriesReloaded -= EntriesReloaded;
        vm.EntrySelected -= OnEntrySelected;
        vm.OpenSettingsRequested -= OnOpenSettingsRequested;
        if (_patternSavedHandlers.Remove(vm, out var patternSavedHandler))
        {
            PatternSaved -= patternSavedHandler;
        }
        // Vom Ereignis abmelden nur für diese Instanz
        if (_selectedEntryHandlers.Remove(vm, out var handler))
        {
            SelectedEntryChanged -= handler;
        }
    }

    private void EntriesReloaded(object? sender, EventArgs e)
    {
        PatternMatchPanelVM?.ResetMatches();

        foreach (var list in Lists)
        {
            foreach (var entry in list.LogFilesEntries)
            {
                App.PatternService?.MatchLine(entry);
            }
        }

        RefreshChart();
        RefreshSearchResults();
    }

    private void OnListTypesChanged(object? sender, LogType selectedType)
    {
        ChartVM.TypeToShow = selectedType;
        RefreshChart();
    }

    private void OnEntrySelected(object? sender, LogFileEntry? entry)
    {
        SelectedEntryGlobal = entry;
    }

    private void OnPatternMatchSelected(object? sender, LogFileEntry? entry)
    {
        SelectedEntryGlobal = entry;
    }

    private void OnOpenSettingsRequested(object? sender, EventArgs e)
    {
        IsSettingsPaneOpen = true;
    }

    private void RefreshSearchResults()
    {
        SearchResults.Clear();

        var searchText = GlobalSearchText?.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            SelectedSearchResult = null;
            return;
        }

        static bool ContainsIgnoreCase(string? source, string value) =>
            !string.IsNullOrEmpty(source) && source.Contains(value, StringComparison.OrdinalIgnoreCase);

        var results = Lists
            .SelectMany(l => l.LogFilesEntries)
            .Where(entry =>
                ContainsIgnoreCase(entry.Text, searchText) ||
                ContainsIgnoreCase(entry.RawLine, searchText) ||
                (entry.Detail?.Any(detail => ContainsIgnoreCase(detail, searchText)) ?? false))
            .OrderBy(entry => entry.Date)
            .ToList();

        foreach (var entry in results)
        {
            SearchResults.Add(entry);
        }

        if (SearchResults.Count == 0)
        {
            SelectedSearchResult = null;
        }
    }
}
