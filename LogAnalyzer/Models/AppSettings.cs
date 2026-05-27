using CommunityToolkit.Mvvm.ComponentModel;

namespace LogAnalyzer.Models;

public sealed class AppSettings
{
    public LiveChartSettings LivChart { get; set; } = new();
    public List<ParserProfile> ParserProfiles { get; set; } = new();
    public SettingsViewSettings SettingsView { get; set; } = new();
}

public sealed class LiveChartSettings
{
    public bool ShowLiveChart { get; set; } = true;
}

public sealed class SettingsViewSettings
{
    public bool SyncSelectionAcrossLists { get; set; } = true;
    public int MaxEntriesPerList { get; set; } = 10000;
    public TimeSpan SyncTolerance { get; set; } = TimeSpan.FromHours(1);
    public string ExplorerRootFolder { get; set; } = string.Empty;
    public List<string> ExplorerRootFolderHistory { get; set; } = new();
    public bool AutoReloadLogFiles { get; set; } = false;
    public bool DateSortDescending { get; set; } = true;
    public List<HighlightRule> HighlightRules { get; set; } = new();
}

public sealed class HighlightRule : ObservableObject
{
    private string _searchText = string.Empty;
    private string _color = "#FFFF00";
    private bool _isEnabled = true;

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
