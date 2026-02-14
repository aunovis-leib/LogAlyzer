using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using LogAnalyzer.Models;
using static LogAnalyzer.Models.LogFileEntry;
using System.Text.Json;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels;

public partial class LogListViewModel : ObservableObject
{
    private readonly AppSettingsManager _appSettings;
    private readonly List<ParserProfile> _profiles;

    [ObservableProperty]
    private ParserProfile? _selectedProfile;

    public event EventHandler? EntriesReloaded;
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
    private LogType? _selectedType = null;

    public ObservableCollection<object?> AvailableTypes { get; } = [];

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
            var entries = await Task.Run(() =>
            {
                var all = new List<LogFileEntry>();
                foreach (var fn in dlg.FileNames)
                {
                    all.AddRange(ParseLogFile(fn));
                }
                return all;
            });

            _suppressAvailableTypesUpdate = true;
            LogFilesEntries.Clear();
            foreach (var e in entries)
            {
                LogFilesEntries.Add(e);
            }
            _suppressAvailableTypesUpdate = false;
            LogFilesView.Refresh();
            UpdateAvailableTypes();
            EntriesReloaded?.Invoke(this, EventArgs.Empty);
        }
    }

    private List<LogFileEntry> ParseLogFile(string fileName)
    {
        var list = new List<LogFileEntry>();
        foreach (var line in File.ReadLines(fileName))
        {
            if (TryParseLine(line, out var entry))
            {
                entry.Detail = [];
                list.Add(entry);
            }
            else
            {
                AddLineToLastEntryDetail(list, line);
            }
        }
        return list;
    }

    private static void AddLineToLastEntryDetail(List<LogFileEntry> list, string line)
    {
        if (list.Count == 0) return;
        var last = list[^1];
        var details = last.Detail?.ToList() ?? [];
        details.Add(line);
        last.Detail = [.. details];
    }

    private bool TryParseLine(string line, out LogFileEntry entry)
    {
        entry = new LogFileEntry();
        if (string.IsNullOrWhiteSpace(line)) return false;

        // Try using selected parser profile
        if (ShouldUseSelectedProfile(out var profile) && profile is not null)
        {
            return TryParseWithProfile(line, profile, out entry);
        }

        // Fallback: legacy pipe splitter and default date format
        return TryParseLegacy(line, out entry);
    }

    private bool ShouldUseSelectedProfile(out ParserProfile? profile)
    {
        profile = null;
        if (AppSettingsManager.Instance != null && _profiles.Count > 0 && SelectedProfile is not null)
        {
            profile = SelectedProfile;
            return true;
        }
        return false;
    }

    private bool TryParseWithProfile(string line, ParserProfile profile, out LogFileEntry entry)
    {
        entry = new LogFileEntry();
        var parts = line.Split([profile.Splitter], StringSplitOptions.None);
        if (parts.Length < 3) return false;

        var datePart = parts[0].Trim();
        var typePart = parts[1].Replace("\t", string.Empty).Trim();
        var textPart = string.Join(profile.Splitter, parts[2..]).Trim();

        if (!TryParseDate(datePart, profile.DateFormat, out var dt))
            return false;

        entry.Date = dt;
        entry.Type = TryParseLogType(typePart);
        entry.Text = textPart;
        return true;
    }

    private bool TryParseLegacy(string line, out LogFileEntry entry)
    {
        entry = new LogFileEntry();
        var parts = line.Split('|');
        if (parts.Length < 3) return false;

        var datePart = parts[0].Trim();
        var typePart = parts[1].Replace("\t", string.Empty).Trim();
        var textPart = string.Join("|", parts[2..]).Trim();

        if (!TryParseDate(datePart, "dd.MM.yyyy HH:mm:ss.fff", out var dt))
            return false;

        entry.Date = dt;
        entry.Type = TryParseLogType(typePart);
        entry.Text = textPart;
        return true;
    }

    private bool TryParseDate(string datePart, string dateFormat, out DateTime dt)
    {
        if (DateTime.TryParseExact(
            datePart,
            dateFormat,
            System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
            System.Globalization.DateTimeStyles.None,
            out dt))
        {
            return true;
        }
        if (System.DateTimeOffset.TryParse(
            datePart,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var dto))
        {
            dt = dto.LocalDateTime;
            return true;
        }
        return false;
    }

    private LogType TryParseLogType(string typePart)
    {
        if (!Enum.TryParse<LogType>(typePart, true, out var type))
        {
            type = LogType.Info;
        }
        return type;
    }
    

    public LogListViewModel(AppSettingsManager appSettings, ParserProfile? selectedProfile)
    {
        _appSettings = appSettings;
        _profiles = [.. _appSettings.ParserProfiles];
        _selectedProfile = selectedProfile;
        LogFilesView = CollectionViewSource.GetDefaultView(LogFilesEntries);
        LogFilesView.Filter = FilterByType;
        // initialize available types with just 'Alle'
        UpdateAvailableTypes();
        LogFilesEntries.CollectionChanged += (_, __) =>
        {
            if (_suppressAvailableTypesUpdate) return;
            UpdateAvailableTypes();
        };
    }

    partial void OnSelectedTypeChanged(LogType? value)
    {
        LogFilesView.Filter = FilterByType;
        LogFilesView.Refresh();
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
        var typeOk = SelectedType is null || e.Type == SelectedType.Value;
        if (!typeOk) return false;
        if (FilterFromDate is not null && e.Date.Date < FilterFromDate.Value.Date) return false;
        if (FilterToDate is not null && e.Date.Date > FilterToDate.Value.Date) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        return (e.Text?.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
    }

    private void UpdateAvailableTypes()
    {
        if (_suppressAvailableTypesUpdate) return;
        // Build distinct types from current entries
        var types = LogFilesEntries
            .Select(x => x.Type)
            .Distinct()
            .OrderBy(t => t)
            .Cast<object>()
            .ToList();

        // Always include null (Alle) as first entry
        AvailableTypes.Clear();
        AvailableTypes.Add(null);
        foreach (var t in types)
        {
            AvailableTypes.Add(t);
        }

        // Ensure SelectedType is valid; reset to null if not present
        if (SelectedType is not null && !types.Contains(SelectedType.Value))
        {
            SelectedType = null;
        }
    }
}
