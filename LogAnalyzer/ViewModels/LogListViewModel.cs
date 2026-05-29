using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using LogAnalyzer.Services.Parsing;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;

namespace LogAnalyzer.ViewModels;

public partial class LogListViewModel : ObservableObject
{
    private readonly AppSettingsManager _appSettings;
    private CancellationTokenSource? _loadCancellation;
    private string[] _currentLoadedFiles = [];
    private FileSystemWatcher? _fileSystemWatcher;
    private Dictionary<string, long> _filePositions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _partialLineBuffers = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, LogFileEntry> _incompleteEntryPerFile = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<string>> _incompleteEntryDetailsPerFile = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer? _debounceTimer;
    private DispatcherTimer? _filterDebounceTimer;

    public FileExplorerViewModel FileExplorerVM { get; } = new();

    public SettingsViewModel? Settings { get; private set; }

    [ObservableProperty]
    private ParserProfile? _selectedProfile;

    public event EventHandler? EntriesReloaded;
    public event EventHandler<LogFileEntry?>? EntrySelected;
    public event EventHandler<LogType>? TypesChanged;
    private bool _suppressAvailableTypesUpdate;
    private ObservableCollection<LogFileEntry> _logFilesEntries = [];
    public ObservableCollection<LogFileEntry> LogFilesEntries
    {
        get => _logFilesEntries;
        set
        {
            SetProperty(ref _logFilesEntries, value);
        }
    }
    public ICollectionView LogFilesView { get; }

    [ObservableProperty]
    private LogType _selectedType = LogType.All;

    public ObservableCollection<LogType> AvailableTypes { get; } = [];

    [ObservableProperty]
    private LogFileEntry? _selectedEntry;

    [ObservableProperty]
    private string _text1 = string.Empty;

    [ObservableProperty]
    private string _text2 = string.Empty;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _filterFromTime = string.Empty;

    [ObservableProperty]
    private string _filterToTime = string.Empty;

    [ObservableProperty]
    private DateTime? _filterFromDate = null;

    [ObservableProperty]
    private DateTime? _filterToDate = null;

