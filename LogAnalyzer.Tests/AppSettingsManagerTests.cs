using LogAnalyzer.Services;
using System.IO;
using Xunit;

namespace LogAnalyzer.Tests
{
    public class AppSettingsManagerTests
    {
        [Fact]
        public void Defaults_AreCreated_WhenNoFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", "appsettings1");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            AppSettingsManager.TestBaseDirectory = tempDir;

            var lazyField = typeof(AppSettingsManager).GetField("_lazy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            lazyField.SetValue(null, new System.Lazy<AppSettingsManager>(() => (AppSettingsManager)System.Activator.CreateInstance(typeof(AppSettingsManager), true)));

            var mgr = AppSettingsManager.Instance;

            Assert.NotNull(mgr.Settings);
        }

        [Fact]
        public void Save_WritesFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", "appsettings2");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            AppSettingsManager.TestBaseDirectory = tempDir;

            var lazyField = typeof(AppSettingsManager).GetField("_lazy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            lazyField.SetValue(null, new System.Lazy<AppSettingsManager>(() => (AppSettingsManager)System.Activator.CreateInstance(typeof(AppSettingsManager), true)));

            var mgr = AppSettingsManager.Instance;
            mgr.Settings.ParserProfiles.Add(new Models.ParserProfile { Name = "p1" });
            mgr.Save();

            var path = Path.Combine(tempDir, "appsettings.json");
            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("p1", content);
        }
    }
}