using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using LogAnalyzer.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace LogAnalyzer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public event EventHandler<bool>? AutoReloadToggled;
    public event EventHandler<int>? MaxEntriesPerListChanged;
    public event EventHandler? HighlightRulesChanged;

    [ObservableProperty]
    private bool _syncSelectionAcrossLists = true;

    [ObservableProperty]
    private bool _showFileExplorerInLogLists = true;

    [ObservableProperty]
    private bool _showLiveChart = false;

    [ObservableProperty]
    private bool _showPatternMatchPanel = false;

    [ObservableProperty]
    private int _maxEntriesPerList = 10000;

    // Tolerance for date/time synchronization
    [ObservableProperty]
    private TimeSpan _syncTolerance = TimeSpan.FromHours(1);

    [ObservableProperty]
    private string _explorerRootFolder = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _explorerRootFolderHistory = [];

    [ObservableProperty]
    private bool _autoReloadLogFiles = false;

    [ObservableProperty]
    private bool _dateSortDescending = true;

    [ObservableProperty]
    private ObservableCollection<HighlightRule> _highlightRules = [];

    [ObservableProperty]
    private string _highlightSearchText = string.Empty;

    [ObservableProperty]
    private string _highlightColor = "#FFFF00";

    public ObservableCollection<ParserProfile> ParserProfiles { get; } = [];

    private ParserProfile? _selectedParserProfile;
    public ParserProfile? SelectedParserProfile
    {
        get => _selectedParserProfile;
        set => SetProperty(ref _selectedParserProfile, value);
    }

    public SettingsViewModel()
    {
        var settings = AppSettingsManager.Instance.Settings;
        var liveChart = GetOrCreateLiveChartSettings(settings);
        var patternMatchPanel = GetOrCreatePatternMatchPanelSettings(settings);
        var settingsView = GetOrCreateSettingsViewSettings(settings);
        ShowLiveChart = liveChart.ShowLiveChart;
        ShowPatternMatchPanel = patternMatchPanel.ShowPatternMatchPanel;
        SyncSelectionAcrossLists = settingsView.SyncSelectionAcrossLists;
        ShowFileExplorerInLogLists = settingsView.ShowFileExplorerInLogLists;
        MaxEntriesPerList = settingsView.MaxEntriesPerList;
        SyncTolerance = settingsView.SyncTolerance;
        AutoReloadLogFiles = settingsView.AutoReloadLogFiles;
        DateSortDescending = settingsView.DateSortDescending;

        var history = settingsView.ExplorerRootFolderHistory ?? [];
        var uniqueHistory = history.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var folder in uniqueHistory)
        {
            ExplorerRootFolderHistory.Add(folder);
        }
        settingsView.ExplorerRootFolderHistory = uniqueHistory;

        ExplorerRootFolder = settingsView.ExplorerRootFolder;

        foreach (var profile in settings.ParserProfiles)
        {
            AttachParserProfile(profile);
            ParserProfiles.Add(profile);
        }

        SelectedParserProfile = ParserProfiles.FirstOrDefault();

        foreach (var rule in settingsView.HighlightRules)
        {
            HighlightRules.Add(rule);
            // Subscribe to property changes for auto-save and update highlights
            rule.PropertyChanged += (s, e) =>
            {
                SaveHighlightRules();
                HighlightRulesChanged?.Invoke(this, EventArgs.Empty);
            };
        }
    }

    private void AttachParserProfile(ParserProfile profile)
    {
        profile.PropertyChanged += (_, __) =>
        {
            SaveParserProfiles();
        };
    }

    private void SaveParserProfiles()
    {
        var manager = AppSettingsManager.Instance;
        manager.Settings.ParserProfiles = [.. ParserProfiles];
        manager.Save();
    }

    [RelayCommand]
    private void AddParserProfile()
    {
        var profile = new ParserProfile
        {
            Name = $"Profile {ParserProfiles.Count + 1}"
        };

        AttachParserProfile(profile);
        ParserProfiles.Add(profile);
        SelectedParserProfile = profile;
        SaveParserProfiles();
    }

    [RelayCommand]
    private void RemoveParserProfile(ParserProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        if (!ParserProfiles.Contains(profile))
        {
            return;
        }

        ParserProfiles.Remove(profile);
        if (ReferenceEquals(SelectedParserProfile, profile))
        {
            SelectedParserProfile = ParserProfiles.FirstOrDefault();
        }

        SaveParserProfiles();
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

    partial void OnShowPatternMatchPanelChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var patternMatchPanel = GetOrCreatePatternMatchPanelSettings(manager.Settings);
        patternMatchPanel.ShowPatternMatchPanel = value;
        manager.Save();
    }

    partial void OnSyncSelectionAcrossListsChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.SyncSelectionAcrossLists = value;
        manager.Save();
    }

    partial void OnShowFileExplorerInLogListsChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.ShowFileExplorerInLogLists = value;
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
        }

        // Always sync the current history to settings
        settingsView.ExplorerRootFolderHistory = new List<string>(ExplorerRootFolderHistory);
        manager.Save();
    }

    partial void OnExplorerRootFolderHistoryChanged(ObservableCollection<string> value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.ExplorerRootFolderHistory = new List<string>(value);
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
        ShowFileExplorerInLogLists = true;
        ShowLiveChart = false;
        ShowPatternMatchPanel = false;
        MaxEntriesPerList = 10000;
        SyncTolerance = TimeSpan.FromHours(1);
        ExplorerRootFolder = string.Empty;
        AutoReloadLogFiles = false;
        DateSortDescending = true;
        HighlightRules.Clear();
        HighlightSearchText = string.Empty;
        HighlightColor = "#FFFF00";
    }

    [RelayCommand]
    private void AddHighlightRule()
    {
        if (string.IsNullOrWhiteSpace(HighlightSearchText))
            return;

        var rule = new HighlightRule { SearchText = HighlightSearchText, Color = HighlightColor };
        // Subscribe to property changes for auto-save and update highlights
        rule.PropertyChanged += (s, e) =>
        {
            SaveHighlightRules();
            HighlightRulesChanged?.Invoke(this, EventArgs.Empty);
        };
        HighlightRules.Add(rule);
        SaveHighlightRules();
        HighlightSearchText = string.Empty;
        HighlightColor = "#FFFF00";
    }

    [RelayCommand]
    private void RemoveHighlightRule(HighlightRule rule)
    {
        if (rule == null) return;
        HighlightRules.Remove(rule);
        SaveHighlightRules();
    }

    private void SaveHighlightRules()
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.HighlightRules = [.. HighlightRules];
        manager.Save();
    }

    [RelayCommand]
    private void OpenPatternEditor()
    {
        try
        {
            var patternService = App.PatternService;
            if (patternService == null)
            {
                System.Windows.MessageBox.Show("Pattern Service not initialized.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var editorVM = new PatternEditorViewModel(patternService);
            var editorWindow = new Window
            {
                Title = "Log Pattern Editor",
                Width = 1000,
                Height = 700,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Content = new PatternEditorView { DataContext = editorVM }
            };

            editorWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error opening Pattern Editor: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private static LiveChartSettings GetOrCreateLiveChartSettings(AppSettings settings)
    {
        settings.LivChart ??= new LiveChartSettings();
        return settings.LivChart;
    }

    private static PatternMatchPanelSettings GetOrCreatePatternMatchPanelSettings(AppSettings settings)
    {
        settings.PatternMatchPanel ??= new PatternMatchPanelSettings();
        return settings.PatternMatchPanel;
    }

    private static SettingsViewSettings GetOrCreateSettingsViewSettings(AppSettings settings)
    {
        settings.SettingsView ??= new SettingsViewSettings();
        return settings.SettingsView;
    }
}