    private int _filteredEntryCount;
    public int FilteredEntryCount
    {
        get => _filteredEntryCount;
        set => SetProperty(ref _filteredEntryCount, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _loadingStatus = string.Empty;
    public string LoadingStatus
    {
        get => _loadingStatus;
        set => SetProperty(ref _loadingStatus, value);
    }

    private bool _hasNewEntries = false;
    public bool HasNewEntries
    {
        get => _hasNewEntries;
        set => SetProperty(ref _hasNewEntries, value);
    }

    private int _newEntriesCount = 0;
    public int NewEntriesCount
    {
        get => _newEntriesCount;
        set => SetProperty(ref _newEntriesCount, value);
    }

    [RelayCommand]
    private void CancelLoading()
    {
        _loadCancellation?.Cancel();
    }

    [RelayCommand]
    private void ClearNewEntriesNotification()
    {
        HasNewEntries = false;
        NewEntriesCount = 0;
    }

    [RelayCommand]
    private void ApplyFilterText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        FilterText = text.Trim();
    }

    [RelayCommand]
    private void SelectEntry(LogFileEntry? entry)
    {
        if (entry is null) return;
        entry.IsDetailVisible = !entry.IsDetailVisible;
        SelectedEntry = entry;
        EntrySelected?.Invoke(this, entry);
    }

    [RelayCommand]
    private void AddHighlightRuleFromEntry(LogFileEntry? entry)
    {
        if (entry is null || Settings is null) return;
        if (string.IsNullOrWhiteSpace(entry.Text)) return;

        Settings.HighlightSearchText = entry.Text;
        Settings.AddHighlightRuleCommand.Execute(null);
    }

    public void SelectEntryFromOutside(LogFileEntry? entry, TimeSpan syncTolerance)
    {
        if (entry is null) return;

        LogFileEntry? foundEntry;
        if (syncTolerance == TimeSpan.Zero)
        {
            foundEntry = LogFilesEntries.FirstOrDefault(x => x.Date == entry.Date);
        }
        else
        {
            var toleranceSeconds = syncTolerance.TotalSeconds;
            foundEntry = LogFilesEntries
                .Where(x => Math.Abs((x.Date - entry.Date).TotalSeconds) <= toleranceSeconds)
                .MinBy(x => Math.Abs((x.Date - entry.Date).TotalSeconds));
        }

        if (foundEntry is not null)
        {
            foundEntry.IsDetailVisible = true;
            SelectedEntry = foundEntry;
        }
    }

    [RelayCommand]
    private async Task ChooseFile()
    {
        if (IsLoading) return;

        var dlg = new OpenFileDialog
        {
            Title = "Logdatei wählen",
            Filter = "Log Files (*.log)|*.log",
            Multiselect = true
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        // Setze Explorer auf das Verzeichnis der ersten gewählten Datei
        if (dlg.FileNames.Length > 0)
        {
            var dir = System.IO.Path.GetDirectoryName(dlg.FileNames[0]);
            if (!string.IsNullOrEmpty(dir))
                FileExplorerVM.LoadItems(dir);
            FileExplorerVM.SetLoadedFiles(dlg.FileNames);
        }

        await LoadFilesAsync(dlg.FileNames);
    }

    private ILogParser GetActiveParser()
    {
        if (SelectedProfile is not null)
        {
            return new ProfileLogParser(SelectedProfile);
        }
        return new LegacyLogParser();
    }

    public LogListViewModel(AppSettingsManager appSettings, ParserProfile? selectedProfile, SettingsViewModel? settingsViewModel = null)
    {
        _appSettings = appSettings;
        _selectedProfile = selectedProfile;
        Settings = settingsViewModel;
        LogFilesView = CollectionViewSource.GetDefaultView(LogFilesEntries);
        LogFilesView.Filter = FilterByType;
        FileExplorerVM.FilesSelected += OnExplorerFilesSelected;
        FileExplorerVM.FileCleared += OnExplorerFileCleared;
        UpdateFilteredEntryCount();

        // Abonniere Auto-Reload Toggle Events
        if (settingsViewModel != null)
        {
            settingsViewModel.AutoReloadToggled += (sender, enabled) =>
            {
                if (enabled && _currentLoadedFiles.Length > 0)
                {
                    StartAutoReload();
                }
                else
                {
                    StopAutoReload();
                }
            };

            // Subscribe to HighlightRules collection changes
            settingsViewModel.HighlightRules.CollectionChanged += (sender, e) =>
            {
                UpdateHighlights();
            };

            // Subscribe to individual HighlightRule property changes
            settingsViewModel.HighlightRulesChanged += (sender, e) =>
            {
                UpdateHighlights();
            };
        }

        // initialize available types with just 'Alle'
        UpdateAvailableTypes();
        // initialize available dates
        UpdateAvailableDates();

        // Initialize default sort by Date
        ApplyDefaultDateSort(settingsViewModel);

        LogFilesEntries.CollectionChanged += (_, __) =>
        {
            if (_suppressAvailableTypesUpdate) return;
            UpdateAvailableTypes();
            UpdateAvailableDates();
            UpdateFilteredEntryCount();
            UpdateHighlights();
        };
    }

    private void StartAutoReload()
    {
        if (_fileSystemWatcher != null) return;

        if (_currentLoadedFiles.Length == 0) return;

        var watcherDirectory = Path.GetDirectoryName(_currentLoadedFiles[0]);
        if (string.IsNullOrEmpty(watcherDirectory))
            return;

        try
        {
            _fileSystemWatcher = new FileSystemWatcher(watcherDirectory)
            {
                Filter = "*.log",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = false
            };

            _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
        catch
        {
            _fileSystemWatcher?.Dispose();
            _fileSystemWatcher = null;
        }
    }

    private void StopAutoReload()
    {
        if (_fileSystemWatcher != null)
        {
            _fileSystemWatcher.EnableRaisingEvents = false;
            _fileSystemWatcher.Changed -= FileSystemWatcher_Changed;
            _fileSystemWatcher.Dispose();
            _fileSystemWatcher = null;
        }

        if (_debounceTimer != null)
        {
            _debounceTimer.Stop();
            _debounceTimer.Tick -= DebounceTimer_Tick;
            _debounceTimer = null;
        }

        if (_filterDebounceTimer != null)
        {
            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Tick -= (s, e) => { };
            _filterDebounceTimer = null;
        }
    }

    private HashSet<string> _pendingFileChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _changeSync = new();

    private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        if (IsLoading || _currentLoadedFiles.Length == 0)
            return;

        lock (_changeSync)
        {
            _pendingFileChanges.Add(e.FullPath);
        }

        // Sicherstellen, dass die Timer-Erstellung auf dem UI-Thread erfolgt
        App.Current.Dispatcher.Invoke(() =>
        {
            if (_debounceTimer == null)
            {
                _debounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _debounceTimer.Tick += DebounceTimer_Tick;
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        });
    }

    private async void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();

        string[] filesToProcess;
        lock (_changeSync)
        {
            filesToProcess = _pendingFileChanges.ToArray();
            _pendingFileChanges.Clear();
        }

        if (filesToProcess.Length == 0)
            return;

        var token = _loadCancellation?.Token ?? CancellationToken.None;
        int totalNewEntries = 0;

        foreach (var filePath in filesToProcess)
        {
            if (!_currentLoadedFiles.Contains(filePath, StringComparer.OrdinalIgnoreCase))
                continue;

            try
            {
                if (!File.Exists(filePath))
                    continue;

                var newEntries = await ReadAndParseAppendedEntriesAsync(filePath, token).ConfigureAwait(false);
                if (newEntries is { Count: > 0 })
                {
                    totalNewEntries += newEntries.Count;
                    await App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var maxEntries = Math.Max(1, _appSettings.Settings.SettingsView?.MaxEntriesPerList ?? int.MaxValue);
                        var remaining = Math.Max(0, maxEntries - LogFilesEntries.Count);
                        foreach (var entry in newEntries.Take(remaining))
                        {
                            LogFilesEntries.Add(entry);
                        }
                        if (newEntries.Count > 0)
                        {
                            NewEntriesCount = totalNewEntries;
                            HasNewEntries = true;
                            UpdateAvailableTypes();
                            UpdateAvailableDates();
                            UpdateHighlights();
                            RefreshView();
                            EntriesReloaded?.Invoke(this, EventArgs.Empty);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing file {filePath}: {ex}");
            }
        }
    }

    private async Task<List<LogFileEntry>> ReadAndParseAppendedEntriesAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = new List<LogFileEntry>();
        try
        {
            if (!File.Exists(filePath)) return result;

            return await Task.Run(async () =>
            {
                var entries = new List<LogFileEntry>();
                try
                {
                    long lastPos = _filePositions.TryGetValue(filePath, out var p) ? p : 0L;
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);

                    if (fs.Length < lastPos)
                    {
                        // file truncated/rotated
                        lastPos = 0;
                        _partialLineBuffers[filePath] = string.Empty;
                        _incompleteEntryPerFile.Remove(filePath);
                        _incompleteEntryDetailsPerFile.Remove(filePath);
                    }

                    fs.Seek(lastPos, SeekOrigin.Begin);
                    using var sr = new StreamReader(fs);
                    var appended = await sr.ReadToEndAsync().ConfigureAwait(false);

                    // update stored position
                    _filePositions[filePath] = fs.Position;

                    if (string.IsNullOrEmpty(appended))
                    {
                        if (!_partialLineBuffers.TryGetValue(filePath, out var existingPrefix) || string.IsNullOrEmpty(existingPrefix))
                        {
                            return entries;
                        }
                    }

                    var prefix = _partialLineBuffers.TryGetValue(filePath, out var pref) ? pref : string.Empty;
                    var combined = (prefix ?? string.Empty) + appended;
                    var lines = combined.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    var endsWithNewline = appended.EndsWith("\n") || appended.EndsWith("\r\n");

                    if (!endsWithNewline && lines.Length > 0)
                    {
                        _partialLineBuffers[filePath] = lines[^1];
                        lines = lines.Take(lines.Length - 1).ToArray();
                    }
                    else
                    {
                        _partialLineBuffers[filePath] = string.Empty;
                    }

                    // restore incomplete entry if present
                    LogFileEntry? currentEntry = null;
                    List<string>? currentDetail = null;
                    if (_incompleteEntryPerFile.TryGetValue(filePath, out var inc))
                    {
                        currentEntry = inc;
                        if (_incompleteEntryDetailsPerFile.TryGetValue(filePath, out var det))
                            currentDetail = new List<string>(det);
                    }

                    var parser = GetActiveParser();

                    foreach (var ln in lines)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (parser.TryParse(ln, out var entry))
                        {
                            if (currentEntry is not null)
                            {
                                currentEntry.Detail = currentDetail is { Count: > 0 } ? [.. currentDetail] : [];
                                entries.Add(currentEntry);
                            }
                            currentEntry = entry;
                            currentDetail = null;
                        }
                        else if (currentEntry is not null)
                        {
                            currentDetail ??= new List<string>();
                            currentDetail.Add(ln);
                        }
                        // else: no current entry and line didn't parse -> ignore
                    }

                    // finalize or keep incomplete
                    if (currentEntry is not null)
                    {
                        if (endsWithNewline)
                        {
                            currentEntry.Detail = currentDetail is { Count: > 0 } ? [.. currentDetail] : [];
                            entries.Add(currentEntry);
                            _incompleteEntryPerFile.Remove(filePath);
                            _incompleteEntryDetailsPerFile.Remove(filePath);
                        }
                        else
                        {
                            // keep as incomplete for next tick
                            _incompleteEntryPerFile[filePath] = currentEntry;
                            _incompleteEntryDetailsPerFile[filePath] = currentDetail ?? new List<string>();
                        }
                    }
                }
                catch
                {
                    // ignore parse/read errors
                }

                return entries;
            }, cancellationToken).ConfigureAwait(false) ?? result;
        }
        catch
        {
            return result;
        }
    }

    private async void OnExplorerFilesSelected(object? sender, IReadOnlyList<string> filePaths)
    {
        if (ChooseFileCommand.IsRunning || IsLoading)
        {
            return;
        }

        await LoadFilesAsync(filePaths.ToArray());
    }

    private void OnExplorerFileCleared(object? sender, string clearedFilePath)
    {
        if (!_currentLoadedFiles.Contains(clearedFilePath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        // Clear all entries since we don't have source file tracking
        // The Auto-Reload watcher will detect the file size change and handle it
        LogFilesEntries.Clear();

        // Reset file position tracking for this file
        _filePositions[clearedFilePath] = 0;
        _partialLineBuffers[clearedFilePath] = string.Empty;
        _incompleteEntryPerFile.Remove(clearedFilePath);
        _incompleteEntryDetailsPerFile.Remove(clearedFilePath);

        UpdateAvailableTypes();
        UpdateAvailableDates();
        RefreshView();
    }

    private async Task LoadFilesAsync(string[] fileNames)
    {
        if (fileNames.Length == 0)
        {
            return;
        }

        _currentLoadedFiles = fileNames;

        // Prüfe ob Auto-Reload aktiviert ist
        var autoReloadEnabled = _appSettings.Settings.SettingsView?.AutoReloadLogFiles ?? false;
        if (autoReloadEnabled)
        {
            StartAutoReload();
        }

        var dir = System.IO.Path.GetDirectoryName(fileNames[0]);
        if (!string.IsNullOrEmpty(dir))
            FileExplorerVM.LoadItems(dir);
        FileExplorerVM.SetLoadedFiles(fileNames);

        IsLoading = true;
        LoadingStatus = "Lade Logdateien...";
        var previousFilter = LogFilesView.Filter;

        DateTime? minDate = null;
        DateTime? maxDate = null;
        var observedTypes = new HashSet<LogType>();

        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;

        try
        {
            _suppressAvailableTypesUpdate = true;
            LogFilesView.Filter = null;
            LogFilesEntries.Clear();

            var parser = GetActiveParser();
            var loader = new LogFileChunkLoader(parser);
            var maxEntries = Math.Max(1, _appSettings.Settings.SettingsView?.MaxEntriesPerList ?? int.MaxValue);
            var loadedEntries = 0;

            await foreach (var chunk in loader.LoadAsync(fileNames, 2000, token))
            {
                foreach (var e in chunk.Entries)
                {
                    if (loadedEntries >= maxEntries)
                    {
                        break;
                    }

                    LogFilesEntries.Add(e);
                    loadedEntries++;
                    observedTypes.Add(e.Type);

                    var day = e.Date.Date;
                    if (minDate is null || day < minDate.Value)
                    {
                        minDate = day;
                    }

                    if (maxDate is null || day > maxDate.Value)
                    {
                        maxDate = day;
                    }
                }

                LoadingStatus = $"Geladen: {loadedEntries:N0} Einträge";

                if (loadedEntries >= maxEntries)
                {
                    break;
                }
            }

            // Speichere die aktuellen Dateipositionen nach dem Laden
            foreach (var filePath in fileNames)
            {
                if (File.Exists(filePath))
                {
                    _filePositions[filePath] = new FileInfo(filePath).Length;
                }
            }

            if (loadedEntries >= maxEntries)
            {
                LoadingStatus = $"Maximale Eintragsanzahl erreicht ({maxEntries:N0}).";
            }

            _suppressAvailableTypesUpdate = false;
            UpdateAvailableTypes(observedTypes);
            UpdateAvailableDates(minDate, maxDate);
            LogFilesView.Filter = FilterByType;
            RefreshView();
            UpdateHighlights();
            EntriesReloaded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            LoadingStatus = "Ladevorgang abgebrochen.";
            _suppressAvailableTypesUpdate = false;
            UpdateAvailableTypes(observedTypes);
            UpdateAvailableDates(minDate, maxDate);
            LogFilesView.Filter = FilterByType;
            RefreshView();
            UpdateHighlights();
            EntriesReloaded?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            if (LogFilesView.Filter is null)
            {
                LogFilesView.Filter = previousFilter ?? FilterByType;
                RefreshView();
            }

            _loadCancellation?.Dispose();
            _loadCancellation = null;
            IsLoading = false;
        }
    }

    /// <summary>
    /// Reloads the currently loaded files with a new max entries limit.
    /// Called when the MaxEntriesPerList setting changes.
    /// </summary>
    public async Task ReloadWithNewMaxEntriesAsync()
    {
        if (_currentLoadedFiles.Length == 0)
        {
            return;
        }

        await LoadFilesAsync(_currentLoadedFiles);
    }

    partial void OnSelectedTypeChanged(LogType value)
    {
        if (IsLoading) return;
        LogFilesView.Filter = FilterByType;
        RefreshView();
        UpdateAvailableTypes();
    }

    partial void OnFilterTextChanged(string value)
    {
        if (IsLoading) return;

        _filterDebounceTimer?.Stop();

        if (_filterDebounceTimer == null)
        {
            _filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _filterDebounceTimer.Tick += (s, e) =>
            {
                _filterDebounceTimer?.Stop();
                RefreshView();
            };
        }

        _filterDebounceTimer.Start();
    }

    partial void OnFilterFromTimeChanged(string value)
    {
        if (IsLoading) return;
        RefreshView();
    }

    partial void OnFilterToTimeChanged(string value)
    {
        if (IsLoading) return;
        RefreshView();
    }

    partial void OnFilterFromDateChanged(DateTime? value)
    {
        if (IsLoading) return;
        RefreshView();
    }

    partial void OnFilterToDateChanged(DateTime? value)
    {
        if (IsLoading) return;
        RefreshView();
    }

    public void UpdateHighlights()
    {
        if (Settings?.HighlightRules == null) return;

        foreach (var entry in LogFilesEntries)
        {
            var matchedRule = Settings.HighlightRules.FirstOrDefault(rule =>
                rule.IsEnabled &&
                !string.IsNullOrWhiteSpace(rule.SearchText) &&
                entry.Text.Contains(rule.SearchText, StringComparison.OrdinalIgnoreCase));

            entry.HighlightColor = matchedRule?.Color;
        }
    }

    private void RefreshView()
    {
        LogFilesView.Refresh();
        UpdateFilteredEntryCount();
    }

    private void UpdateFilteredEntryCount()
    {
        FilteredEntryCount = LogFilesView.OfType<object>().Count();
    }

    private bool FilterByType(object obj)
    {
        if (obj is not LogFileEntry e) return false;
        var typeOk = SelectedType == LogType.All || e.Type == SelectedType;
        if (!typeOk) return false;
        if (FilterFromDate is not null && e.Date.Date < FilterFromDate.Value.Date) return false;
        if (FilterToDate is not null && e.Date.Date > FilterToDate.Value.Date) return false;

        // Time range filtering
        if (!string.IsNullOrWhiteSpace(FilterFromTime) || !string.IsNullOrWhiteSpace(FilterToTime))
        {
            var entryTime = e.Date.ToString("HH:mm:ss");
            var fromTime = FilterFromTime?.Trim();
            var toTime = FilterToTime?.Trim();

            if (!string.IsNullOrEmpty(fromTime) && string.Compare(entryTime, fromTime, StringComparison.Ordinal) < 0)
                return false;

            if (!string.IsNullOrEmpty(toTime) && string.Compare(entryTime, toTime, StringComparison.Ordinal) > 0)
                return false;
        }

        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        // Use cached lowercase filter for comparison
        var filterLower = FilterText.Trim().ToLowerInvariant();
        return (e.Text?.Contains(filterLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (e.Detail?.Any(d => d?.Contains(filterLower, StringComparison.OrdinalIgnoreCase) ?? false) ?? false);
    }

    private void UpdateAvailableTypes(IEnumerable<LogType>? observedTypes = null)
    {
        if (_suppressAvailableTypesUpdate) return;

        var types = (observedTypes ?? LogFilesEntries.Select(x => x.Type))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        AvailableTypes.Clear();
        AvailableTypes.Add(LogType.All);
        foreach (var t in types)
        {
            AvailableTypes.Add(t);
        }

        if (SelectedType != LogType.All && !types.Contains(SelectedType))
        {
            SelectedType = LogType.All;
        }

        OnPropertyChanged(nameof(SelectedType));
        TypesChanged?.Invoke(this, SelectedType);
    }

    private void UpdateAvailableDates(DateTime? minDate = null, DateTime? maxDate = null)
    {
        if (_suppressAvailableTypesUpdate) return;

        if (minDate is not null && maxDate is not null)
        {
            FilterFromDate = minDate;
            FilterToDate = maxDate;
            return;
        }

        if (LogFilesEntries == null || LogFilesEntries.Count == 0)
        {
            return;
        }
        FilterFromDate = LogFilesEntries.Min(e => e.Date.Date);
        FilterToDate = LogFilesEntries.Max(e => e.Date.Date);
    }

    private void ApplyDefaultDateSort(SettingsViewModel? settingsViewModel)
    {
        if (settingsViewModel == null || LogFilesView == null)
            return;

        var sortDirection = settingsViewModel.DateSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        LogFilesView.SortDescriptions.Clear();
        LogFilesView.SortDescriptions.Add(new SortDescription(nameof(LogFileEntry.Date), sortDirection));
    }

    ~LogListViewModel()
    {
        StopAutoReload();
        if (_filterDebounceTimer != null)
        {
            _filterDebounceTimer.Stop();
            _filterDebounceTimer = null;
        }
        _loadCancellation?.Dispose();
    }
}
