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
}
