using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using LogAnalyzer.Models;

namespace LogAnalyzer.ViewModels;

public partial class LogListViewModel : ObservableObject
{
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
        }
    }

    private static List<LogFileEntry> ParseLogFile(string fileName)
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

    private static bool TryParseLine(string line, out LogFileEntry entry)
    {
        entry = new LogFileEntry();
        if (string.IsNullOrWhiteSpace(line)) return false;

        // Erwartetes Format: "dd.MM.yyyy HH:mm:ss.fff |\tType\t|\tText"
        // Split an den Pipe-Zeichen, Tabs entfernen
        var parts = line.Split('|');
        if (parts.Length < 3) return false;

        var datePart = parts[0].Trim();
        var typePart = parts[1].Replace("\t", string.Empty).Trim();
        var textPart = string.Join("|", parts[2..]).Trim();

        if (!DateTime.TryParseExact(
            datePart,
            "dd.MM.yyyy HH:mm:ss.fff",
            System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
            System.Globalization.DateTimeStyles.None,
            out var dt))
        {
            // Fallback: support ISO 8601 format e.g. 2026-02-10T14:23:57.149+01:00
            if (System.DateTimeOffset.TryParse(
                datePart,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var dto))
            {
                dt = dto.LocalDateTime;
            }
            else
            {
                return false;
            }
        }


        LogType type = LogType.Info;
        if (typePart.Equals("Error", StringComparison.OrdinalIgnoreCase)) type = LogType.Error;
        else if (typePart.Equals("Debug", StringComparison.OrdinalIgnoreCase)) type = LogType.Debug;
        else if (typePart.Equals("Info", StringComparison.OrdinalIgnoreCase)) type = LogType.Info;

        entry.Date = dt;
        entry.Type = type;
        entry.Text = textPart;
        return true;
    }

    public LogListViewModel()
    {
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
