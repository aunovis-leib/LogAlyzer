using LogAnalyzer.Services;
using LogAnalyzer.ViewModels;
using System.IO;
using Xunit;

namespace LogAnalyzer.Tests
{
    public class SettingsViewModelTests
    {
        [Fact]
        public void Constructor_LoadsValuesFromSettings()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", "settings1");
            Directory.CreateDirectory(tempDir);
            AppSettingsManager.TestBaseDirectory = tempDir;

            // ensure no file exists -> defaults
            var mgrField = typeof(AppSettingsManager).GetProperty("Instance");
            // reset singleton by reflection
            var lazyField = typeof(AppSettingsManager).GetField("_lazy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            lazyField.SetValue(null, new System.Lazy<AppSettingsManager>(() => (AppSettingsManager)System.Activator.CreateInstance(typeof(AppSettingsManager), true)));

            var vm = new SettingsViewModel();

            Assert.True(vm.SyncSelectionAcrossLists);
            Assert.True(vm.ShowLiveChart);
            Assert.Equal(10000, vm.MaxEntriesPerList);
        }

        [Fact]
        public void ResetDefaults_CommandResetsValues()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", "settings3");
            Directory.CreateDirectory(tempDir);
            AppSettingsManager.TestBaseDirectory = tempDir;

            var lazyField = typeof(AppSettingsManager).GetField("_lazy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            lazyField.SetValue(null, new System.Lazy<AppSettingsManager>(() => (AppSettingsManager)System.Activator.CreateInstance(typeof(AppSettingsManager), true)));

            var vm = new SettingsViewModel();
            vm.SyncSelectionAcrossLists = false;
            vm.ShowLiveChart = true;
            vm.MaxEntriesPerList = 5;

            vm.ResetDefaultsCommand.Execute(null);

            Assert.True(vm.SyncSelectionAcrossLists);
            Assert.False(vm.ShowLiveChart);
            Assert.Equal(10000, vm.MaxEntriesPerList);
        }
    }
}