using LogAnalyzer.Services;
using System;
using System.IO;
using Xunit;

namespace LogAnalyzer.Tests
{
    [Collection("AppSettingsManagerSerial")]
    public class AppSettingsManagerTests
    {
        [Fact]
        public void Initialize_Throws_When_BaseDirectory_Is_NullOrEmpty()
        {
            Assert.Throws<ArgumentException>(() => AppSettingsManager.Initialize((string?)null!));
            Assert.Throws<ArgumentException>(() => AppSettingsManager.Initialize(string.Empty));
        }

        [Fact]
        public void Defaults_AreCreated_WhenNoFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", "appsettings1_" + Guid.NewGuid().ToString());
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            AppSettingsManager.Initialize(tempDir);
            var mgr = AppSettingsManager.Instance;

            Assert.NotNull(mgr.Settings);
            Assert.NotNull(mgr.Settings.LivChart);
            Assert.NotNull(mgr.Settings.ParserProfiles);
            Assert.NotNull(mgr.Settings.SettingsView);
        }

        [Fact]
        public void Save_WritesFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", "appsettings2_" + Guid.NewGuid().ToString());
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            AppSettingsManager.Initialize(tempDir);
            var mgr = AppSettingsManager.Instance;
            mgr.Settings.ParserProfiles.Add(new Models.ParserProfile { Name = "p1" });
            mgr.Save();

            var path = Path.Combine(tempDir, "appsettings.json");
            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("p1", content);
        }

        [Fact]
        public void Load_Handles_Invalid_Json_And_Uses_Defaults()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", "appsettings3_" + Guid.NewGuid().ToString());
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var path = Path.Combine(tempDir, "appsettings.json");
            File.WriteAllText(path, "{ invalid json }");

            AppSettingsManager.Initialize(tempDir);
            var mgr = AppSettingsManager.Instance;

            Assert.NotNull(mgr.Settings);
            Assert.NotNull(mgr.Settings.LivChart);
            Assert.NotNull(mgr.Settings.ParserProfiles);
            Assert.NotNull(mgr.Settings.SettingsView);
        }
    }
}
