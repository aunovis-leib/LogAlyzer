using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading;

namespace LogAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Services.AppSettingsManager _appSettings;
    public ObservableCollection<ParserProfile> Profiles { get; }

    [ObservableProperty]
    private ParserProfile? _selectedProfile;
    public ObservableCollection<LogListViewModel> Lists { get; } = [];
    public LiveChartViewModel ChartVM { get; } = new();
    public SettingsViewModel? SettingsVM { get; private set; }
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
    public ObservableCollection<LogFileEntry> RuleMatchResults { get; } = [];

    [ObservableProperty]
    private LogFileEntry? _selectedSearchResult;

    [ObservableProperty]
    private LogFileEntry? _selectedRuleMatchResult;

    [ObservableProperty]
    private bool _isSettingsPaneOpen;

    public bool ShowSearchResultsTab => !string.IsNullOrWhiteSpace(GlobalSearchText);
    public bool ShowRuleMatchesTab => RuleMatchResults.Count > 0;

    [ObservableProperty]
    private LogFileEntry? _selectedEntryGlobal;

    public event EventHandler<LogFileEntry?>? SelectedEntryChanged;
    private readonly Dictionary<LogListViewModel, EventHandler<LogFileEntry?>> _selectedEntryHandlers = [];
    private readonly Dictionary<LogListViewModel, EventHandler<string>> _patternSavedHandlers = [];
    private readonly Dictionary<LogListViewModel, EventHandler<string>> _globalSearchRequestedHandlers = [];
    private CancellationTokenSource? _searchRefreshCancellation;
    private CancellationTokenSource? _entriesReloadCancellation;
    private const int SearchRefreshDebounceMs = 250;

    public MainViewModel(Services.AppSettingsManager appSettings)
    {
        _appSettings = appSettings;
        Profiles = [.. _appSettings.ParserProfiles];
        SelectedProfile = Profiles.FirstOrDefault();
        SettingsVM = new SettingsViewModel();
        RuleMatchResults.CollectionChanged += (_, __) => OnPropertyChanged(nameof(ShowRuleMatchesTab));
        SettingsVM.PropertyChanged += SettingsVM_PropertyChanged;
        SettingsVM.MaxEntriesPerListChanged += SettingsVM_MaxEntriesPerListChanged;
        SettingsVM.ParserProfiles.CollectionChanged += (_, __) =>
        {
            SyncProfilesFromSettings();
        };
        SyncProfilesFromSettings();

        if (App.PatternService is not null)
        {
            PatternMatchPanelVM = new PatternMatchPanelViewModel(App.PatternService);
            PatternMatchPanelVM.MatchSelected += OnPatternMatchSelected;
            PatternMatchPanelVM.IsActive = SettingsVM.ShowPatternMatchPanel;
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
        RefreshRuleMatchResults();
    }

    private void SyncProfilesFromSettings()
    {
        if (SettingsVM is null)
        {
            return;
        }

        var currentSelectedName = SelectedProfile?.Name;

        Profiles.Clear();
        foreach (var profile in SettingsVM.ParserProfiles)
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault(p => p.Name == currentSelectedName)
            ?? Profiles.FirstOrDefault();
    }

    [RelayCommand]
    private void RemoveList(LogListViewModel? listToRemove)
    {
        if (listToRemove is null)
        {
            return;
        }

        if (Lists.Count <= 1)
        {
            return;
        }

        if (!Lists.Contains(listToRemove))
        {
            return;
        }

        Lists.Remove(listToRemove);
    }

    private void SettingsVM_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ShowLiveChart))
        {
            if (SettingsVM?.ShowLiveChart == true)
            {
                RefreshChart();
            }

            return;
        }

        if (e.PropertyName == nameof(SettingsViewModel.ShowPatternMatchPanel))
        {
            if (PatternMatchPanelVM is not null && SettingsVM is not null)
            {
                PatternMatchPanelVM.IsActive = SettingsVM.ShowPatternMatchPanel;
            }

            return;
        }

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
        ScheduleSearchResultsRefresh(immediate: true);
        RefreshRuleMatchResults();
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
        // The global selection only seeds newly created lists. Each LogListViewModel keeps
        // its own parser profile so different views can display different profiles.
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
        ScheduleSearchResultsRefresh(immediate: false);
    }

    partial void OnSelectedSearchResultChanged(LogFileEntry? value)
    {
        if (value is null || ReferenceEquals(SelectedEntryGlobal, value))
        {
            return;
        }

        SelectedEntryGlobal = value;
    }

    partial void OnSelectedRuleMatchResultChanged(LogFileEntry? value)
    {
        if (value is null || ReferenceEquals(SelectedEntryGlobal, value))
        {
            return;
        }

        SelectedEntryGlobal = value;
    }

    public bool NavigateToSearchResult(LogFileEntry? entry)
    {
        if (entry is null)
        {
            return false;
        }

        SelectedEntryGlobal = entry;

        var targetList = Lists.FirstOrDefault(list => list.LogFilesEntries.Contains(entry));
        if (targetList is null)
        {
            return false;
        }

        entry.IsDetailVisible = true;
        targetList.SelectedEntry = entry;
        return true;
    }

    [RelayCommand]
    private void RunSearch()
    {
        ScheduleSearchResultsRefresh(immediate: true);
    }

    private void RefreshChart()
    {
        if (SettingsVM?.ShowLiveChart != true)
        {
            return;
        }

        var allEntries = Lists.SelectMany(l => l.LogFilesEntries).ToList();
        ChartVM.UpdateFromEntries(allEntries, FilterFromDate, FilterToDate);
    }

    partial void OnSelectedEntryGlobalChanged(LogFileEntry? value)
    {
        var syncEnabled = SettingsVM?.SyncSelectionAcrossLists ?? true;

        if (syncEnabled)
        {
            foreach (var l in Lists)
            {
                var tolerance = SettingsVM?.SyncTolerance ?? TimeSpan.Zero;
                l.SelectEntryFromOutside(value, tolerance);
            }
        }

        if (value is null)
        {
            if (SelectedSearchResult is not null)
            {
                SelectedSearchResult = null;
            }

            if (SelectedRuleMatchResult is not null)
            {
                SelectedRuleMatchResult = null;
            }
        }
        else if (SearchResults.Contains(value) && !ReferenceEquals(SelectedSearchResult, value))
        {
            SelectedSearchResult = value;
        }

        if (value is not null
            && RuleMatchResults.Contains(value)
            && !ReferenceEquals(SelectedRuleMatchResult, value))
        {
            SelectedRuleMatchResult = value;
        }

        if (syncEnabled)
        {
            // Event benachrichtigen
            SelectedEntryChanged?.Invoke(this, value);
        }
    }

    private void SubscribeToList(LogListViewModel vm)
    {
        vm.EntriesReloading += OnEntriesReloading;
        vm.EntriesReloaded += EntriesReloaded;
        vm.HighlightsUpdated += OnHighlightsUpdated;
        vm.EntrySelected += OnEntrySelected;
        vm.TypesChanged += OnListTypesChanged;
        vm.OpenSettingsRequested += OnOpenSettingsRequested;
        vm.SetGlobalSearchText = text => GlobalSearchText = text;
        EventHandler<string> patternSavedHandler = (_, patternId) => vm.ReapplyPatternToLoadedEntries(patternId);
        _patternSavedHandlers[vm] = patternSavedHandler;
        PatternSaved += patternSavedHandler;
        EventHandler<string> globalSearchRequestedHandler = (_, searchText) => GlobalSearchText = searchText;
        _globalSearchRequestedHandlers[vm] = globalSearchRequestedHandler;
        vm.GlobalSearchRequested += globalSearchRequestedHandler;
        // Bei Ereignis die Auswahl f�r diese Instanz setzen
        EventHandler<LogFileEntry?> handler = (sender, entry) => { vm.SelectedEntry = entry; };
        _selectedEntryHandlers[vm] = handler;
        SelectedEntryChanged += handler;
    }

    private void UnsubscribeFromList(LogListViewModel vm)
    {
        vm.EntriesReloading -= OnEntriesReloading;
        vm.EntriesReloaded -= EntriesReloaded;
        vm.HighlightsUpdated -= OnHighlightsUpdated;
        vm.EntrySelected -= OnEntrySelected;
        vm.OpenSettingsRequested -= OnOpenSettingsRequested;
        vm.SetGlobalSearchText = null;
        if (_patternSavedHandlers.Remove(vm, out var patternSavedHandler))
        {
            PatternSaved -= patternSavedHandler;
        }
        if (_globalSearchRequestedHandlers.Remove(vm, out var globalSearchRequestedHandler))
        {
            vm.GlobalSearchRequested -= globalSearchRequestedHandler;
        }
        // Vom Ereignis abmelden nur f�r diese Instanz
        if (_selectedEntryHandlers.Remove(vm, out var handler))
        {
            SelectedEntryChanged -= handler;
        }
    }

    private async void EntriesReloaded(object? sender, EventArgs e)
    {
        var currentReloadCancellation = ReplaceCancellationTokenSource(ref _entriesReloadCancellation);

        PatternMatchPanelVM?.ResetMatches();

        try
        {
            var entries = Lists
                .SelectMany(list => list.LogFilesEntries)
                .ToList();

            await Task.Run(() =>
            {
                foreach (var entry in entries)
                {
                    currentReloadCancellation.Token.ThrowIfCancellationRequested();
                    App.PatternService?.MatchLine(entry);
                }
            }, currentReloadCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_entriesReloadCancellation, currentReloadCancellation))
            {
                _entriesReloadCancellation = null;
            }

            currentReloadCancellation.Dispose();
        }

        RefreshChart();
        ScheduleSearchResultsRefresh(immediate: true);
        RefreshRuleMatchResults();
    }

    private void OnEntriesReloading(object? sender, EventArgs e)
    {
        RuleMatchResults.Clear();
        SelectedRuleMatchResult = null;
    }

    private void OnHighlightsUpdated(object? sender, EventArgs e)
    {
        RefreshRuleMatchResults();
    }

    private void OnListTypesChanged(object? sender, LogType selectedType)
    {
        ChartVM.TypeToShow = selectedType;
        RefreshChart();
    }

    private void RefreshRuleMatchResults()
    {
        var ruleMatches = Lists
            .SelectMany(list => list.LogFilesEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.HighlightColor))
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.LineNumber)
            .ToList();

        RuleMatchResults.Clear();
        foreach (var entry in ruleMatches)
        {
            RuleMatchResults.Add(entry);
        }

        if (RuleMatchResults.Count == 0)
        {
            SelectedRuleMatchResult = null;
        }
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

    private void ScheduleSearchResultsRefresh(bool immediate)
    {
        _ = RefreshSearchResultsAsync(immediate);
    }

    private async Task RefreshSearchResultsAsync(bool immediate)
    {
        var currentSearchCancellation = ReplaceCancellationTokenSource(ref _searchRefreshCancellation);

        try
        {
            if (!immediate)
            {
                await Task.Delay(SearchRefreshDebounceMs, currentSearchCancellation.Token);
            }

            var searchText = GlobalSearchText?.Trim();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                SearchResults.Clear();
                SelectedSearchResult = null;
                return;
            }

            var entries = Lists
                .SelectMany(l => l.LogFilesEntries)
                .ToList();

            var results = await Task.Run(() =>
            {
                static bool ContainsIgnoreCase(string? source, string value) =>
                    !string.IsNullOrEmpty(source) && source.Contains(value, StringComparison.OrdinalIgnoreCase);

                return entries
                    .Where(entry =>
                        ContainsIgnoreCase(entry.Text, searchText) ||
                        ContainsIgnoreCase(entry.RawLine, searchText) ||
                        (entry.Detail?.Any(detail => ContainsIgnoreCase(detail, searchText)) ?? false))
                    .OrderBy(entry => entry.Date)
                    .ToList();
            }, currentSearchCancellation.Token);

            SearchResults.Clear();
            foreach (var entry in results)
            {
                SearchResults.Add(entry);
            }

            if (SearchResults.Count == 0)
            {
                SelectedSearchResult = null;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_searchRefreshCancellation, currentSearchCancellation))
            {
                _searchRefreshCancellation = null;
            }

            currentSearchCancellation.Dispose();
        }
    }

    private static CancellationTokenSource ReplaceCancellationTokenSource(ref CancellationTokenSource? current)
    {
        var replacement = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref current, replacement);
        previous?.Cancel();
        previous?.Dispose();
        return replacement;
    }

    public LiveLoadedEntriesSnapshot GetLoadedEntriesSnapshot(int maxEntriesPerList = 500, int maxTotalEntries = 5000)
    {
        maxEntriesPerList = Math.Clamp(maxEntriesPerList, 1, 5000);
        maxTotalEntries = Math.Clamp(maxTotalEntries, 1, 50000);

        var listSnapshots = new List<LiveLogListSnapshot>(Lists.Count);
        var total = 0;
        var truncated = false;

        for (var listIndex = 0; listIndex < Lists.Count; listIndex++)
        {
            var list = Lists[listIndex];
            var entries = new List<LiveLogEntrySnapshot>();

            foreach (var item in list.LogFilesView)
            {
                if (item is not LogFileEntry entry)
                {
                    continue;
                }

                if (entries.Count >= maxEntriesPerList || total >= maxTotalEntries)
                {
                    truncated = true;
                    break;
                }

                entries.Add(new LiveLogEntrySnapshot(
                    entry.LineNumber,
                    entry.Date,
                    entry.IsTimeOnlyTimestamp,
                    entry.Type,
                    entry.Text,
                    entry.RawLine,
                    entry.Detail));

                total++;
            }

            listSnapshots.Add(new LiveLogListSnapshot(listIndex, entries.Count, entries));

            if (total >= maxTotalEntries)
            {
                truncated = true;
                break;
            }
        }

        return new LiveLoadedEntriesSnapshot(DateTime.UtcNow, Lists.Count, total, truncated, listSnapshots);
    }

    public LiveOpenFilesSnapshot GetOpenFilesSnapshot()
    {
        var lists = new List<LiveOpenFileListSnapshot>(Lists.Count);

        for (var listIndex = 0; listIndex < Lists.Count; listIndex++)
        {
            var list = Lists[listIndex];
            var files = list.GetLoadedFilesSnapshot();
            lists.Add(new LiveOpenFileListSnapshot(listIndex, files));
        }

        var distinctFiles = lists
            .SelectMany(x => x.Files)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LiveOpenFilesSnapshot(DateTime.UtcNow, lists, distinctFiles);
    }

    public LiveSelectedEntrySnapshot GetSelectedEntrySnapshot()
    {
        for (var listIndex = 0; listIndex < Lists.Count; listIndex++)
        {
            var selected = Lists[listIndex].SelectedEntry;
            if (selected is null)
            {
                continue;
            }

            return new LiveSelectedEntrySnapshot(
                true,
                listIndex,
                selected.LineNumber,
                selected.Date,
                selected.IsTimeOnlyTimestamp,
                selected.Type,
                selected.Text,
                selected.RawLine,
                selected.Detail);
        }

        return new LiveSelectedEntrySnapshot(false, null, null, null, null, null, null, null, null);
    }

    public bool TrySelectEntry(int listIndex, int lineNumber)
    {
        if (listIndex < 0 || listIndex >= Lists.Count)
        {
            return false;
        }

        var list = Lists[listIndex];
        var selected = list.TrySelectEntryByLineNumber(lineNumber);
        if (!selected)
        {
            return false;
        }

        SelectedEntryGlobal = list.SelectedEntry;
        return true;
    }

    public LiveSetFilterTextActionResult SetFilterText(int listIndex, string? filterText)
    {
        if (listIndex < 0 || listIndex >= Lists.Count)
        {
            return new LiveSetFilterTextActionResult(false, listIndex, null, "Invalid listIndex.");
        }

        var normalized = filterText?.Trim() ?? string.Empty;
        Lists[listIndex].FilterText = normalized;
        return new LiveSetFilterTextActionResult(true, listIndex, normalized, null);
    }

    public LiveSetTimeFilterActionResult SetTimeFilter(
        int listIndex,
        DateTime? fromDate,
        DateTime? toDate,
        TimeOnly? fromTime,
        TimeOnly? toTime)
    {
        if (listIndex < 0 || listIndex >= Lists.Count)
        {
            return new LiveSetTimeFilterActionResult(false, listIndex, null, null, null, null, "Invalid listIndex.");
        }

        var list = Lists[listIndex];
        list.FilterFromDate = fromDate;
        list.FilterToDate = toDate;
        list.FilterFromTime = fromTime;
        list.FilterToTime = toTime;

        return new LiveSetTimeFilterActionResult(true, listIndex, fromDate, toDate, fromTime, toTime, null);
    }

    public async Task<LiveLoadFilesActionResult> LoadFilesIntoListAsync(int listIndex, IReadOnlyList<string>? filePaths)
    {
        if (listIndex < 0 || listIndex >= Lists.Count)
        {
            return new LiveLoadFilesActionResult(false, listIndex, 0, "Invalid listIndex.");
        }

        var paths = filePaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (paths.Length == 0)
        {
            return new LiveLoadFilesActionResult(false, listIndex, 0, "No file paths were provided.");
        }

        var missingPath = paths.FirstOrDefault(path => !File.Exists(path));
        if (!string.IsNullOrWhiteSpace(missingPath))
        {
            return new LiveLoadFilesActionResult(false, listIndex, 0, $"File not found: {missingPath}");
        }

        await Lists[listIndex].LoadFilesFromExternalAsync(paths);
        return new LiveLoadFilesActionResult(true, listIndex, paths.Length, null);
    }
}

