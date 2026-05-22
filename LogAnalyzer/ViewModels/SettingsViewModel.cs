using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using System.Collections.ObjectModel;

namespace LogAnalyzer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public event EventHandler<bool>? AutoReloadToggled;
    public event EventHandler<int>? MaxEntriesPerListChanged;

    [ObservableProperty]
    private bool _syncSelectionAcrossLists = true;

    [ObservableProperty]
    private bool _showLiveChart = false;

    [ObservableProperty]
    private int _maxEntriesPerList = 10000;

    // Tolerance for date/time synchronization
    [ObservableProperty]
    private TimeSpan _syncTolerance = TimeSpan.FromHours(1);

    [ObservableProperty]
    private string _explorerRootFolder = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _explorerRootFolderHistory = new();

    [ObservableProperty]
    private bool _autoReloadLogFiles = false;

    [ObservableProperty]
    private bool _dateSortDescending = true;

    public SettingsViewModel()
    {
        var settings = AppSettingsManager.Instance.Settings;
        var liveChart = GetOrCreateLiveChartSettings(settings);
        var settingsView = GetOrCreateSettingsViewSettings(settings);
        ShowLiveChart = liveChart.ShowLiveChart;
        SyncSelectionAcrossLists = settingsView.SyncSelectionAcrossLists;
        MaxEntriesPerList = settingsView.MaxEntriesPerList;
        SyncTolerance = settingsView.SyncTolerance;
        AutoReloadLogFiles = settingsView.AutoReloadLogFiles;
        DateSortDescending = settingsView.DateSortDescending;

        var history = settingsView.ExplorerRootFolderHistory ?? new List<string>();
        var uniqueHistory = history.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var folder in uniqueHistory)
        {
            ExplorerRootFolderHistory.Add(folder);
        }
        settingsView.ExplorerRootFolderHistory = uniqueHistory;

        ExplorerRootFolder = settingsView.ExplorerRootFolder;
    }

    // Allow external callers (e.g. view tests or view code) to set the
    // explorer root folder based on a MainViewModel instance. This extracts
    // the logic from the view's click handler into a testable method.
    public void SetExplorerRootFromMain(MainViewModel? mainVm)
    {
        if (mainVm is null) return;

        var currentPath = mainVm.Lists
            .Select(x => x.FileExplorerVM.CurrentPath)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            ExplorerRootFolder = currentPath;
        }
    }

    partial void OnShowLiveChartChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var liveChart = GetOrCreateLiveChartSettings(manager.Settings);
        liveChart.ShowLiveChart = value;
        manager.Save();
    }

    partial void OnSyncSelectionAcrossListsChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.SyncSelectionAcrossLists = value;
        manager.Save();
    }

    partial void OnMaxEntriesPerListChanged(int value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.MaxEntriesPerList = value;
        manager.Save();
        MaxEntriesPerListChanged?.Invoke(this, value);
    }

    partial void OnSyncToleranceChanged(TimeSpan value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.SyncTolerance = value;
        manager.Save();
    }

    partial void OnExplorerRootFolderChanged(string value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        var trimmedValue = value?.Trim() ?? string.Empty;
        settingsView.ExplorerRootFolder = trimmedValue;

        if (!string.IsNullOrWhiteSpace(trimmedValue) && !ExplorerRootFolderHistory.Contains(trimmedValue, StringComparer.OrdinalIgnoreCase))
        {
            ExplorerRootFolderHistory.Add(trimmedValue);
            settingsView.ExplorerRootFolderHistory = new List<string>(ExplorerRootFolderHistory);
        }

        manager.Save();
    }

    partial void OnAutoReloadLogFilesChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.AutoReloadLogFiles = value;
        manager.Save();
        AutoReloadToggled?.Invoke(this, value);
    }

    partial void OnDateSortDescendingChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.DateSortDescending = value;
        manager.Save();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        SyncSelectionAcrossLists = true;
        ShowLiveChart = false;
        MaxEntriesPerList = 10000;
        SyncTolerance = TimeSpan.FromHours(1);
        ExplorerRootFolder = string.Empty;
        AutoReloadLogFiles = false;
        DateSortDescending = true;
    }

    private static LiveChartSettings GetOrCreateLiveChartSettings(AppSettings settings)
    {
        settings.LivChart ??= new LiveChartSettings();
        return settings.LivChart;
    }

    private static SettingsViewSettings GetOrCreateSettingsViewSettings(AppSettings settings)
    {
        settings.SettingsView ??= new SettingsViewSettings();
        return settings.SettingsView;
    }
}
