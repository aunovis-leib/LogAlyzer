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

    [ObservableProperty]
    private LogFileEntry? _selectedEntry;

    [ObservableProperty]
    private string _text1 = string.Empty;

    [ObservableProperty]
    private string _text2 = string.Empty;

    [ObservableProperty]
    private DateTime? _filterDate = null;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [RelayCommand]
    private async Task ChooseFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Logdatei wählen",
            Filter = "Log Files (*.log)|*.log"
        };
        if (dlg.ShowDialog() == true)
        {
            var entries = await Task.Run(() => ParseLogFile(dlg.FileName));

            LogFilesEntries.Clear();
            foreach (var e in entries)
            {
                LogFilesEntries.Add(e);
            }
            LogFilesView.Refresh();
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
            out var dt)) return false;


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
    }

    partial void OnSelectedTypeChanged(LogType? value)
    {
        LogFilesView.Filter = FilterByType;
        LogFilesView.Refresh();
    }

    partial void OnFilterDateChanged(DateTime? value)
    {
        LogFilesView.Refresh();
    }

    partial void OnFilterTextChanged(string value)
    {
        LogFilesView.Refresh();
    }

    private bool FilterByType(object obj)
    {
        if (obj is not LogFileEntry e) return false;
        var typeOk = SelectedType is null || e.Type == SelectedType.Value;
        if (!typeOk) return false;
        var dateOk = FilterDate is null || e.Date.Date == FilterDate.Value.Date;
        if (!dateOk) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        return (e.Text?.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
    }
}
