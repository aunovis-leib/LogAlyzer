using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using LogAnalyzer.Services.Parsing;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Diagnostics;
using LogAnalyzer.Views;
using System.Windows;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LogAnalyzer.ViewModels;

public partial class LogListViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly AppSettingsManager _appSettings;
    private CancellationTokenSource? _loadCancellation;
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private readonly LogPatternService? _patternService;
    private string[] _currentLoadedFiles = [];
    private FileSystemWatcher? _fileSystemWatcher;
    private DispatcherTimer? _debounceTimer;
    private DispatcherTimer? _filterDebounceTimer;
    private readonly Dictionary<string, long> _filePositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _partialLineBuffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LogFileEntry> _incompleteEntryPerFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _incompleteEntryDetailsPerFile = new(StringComparer.OrdinalIgnoreCase);

    public FileExplorerViewModel FileExplorerVM { get; } = new();

    public SettingsViewModel? Settings { get; private set; }

    [ObservableProperty]
    private ParserProfile? _selectedProfile;

    public event EventHandler? EntriesReloaded;
    public event EventHandler<LogFileEntry?>? EntrySelected;
    public event EventHandler<LogType>? TypesChanged;
    public event EventHandler? OpenSettingsRequested;
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

    public int ReapplyPatternToLoadedEntries(string patternId)
    {
        if (_patternService == null || string.IsNullOrWhiteSpace(patternId))
        {
            return 0;
        }

        var matchCount = 0;
        foreach (var entry in LogFilesEntries)
        {
            try
            {
                var matches = _patternService.MatchLine(entry, patternId);
                matchCount += matches.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Pattern Error] Fehler beim Re-Apply von '{patternId}': {ex.Message}");
            }
        }

        return matchCount;
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
    private TimeOnly? _filterFromTime;

    [ObservableProperty]
    private TimeOnly? _filterToTime;

    [ObservableProperty]
    private string _filterFromTimeText = string.Empty;

    [ObservableProperty]
    private string _filterToTimeText = string.Empty;

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

    private readonly Dictionary<string, List<string>> _validationErrors = new();
    private static readonly string[] TimeInputFormats = ["HH:mm:ss", "HH:mm"];

    public bool HasErrors => _validationErrors.Count != 0;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return _validationErrors.SelectMany(x => x.Value);
        }

        return _validationErrors.TryGetValue(propertyName, out var errors)
            ? errors
            : Enumerable.Empty<string>();
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
        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AddToPattern(object? selection)
    {
        var selectedEntries = GetSelectedEntries(selection);
        if (selectedEntries.Count == 0)
        {
            return;
        }

        var firstEntry = selectedEntries[0];

        var patternService = App.PatternService;
        if (patternService == null)
        {
            MessageBox.Show("Pattern Service not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var generatedId = $"pattern_{Guid.NewGuid().ToString()[..8]}";
        var editorVM = new PatternEditorViewModel(patternService);
        editorVM.CurrentPattern.Id = generatedId;
        editorVM.CurrentPattern.Name = generatedId;
        editorVM.CurrentPattern.Severity = firstEntry.Type switch
        {
            LogType.Error => "error",
            LogType.Warning => "warning",
            LogType.Debug => "debug",
            LogType.Info => "info",
            _ => "info"
        };

        var selectedMainLines = selectedEntries
            .Select(e => !string.IsNullOrWhiteSpace(e.RawLine) ? e.RawLine : e.Text)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var escapedPatterns = selectedMainLines
            .Select(Regex.Escape)
            .ToList();

        editorVM.CurrentPattern.RegexPattern = escapedPatterns.Count switch
        {
            0 => string.Empty,
            1 => escapedPatterns[0],
            _ => $"(?:{string.Join("|", escapedPatterns)})"
        };

        editorVM.TestLine = selectedMainLines.FirstOrDefault() ?? string.Empty;

        var editorWindow = new Window
        {
            Title = "Log Pattern Editor",
            Width = 1000,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new PatternEditorView { DataContext = editorVM }
        };

        editorWindow.ShowDialog();
    }

    [RelayCommand]
    private void CopyEntryText(object? selection)
    {
        var selectedEntries = GetSelectedEntries(selection);
        if (selectedEntries.Count == 0)
        {
            return;
        }

        var textToCopy = string.Join(Environment.NewLine, selectedEntries
            .Select(entry => string.IsNullOrWhiteSpace(entry.Text) ? entry.RawLine : entry.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(textToCopy))
        {
            return;
        }

        Clipboard.SetText(textToCopy);
    }

    private static List<LogFileEntry> GetSelectedEntries(object? selection)
    {
        if (selection is IEnumerable<LogFileEntry> typedSelection)
        {
            return typedSelection.Where(x => x is not null).ToList();
        }

        if (selection is IEnumerable enumerable)
        {
            return enumerable.Cast<object>()
                .OfType<LogFileEntry>()
                .ToList();
        }

        if (selection is LogFileEntry singleEntry)
        {
            return [singleEntry];
        }

        return [];
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

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ChooseFile()
    {

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
        _patternService = App.PatternService;
        _selectedProfile = selectedProfile;
        Settings = settingsViewModel;
        _patternService = App.PatternService;  // Pattern Service laden

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
                            entry.LineNumber = LogFilesEntries.Count + 1;
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
        if (filePaths is null || filePaths.Count == 0)
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

        var currentLoadCancellation = new CancellationTokenSource();
        var previousLoadCancellation = Interlocked.Exchange(ref _loadCancellation, currentLoadCancellation);
        previousLoadCancellation?.Cancel();
        previousLoadCancellation?.Dispose();

        await _loadSemaphore.WaitAsync();
        try
        {
            if (!ReferenceEquals(_loadCancellation, currentLoadCancellation))
            {
                return;
            }

            _currentLoadedFiles = fileNames;

            var autoReloadEnabled = _appSettings.Settings.SettingsView?.AutoReloadLogFiles ?? false;
            if (autoReloadEnabled)
            {
                StartAutoReload();
            }

            var dir = System.IO.Path.GetDirectoryName(fileNames[0]);
            if (!string.IsNullOrEmpty(dir))
            {
                FileExplorerVM.LoadItems(dir);
            }
            FileExplorerVM.SetLoadedFiles(fileNames);

            IsLoading = true;
            LoadingStatus = "Lade Logdateien...";
            var previousFilter = LogFilesView.Filter;

            DateTime? minDate = null;
            DateTime? maxDate = null;
            var observedTypes = new HashSet<LogType>();
            var token = currentLoadCancellation.Token;

            _suppressAvailableTypesUpdate = true;
            LogFilesView.Filter = null;
            LogFilesEntries.Clear();

            try
            {
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

                        e.LineNumber = loadedEntries + 1;
                        LogFilesEntries.Add(e);
                        ApplyPatternsToEntry(e);

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

                foreach (var filePath in fileNames)
                {
                    if (File.Exists(filePath))
                    {
                        _filePositions[filePath] = new FileInfo(filePath).Length;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LoadingStatus = "Ladevorgang abgebrochen.";
            }
            finally
            {
                _suppressAvailableTypesUpdate = false;
                UpdateAvailableTypes(observedTypes);
                UpdateAvailableDates(minDate, maxDate);

                LogFilesView.Filter = FilterByType;
                RefreshView();
                UpdateHighlights();
                EntriesReloaded?.Invoke(this, EventArgs.Empty);

                if (LogFilesView.Filter is null)
                {
                    LogFilesView.Filter = previousFilter ?? FilterByType;
                    RefreshView();
                }

                if (ReferenceEquals(_loadCancellation, currentLoadCancellation))
                {
                    _loadCancellation = null;
                }

                IsLoading = false;
            }
        }
        finally
        {
            currentLoadCancellation.Dispose();
            _loadSemaphore.Release();
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

    partial void OnFilterFromTimeTextChanged(string value)
    {
        if (IsLoading) return;

        if (TryParseTimeInput(value, out var parsedTime, out var normalized))
        {
            ClearErrors(nameof(FilterFromTimeText));
            FilterFromTime = parsedTime;

            if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, normalized, StringComparison.Ordinal))
            {
                FilterFromTimeText = normalized;
            }

            return;
        }

        FilterFromTime = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            ClearErrors(nameof(FilterFromTimeText));
            return;
        }

        SetErrors(nameof(FilterFromTimeText), ["Ungültige Uhrzeit. Bitte HH:mm:ss verwenden."]);
    }

    partial void OnFilterToTimeTextChanged(string value)
    {
        if (IsLoading) return;

        if (TryParseTimeInput(value, out var parsedTime, out var normalized))
        {
            ClearErrors(nameof(FilterToTimeText));
            FilterToTime = parsedTime;

            if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, normalized, StringComparison.Ordinal))
            {
                FilterToTimeText = normalized;
            }

            return;
        }

        FilterToTime = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            ClearErrors(nameof(FilterToTimeText));
            return;
        }

        SetErrors(nameof(FilterToTimeText), ["Ungültige Uhrzeit. Bitte HH:mm:ss verwenden."]);
    }

    partial void OnFilterFromTimeChanged(TimeOnly? value)
    {
        if (IsLoading) return;
        RefreshView();
    }

    partial void OnFilterToTimeChanged(TimeOnly? value)
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
        var entryTime = TimeOnly.FromDateTime(e.Date);
        if (FilterFromTime is not null && entryTime < FilterFromTime.Value) return false;
        if (FilterToTime is not null && entryTime > FilterToTime.Value) return false;

        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        // Use cached lowercase filter for comparison
        var filterLower = FilterText.Trim().ToLowerInvariant();
        return (e.Text?.Contains(filterLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (e.Detail?.Any(d => d?.Contains(filterLower, StringComparison.OrdinalIgnoreCase) ?? false) ?? false);
    }

    /// <summary>
    /// Wendet alle definierten Log-Patterns auf einen einzelnen Log-Eintrag an.
    /// Dies wird automatisch aufgerufen, wenn ein neuer Eintrag hinzugefügt wird.
    /// </summary>
    private void ApplyPatternsToEntry(LogFileEntry entry)
    {
        if (_patternService == null)
        {
            return;
        }

        try
        {
            var matches = _patternService.MatchLine(entry);

            if (matches.Any())
            {
                // Debug-Ausgabe: Patterns gefunden
                Debug.WriteLine($"[Pattern Match] {entry.Text?.Substring(0, Math.Min(60, entry.Text?.Length ?? 0))}");
                foreach (var match in matches)
                {
                    Debug.WriteLine($"  ? {match.Pattern.Name} ({match.Pattern.Severity})");
                    foreach (var field in match.ExtractedFields)
                    {
                        Debug.WriteLine($"    - {field.Key}: {field.Value}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Pattern Error] Fehler beim Anwenden von Patterns: {ex.Message}");
        }
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

    private static bool TryParseTimeInput(string? value, out TimeOnly? parsed, out string normalized)
    {
        parsed = null;
        normalized = string.Empty;

        var input = value?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        if (!TimeOnly.TryParseExact(input, TimeInputFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return false;
        }

        parsed = time;
        normalized = time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        return true;
    }

    private void SetErrors(string propertyName, List<string> errors)
    {
        _validationErrors[propertyName] = errors;
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    private void ClearErrors(string propertyName)
    {
        if (_validationErrors.Remove(propertyName))
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
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