public sealed record LiveLoadedEntriesSnapshot(
    DateTime GeneratedAtUtc,
    int ListCount,
    int ReturnedEntryCount,
    bool Truncated,
    IReadOnlyList<LiveLogListSnapshot> Lists);

public sealed record LiveLogListSnapshot(
    int ListIndex,
    int EntryCount,
    IReadOnlyList<LiveLogEntrySnapshot> Entries);

public sealed record LiveLogEntrySnapshot(
    int LineNumber,
    DateTime Date,
    bool IsTimeOnlyTimestamp,
    LogType Type,
    string Text,
    string RawLine,
    string[] Detail);

public sealed record LiveOpenFilesSnapshot(
    DateTime GeneratedAtUtc,
    IReadOnlyList<LiveOpenFileListSnapshot> Lists,
    IReadOnlyList<string> DistinctFiles);

public sealed record LiveOpenFileListSnapshot(
    int ListIndex,
    IReadOnlyList<string> Files);

public sealed record LiveSelectedEntrySnapshot(
    bool HasSelection,
    int? ListIndex,
    int? LineNumber,
    DateTime? Date,
    bool? IsTimeOnlyTimestamp,
    LogType? Type,
    string? Text,
    string? RawLine,
    string[]? Detail);

public sealed record LiveSetFilterTextActionResult(
    bool Success,
    int ListIndex,
    string? FilterText,
    string? Error);

public sealed record LiveSetTimeFilterActionResult(
    bool Success,
    int ListIndex,
    DateTime? FromDate,
    DateTime? ToDate,
    TimeOnly? FromTime,
    TimeOnly? ToTime,
    string? Error);

public sealed record LiveLoadFilesActionResult(
    bool Success,
    int ListIndex,
    int RequestedFileCount,
    string? Error);
