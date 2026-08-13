using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAlyzer.Models;
using LogAlyzer.Services;
using LogAlyzer.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace LogAlyzer.ViewModels;

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

    [ObservableProperty]
    private bool _fastLoadMode = false;

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
    private bool _showMiniMap = true;

    [ObservableProperty]
    private ObservableCollection<HighlightRule> _highlightRules = [];

    public ObservableCollection<HighlightRuleProfile> HighlightRuleProfiles { get; } = [];

    private HighlightRuleProfile? _selectedHighlightRuleProfile;
    private readonly HashSet<HighlightRule> _attachedHighlightRules = [];
    private readonly HashSet<HighlightRuleProfile> _attachedHighlightRuleProfiles = [];
    private bool _suppressHighlightRulePersistence;

    public HighlightRuleProfile? SelectedHighlightRuleProfile
    {
        get => _selectedHighlightRuleProfile;
        set
        {
            if (ReferenceEquals(_selectedHighlightRuleProfile, value))
            {
                return;
            }

            SaveCurrentHighlightRuleProfile();

            if (SetProperty(ref _selectedHighlightRuleProfile, value))
            {
                LoadHighlightRulesForProfile(value);
                SaveHighlightRules();
                HighlightRulesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [ObservableProperty]
    private string _highlightSearchText = string.Empty;

    [ObservableProperty]
    private string _highlightColor = "#FFFF00";

    [ObservableProperty]
    private bool _applyHighlightRules = true;

    [ObservableProperty]
    private bool _limitRuleResultsToFilteredEntries;

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
        FastLoadMode = settingsView.FastLoadMode;
        SyncTolerance = settingsView.SyncTolerance;
        AutoReloadLogFiles = settingsView.AutoReloadLogFiles;
        DateSortDescending = settingsView.DateSortDescending;
        ShowMiniMap = settingsView.ShowMiniMap;
        ApplyHighlightRules = settingsView.ApplyHighlightRules;
        LimitRuleResultsToFilteredEntries = settingsView.LimitRuleResultsToFilteredEntries;

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

        foreach (var profile in settingsView.HighlightRuleProfiles)
        {
            AttachHighlightRuleProfile(profile);
            HighlightRuleProfiles.Add(profile);
        }

        if (HighlightRuleProfiles.Count == 0)
        {
            var defaultProfile = new HighlightRuleProfile
            {
                Name = HighlightRuleProfile.DefaultName,
                Rules = [.. settingsView.HighlightRules]
            };
            AttachHighlightRuleProfile(defaultProfile);
            HighlightRuleProfiles.Add(defaultProfile);
        }

        _selectedHighlightRuleProfile = HighlightRuleProfiles.FirstOrDefault(profile =>
            string.Equals(
                profile.Name,
                settingsView.SelectedHighlightRuleProfileName,
                StringComparison.OrdinalIgnoreCase))
            ?? HighlightRuleProfiles.FirstOrDefault();

        LoadHighlightRulesForProfile(_selectedHighlightRuleProfile);

        HighlightRules.CollectionChanged += (_, _) =>
        {
            if (_suppressHighlightRulePersistence)
            {
                return;
            }

            SaveHighlightRules();
            HighlightRulesChanged?.Invoke(this, EventArgs.Empty);
        };
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

    private void AttachHighlightRuleProfile(HighlightRuleProfile profile)
    {
        if (_attachedHighlightRuleProfiles.Add(profile))
        {
            profile.PropertyChanged += HighlightRuleProfile_PropertyChanged;
        }
    }

    private void DetachHighlightRuleProfile(HighlightRuleProfile profile)
    {
        if (_attachedHighlightRuleProfiles.Remove(profile))
        {
            profile.PropertyChanged -= HighlightRuleProfile_PropertyChanged;
        }
    }

    private void HighlightRuleProfile_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HighlightRuleProfile.Name))
        {
            SaveHighlightRules();
        }
    }

    private string CreateHighlightRuleProfileName()
    {
        var index = 1;
        string name;
        do
        {
            name = $"Profile {index++}";
        }
        while (HighlightRuleProfiles.Any(profile =>
            string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)));

        return name;
    }

    [RelayCommand]
    private void AddHighlightRuleProfile()
    {
        var profile = new HighlightRuleProfile
        {
            Name = CreateHighlightRuleProfileName()
        };

        AttachHighlightRuleProfile(profile);
        HighlightRuleProfiles.Add(profile);
        SelectedHighlightRuleProfile = profile;
        SaveHighlightRules();
    }

    [RelayCommand]
    private void RemoveHighlightRuleProfile(HighlightRuleProfile? profile)
    {
        if (profile is null || !HighlightRuleProfiles.Contains(profile))
        {
            return;
        }

        var wasSelected = ReferenceEquals(SelectedHighlightRuleProfile, profile);
        if (wasSelected)
        {
            SaveCurrentHighlightRuleProfile();
        }

        DetachHighlightRuleProfile(profile);
        HighlightRuleProfiles.Remove(profile);

        if (HighlightRuleProfiles.Count == 0)
        {
            var defaultProfile = new HighlightRuleProfile
            {
                Name = HighlightRuleProfile.DefaultName
            };
            AttachHighlightRuleProfile(defaultProfile);
            HighlightRuleProfiles.Add(defaultProfile);
        }

        if (wasSelected)
        {
            SelectedHighlightRuleProfile = HighlightRuleProfiles.First();
        }
        else
        {
            SaveHighlightRules();
        }
    }

    private void AttachHighlightRule(HighlightRule rule)
    {
        if (_attachedHighlightRules.Add(rule))
        {
            rule.PropertyChanged += HighlightRule_PropertyChanged;
        }
    }

    private void DetachHighlightRule(HighlightRule rule)
    {
        if (_attachedHighlightRules.Remove(rule))
        {
            rule.PropertyChanged -= HighlightRule_PropertyChanged;
        }
    }

    private void HighlightRule_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressHighlightRulePersistence)
        {
            return;
        }

        SaveHighlightRules();
        HighlightRulesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadHighlightRulesForProfile(HighlightRuleProfile? profile)
    {
        _suppressHighlightRulePersistence = true;
        try
        {
            foreach (var rule in HighlightRules)
            {
                DetachHighlightRule(rule);
            }

            HighlightRules.Clear();

            foreach (var rule in profile?.Rules ?? [])
            {
                AttachHighlightRule(rule);
                HighlightRules.Add(rule);
            }
        }
        finally
        {
            _suppressHighlightRulePersistence = false;
        }
    }

    private void SaveCurrentHighlightRuleProfile()
    {
        if (SelectedHighlightRuleProfile is not null)
        {
            SelectedHighlightRuleProfile.Rules = [.. HighlightRules];
        }
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

    partial void OnFastLoadModeChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.FastLoadMode = value;
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

    partial void OnShowMiniMapChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.ShowMiniMap = value;
        manager.Save();
    }

    partial void OnApplyHighlightRulesChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.ApplyHighlightRules = value;
        manager.Save();
        HighlightRulesChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnLimitRuleResultsToFilteredEntriesChanged(bool value)
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);
        settingsView.LimitRuleResultsToFilteredEntries = value;
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
        FastLoadMode = false;
        SyncTolerance = TimeSpan.FromHours(1);
        ExplorerRootFolder = string.Empty;
        AutoReloadLogFiles = false;
        DateSortDescending = true;
        ShowMiniMap = true;
        ApplyHighlightRules = true;
        LimitRuleResultsToFilteredEntries = false;
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
        AttachHighlightRule(rule);
        HighlightRules.Add(rule);
        HighlightSearchText = string.Empty;
        HighlightColor = "#FFFF00";
    }

    [RelayCommand]
    private void RemoveHighlightRule(HighlightRule? rule)
    {
        if (rule is null) return;
        DetachHighlightRule(rule);
        HighlightRules.Remove(rule);
    }

    private void SaveHighlightRules()
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = GetOrCreateSettingsViewSettings(manager.Settings);

        SaveCurrentHighlightRuleProfile();
        settingsView.HighlightRuleProfiles = [.. HighlightRuleProfiles];
        settingsView.SelectedHighlightRuleProfileName = SelectedHighlightRuleProfile?.Name
            ?? HighlightRuleProfiles.FirstOrDefault()?.Name
            ?? HighlightRuleProfile.DefaultName;
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
