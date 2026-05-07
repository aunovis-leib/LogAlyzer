using LogAnalyzer.Services;
using LogAnalyzer.ViewModels;
using System;
using System.IO;
using Xunit;

namespace LogAnalyzer.Tests
{
    [Collection("AppSettingsManagerSerial")]
    public class SettingsViewModelTests
    {
        private static string CreateTempDir(string name)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", name + "_" + Guid.NewGuid().ToString());
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
    }
}