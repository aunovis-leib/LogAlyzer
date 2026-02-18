using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    [RelayCommand]
    private void ResetDefaults()
    {
        SyncSelectionAcrossLists = true;
        ShowLiveChart = false;
        MaxEntriesPerList = 10000;
        SyncTolerance = TimeSpan.FromHours(1);
    }
}
