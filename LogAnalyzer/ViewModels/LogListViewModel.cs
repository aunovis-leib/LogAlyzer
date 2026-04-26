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

namespace LogAnalyzer.ViewModels;

public partial class LogListViewModel : ObservableObject
{
    private readonly AppSettingsManager _appSettings;
    private CancellationTokenSource? _loadCancellation;

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
    private DateTime? _filterFromDate = null;

    [ObservableProperty]
    private DateTime? _filterToDate = null;

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

    [RelayCommand]
    private void CancelLoading()
    {
        _loadCancellation?.Cancel();
    }

    [RelayCommand]
    private void SelectEntry(LogFileEntry? entry)
    {
        if (entry is null) return;
        SelectedEntry = entry;
        EntrySelected?.Invoke(this, entry);
    }

    public void SelectEntryFromOutside(LogFileEntry? entry, TimeSpan syncTolerance)
    {
        if (entry is null) return;
        LogFileEntry? foundEntry;
        if (syncTolerance == TimeSpan.Zero)
            foundEntry = LogFilesEntries.FirstOrDefault(x => x.Date.ToString() == entry.Date.ToString());
        foundEntry = LogFilesEntries
            .OrderBy(x => Math.Abs((x.Date - entry.Date).TotalSeconds))
            .FirstOrDefault(x => Math.Abs((x.Date - entry.Date).TotalSeconds) <= syncTolerance.TotalSeconds);
        if (foundEntry is not null)
        {
            SelectedEntry = foundEntry;
        }
    }

    [RelayCommand]
    private async Task ChooseFile()
    {
        if (IsLoading)
        {
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = "Logdatei wählen",
            Filter = "Log Files (*.log)|*.log",
            Multiselect = true
        };
        if (dlg.ShowDialog() == true)
        {
            IsLoading = true;
            LoadingStatus = "Lade Logdateien...";
            _loadCancellation = new CancellationTokenSource();
            var previousFilter = LogFilesView.Filter;

            DateTime? minDate = null;
            DateTime? maxDate = null;
            var observedTypes = new HashSet<LogType>();
            try
            {
                _suppressAvailableTypesUpdate = true;
                LogFilesView.Filter = null;
                LogFilesEntries.Clear();
                var parser = GetActiveParser();
                var loader = new LogFileChunkLoader(parser);
                var maxEntries = Math.Max(1, _appSettings.Settings.SettingsView?.MaxEntriesPerList ?? int.MaxValue);
                var loadedEntries = 0;

                await foreach (var chunk in loader.LoadAsync(dlg.FileNames, 2000, _loadCancellation.Token))
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

                if (loadedEntries >= maxEntries)
                {
                    LoadingStatus = $"Maximale Eintragsanzahl erreicht ({maxEntries:N0}).";
                }

                _suppressAvailableTypesUpdate = false;
                UpdateAvailableTypes(observedTypes);
                UpdateAvailableDates(minDate, maxDate);
                LogFilesView.Filter = FilterByType;
                LogFilesView.Refresh();
                EntriesReloaded?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                LoadingStatus = "Ladevorgang abgebrochen.";
                _suppressAvailableTypesUpdate = false;
                UpdateAvailableTypes(observedTypes);
                UpdateAvailableDates(minDate, maxDate);
                LogFilesView.Filter = FilterByType;
                LogFilesView.Refresh();
                EntriesReloaded?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                if (LogFilesView.Filter is null)
                {
                    LogFilesView.Filter = previousFilter ?? FilterByType;
                    LogFilesView.Refresh();
                }
                _loadCancellation?.Dispose();
                _loadCancellation = null;
                IsLoading = false;
            }
        }
    }

    private ILogParser GetActiveParser()
    {
        if (SelectedProfile is not null)
        {
            return new ProfileLogParser(SelectedProfile);
        }
        return new LegacyLogParser();
    }

    public LogListViewModel(AppSettingsManager appSettings, ParserProfile? selectedProfile)
    {
        _appSettings = appSettings;
        _selectedProfile = selectedProfile;
        LogFilesView = CollectionViewSource.GetDefaultView(LogFilesEntries);
        LogFilesView.Filter = FilterByType;
        // initialize available types with just 'Alle'
        UpdateAvailableTypes();
        // initialize available dates
        UpdateAvailableDates();
        LogFilesEntries.CollectionChanged += (_, __) =>
        {
            if (_suppressAvailableTypesUpdate) return;
            UpdateAvailableTypes();
            UpdateAvailableDates();
        };
    }

    partial void OnSelectedTypeChanged(LogType value)
    {
        if (IsLoading) return;
        LogFilesView.Filter = FilterByType;
        LogFilesView.Refresh();
        UpdateAvailableTypes();
    }

    partial void OnFilterTextChanged(string value)
    {
        if (IsLoading) return;
        LogFilesView.Refresh();
    }

    partial void OnFilterFromDateChanged(DateTime? value)
    {
        if (IsLoading) return;
        LogFilesView.Refresh();
    }

    partial void OnFilterToDateChanged(DateTime? value)
    {
        if (IsLoading) return;
        LogFilesView.Refresh();
    }

    private bool FilterByType(object obj)
    {
        if (obj is not LogFileEntry e) return false;
        var typeOk = SelectedType == LogType.All || e.Type == SelectedType;
        if (!typeOk) return false;
        if (FilterFromDate is not null && e.Date.Date < FilterFromDate.Value.Date) return false;
        if (FilterToDate is not null && e.Date.Date > FilterToDate.Value.Date) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        return (e.Text?.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
    }

    private void UpdateAvailableTypes(IEnumerable<LogType>? observedTypes = null)
    {
        if (_suppressAvailableTypesUpdate) return;
        // Build distinct types from current entries
        var types = (observedTypes ?? LogFilesEntries.Select(x => x.Type))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        // Always include Alle as first entry
        AvailableTypes.Clear();
        AvailableTypes.Add(LogType.All);
        foreach (var t in types)
        {
            AvailableTypes.Add(t);
        }

        // Ensure SelectedType is valid; reset to All if not present
        if (SelectedType != LogType.All && !types.Contains(SelectedType))
        {
            SelectedType = LogType.All;
        }

        OnPropertyChanged(nameof(SelectedType));
        // notify subscribers (e.g. main VM) about available types
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
}
