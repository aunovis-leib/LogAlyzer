using CommunityToolkit.Mvvm.ComponentModel;

namespace LogAlyzer.Models;

public sealed class AppSettings
{
    public LiveChartSettings LivChart { get; set; } = new();
    public PatternMatchPanelSettings PatternMatchPanel { get; set; } = new();
    public List<ParserProfile> ParserProfiles { get; set; } = [];
    public SettingsViewSettings SettingsView { get; set; } = new();
}

public sealed class LiveChartSettings
{
    public bool ShowLiveChart { get; set; } = true;
}

public sealed class PatternMatchPanelSettings
{
    public bool ShowPatternMatchPanel { get; set; } = true;
}

public sealed class SettingsViewSettings
{
    public bool ApplyHighlightRules { get; set; } = true;
    public bool LimitRuleResultsToFilteredEntries { get; set; } = false;
    public bool SyncSelectionAcrossLists { get; set; } = true;
    public bool ShowFileExplorerInLogLists { get; set; } = true;
    public int MaxEntriesPerList { get; set; } = 10000;
    public bool FastLoadMode { get; set; } = false;
    public TimeSpan SyncTolerance { get; set; } = TimeSpan.FromHours(1);
    public string ExplorerRootFolder { get; set; } = string.Empty;
    public List<string> ExplorerRootFolderHistory { get; set; } = [];
    public bool AutoReloadLogFiles { get; set; } = false;
    public bool DateSortDescending { get; set; } = true;
    public bool ShowMiniMap { get; set; } = true;
    public List<HighlightRuleProfile> HighlightRuleProfiles { get; set; } = [];
    public string SelectedHighlightRuleProfileName { get; set; } = HighlightRuleProfile.DefaultName;
    public List<HighlightRule> HighlightRules { get; set; } = [];
}

public sealed class HighlightRuleProfile : ObservableObject
{
    public const string DefaultName = "Default";

    private string _name = DefaultName;
    private List<HighlightRule> _rules = [];

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public List<HighlightRule> Rules
    {
        get => _rules;
        set => SetProperty(ref _rules, value ?? []);
    }
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
