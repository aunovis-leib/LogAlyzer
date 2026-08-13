using LogAlyzer.Services;
using LogAlyzer.ViewModels;
using System;
using System.IO;
using Xunit;

namespace LogAlyzer.Tests
{
    [Collection("AppSettingsManagerSerial")]
    public class SettingsViewModelTests
    {
        private static string CreateTempDir(string name)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAlyzerTests", name + "_" + Guid.NewGuid().ToString());
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        [Fact]
        public void Constructor_LoadsValuesFromSettings()
        {
            // Arrange
            var tempDir = CreateTempDir("settings1");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var vm = new SettingsViewModel();

            Assert.True(vm.SyncSelectionAcrossLists);
            Assert.True(vm.ShowLiveChart);
            Assert.Equal(10000, vm.MaxEntriesPerList);
            Assert.Equal(string.Empty, vm.ExplorerRootFolder);
        }

        [Fact]
        public void ResetDefaults_CommandResetsValues()
        {
            var tempDir = CreateTempDir("settings3");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var vm = new SettingsViewModel();
            vm.SyncSelectionAcrossLists = false;
            vm.ShowLiveChart = true;
            vm.MaxEntriesPerList = 5;
            vm.ExplorerRootFolder = tempDir;

            vm.ResetDefaultsCommand.Execute(null);

            Assert.True(vm.SyncSelectionAcrossLists);
            Assert.False(vm.ShowLiveChart);
            Assert.Equal(10000, vm.MaxEntriesPerList);
            Assert.Equal(string.Empty, vm.ExplorerRootFolder);
        }

        [Fact]
        public void ExplorerRootFolder_IsPersistedToSettings()
        {
            var tempDir = CreateTempDir("settings_root_folder");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var vm = new SettingsViewModel();
            vm.ExplorerRootFolder = tempDir;

            Assert.Equal(tempDir, AppSettingsManager.Instance.Settings.SettingsView.ExplorerRootFolder);
        }

        [Fact]
        public void SetExplorerRootFromMain_SetsExplorerRootWhenMainHasPath()
        {
            StaTestHelper.Run(() =>
            {
                var tempDir = CreateTempDir("set_root_from_main");
                AppSettingsManager.TestBaseDirectory = null;
                AppSettingsManager.Initialize(tempDir);

                var manager = AppSettingsManager.Instance;
                var mainVm = new MainViewModel(manager);

                var expected = tempDir;
                // ensure the first list has the desired current path
                mainVm.Lists[0].FileExplorerVM.CurrentPath = expected;

                var vm = new SettingsViewModel();
                vm.SetExplorerRootFromMain(mainVm);

                Assert.Equal(expected, vm.ExplorerRootFolder);
            });
        }

        [Fact]
        public void SetExplorerRootFromMain_NoPath_DoesNotChangeExplorerRoot()
        {
            StaTestHelper.Run(() =>
            {
                var tempDir = CreateTempDir("set_root_from_main_no_path");
                AppSettingsManager.TestBaseDirectory = null;
                AppSettingsManager.Initialize(tempDir);

                var manager = AppSettingsManager.Instance;
                var mainVm = new MainViewModel(manager);

                var vm = new SettingsViewModel();
                vm.ExplorerRootFolder = "initial";

                // make sure all lists have empty current path
                foreach (var l in mainVm.Lists)
                {
                    l.FileExplorerVM.CurrentPath = string.Empty;
                }

                vm.SetExplorerRootFromMain(mainVm);

                Assert.Equal("initial", vm.ExplorerRootFolder);
            });
        }

        [Fact]
        public void MaxEntriesPerListChanged_Event_RaisedWhenValueChanges()
        {
            var tempDir = CreateTempDir("max_entries_event");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var vm = new SettingsViewModel();
            var eventRaised = false;
            var eventValue = 0;

            vm.MaxEntriesPerListChanged += (sender, value) =>
            {
                eventRaised = true;
                eventValue = value;
            };

            vm.MaxEntriesPerList = 5000;

            Assert.True(eventRaised);
            Assert.Equal(5000, eventValue);
        }

