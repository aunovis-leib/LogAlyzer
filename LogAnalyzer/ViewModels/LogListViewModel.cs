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
        var dlg = new OpenFileDialog
        {
            Title = "Logdatei wählen",
            Filter = "Log Files (*.log)|*.log",
            Multiselect = true
        };
        if (dlg.ShowDialog() == true)
        {
            DateTime? minDate = null;
            DateTime? maxDate = null;
            var observedTypes = new HashSet<LogType>();
            _suppressAvailableTypesUpdate = true;
            LogFilesEntries.Clear();
            using (LogFilesView.DeferRefresh())
            {
                foreach (var fn in dlg.FileNames)
                {
                    var entries = await Task.Run(() => ParseLogFile(fn));
                    foreach (var e in entries)
                    {
                        LogFilesEntries.Add(e);
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
                }
            }
            _suppressAvailableTypesUpdate = false;
            UpdateAvailableTypes(observedTypes);
            UpdateAvailableDates(minDate, maxDate);
            EntriesReloaded?.Invoke(this, EventArgs.Empty);
        }
    }

    private List<LogFileEntry> ParseLogFile(string fileName)
    {
        var list = new List<LogFileEntry>();
        ILogParser parser = GetActiveParser();
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream);

        LogFileEntry? currentEntry = null;
        List<string>? currentDetail = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (parser.TryParse(line, out var entry))
            {
                if (currentEntry is not null)
                {
                    currentEntry.Detail = currentDetail is { Count: > 0 } ? [.. currentDetail] : [];
                    list.Add(currentEntry);
                }
                currentEntry = entry;
                currentDetail = null;
            }
            else
            {
                if (currentEntry is null)
                {
                    continue;
                }

                currentDetail ??= [];
                currentDetail.Add(line);
            }
        }

        if (currentEntry is not null)
        {
            currentEntry.Detail = currentDetail is { Count: > 0 } ? [.. currentDetail] : [];
            list.Add(currentEntry);
        }

        return list;
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
        LogFilesView.Filter = FilterByType;
        LogFilesView.Refresh();
        UpdateAvailableTypes();
    }

    partial void OnFilterTextChanged(string value)
    {
        LogFilesView.Refresh();
    }

    partial void OnFilterFromDateChanged(DateTime? value)
    {
        LogFilesView.Refresh();
    }

    partial void OnFilterToDateChanged(DateTime? value)
    {
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
