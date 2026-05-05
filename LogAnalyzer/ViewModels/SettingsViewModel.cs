using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
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

    public SettingsViewModel()
    {
        var settings = AppSettingsManager.Instance.Settings;
        var liveChart = GetOrCreateLiveChartSettings(settings);
        var settingsView = GetOrCreateSettingsViewSettings(settings);
        ShowLiveChart = liveChart.ShowLiveChart;
        SyncSelectionAcrossLists = settingsView.SyncSelectionAcrossLists;
        MaxEntriesPerList = settingsView.MaxEntriesPerList;
        SyncTolerance = settingsView.SyncTolerance;
        ExplorerRootFolder = settingsView.ExplorerRootFolder;
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
        settingsView.ExplorerRootFolder = value?.Trim() ?? string.Empty;
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