        [Fact]
        public void MaxEntriesPerListChanged_UpdatesAppSettings()
        {
            var tempDir = CreateTempDir("max_entries_settings");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var vm = new SettingsViewModel();
            vm.MaxEntriesPerList = 3000;

            Assert.Equal(3000, AppSettingsManager.Instance.Settings.SettingsView.MaxEntriesPerList);
        }

        [Fact]
        public void LimitRuleResultsToFilteredEntries_IsPersistedToSettings()
        {
            var tempDir = CreateTempDir("settings_rule_results_filter");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var vm = new SettingsViewModel();
            vm.LimitRuleResultsToFilteredEntries = true;

            Assert.True(AppSettingsManager.Instance.Settings.SettingsView.LimitRuleResultsToFilteredEntries);
        }

        [Fact]
        public void Constructor_LoadsAllHighlightRuleProperties()
        {
            var tempDir = CreateTempDir("settings_highlight_rules_load");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var manager = AppSettingsManager.Instance;
            manager.Settings.SettingsView.HighlightRules.Add(new Models.HighlightRule
            {
                SearchText = "timeout",
                Color = "#FF0000",
                IsEnabled = false
            });
            manager.Save();

            AppSettingsManager.Initialize(tempDir);
            var vm = new SettingsViewModel();

            var rule = Assert.Single(vm.HighlightRules);
            Assert.Equal("timeout", rule.SearchText);
            Assert.Equal("#FF0000", rule.Color);
            Assert.False(rule.IsEnabled);
        }

        [Fact]
        public void HighlightRuleProfiles_CanBeSelectedAndPersisted()
        {
            var tempDir = CreateTempDir("settings_highlight_rule_profiles");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var vm = new SettingsViewModel
            {
                HighlightSearchText = "timeout",
                HighlightColor = "#FF0000"
            };
            vm.AddHighlightRuleCommand.Execute(null);

            var defaultProfile = Assert.Single(vm.HighlightRuleProfiles);
            vm.AddHighlightRuleProfileCommand.Execute(null);
            var secondProfile = vm.SelectedHighlightRuleProfile;
            Assert.NotNull(secondProfile);
            secondProfile!.Name = "Connections";
            vm.HighlightSearchText = "connection";
            vm.HighlightColor = "#00FF00";
            vm.AddHighlightRuleCommand.Execute(null);

            vm.SelectedHighlightRuleProfile = defaultProfile;
            Assert.Equal("timeout", Assert.Single(vm.HighlightRules).SearchText);

            vm.SelectedHighlightRuleProfile = secondProfile;
            Assert.Equal("connection", Assert.Single(vm.HighlightRules).SearchText);

            AppSettingsManager.Initialize(tempDir);
            var loadedVm = new SettingsViewModel();

            Assert.Equal("Connections", loadedVm.SelectedHighlightRuleProfile?.Name);
            Assert.Equal(2, loadedVm.HighlightRuleProfiles.Count);
            Assert.Equal("connection", Assert.Single(loadedVm.HighlightRules).SearchText);
            Assert.Equal(
                "timeout",
                Assert.Single(loadedVm.HighlightRuleProfiles.First(profile =>
                    profile.Name == defaultProfile.Name).Rules).SearchText);
        }

        [Fact]
        public void ResetDefaults_ClearsPersistedHighlightRules()
        {
            var tempDir = CreateTempDir("settings_highlight_rules_reset");
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.Initialize(tempDir);

            var vm = new SettingsViewModel
            {
                HighlightSearchText = "timeout",
                HighlightColor = "#FF0000"
            };
            vm.AddHighlightRuleCommand.Execute(null);

            Assert.Single(AppSettingsManager.Instance.Settings.SettingsView.HighlightRules);

            vm.ResetDefaultsCommand.Execute(null);

            Assert.Empty(vm.HighlightRules);
            AppSettingsManager.Initialize(tempDir);
            Assert.Empty(AppSettingsManager.Instance.Settings.SettingsView.HighlightRules);
        }
    }
}