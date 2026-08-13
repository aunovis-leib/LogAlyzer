using LogAlyzer.Services;
using System;
using System.IO;
using Xunit;

namespace LogAlyzer.Tests
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
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAlyzerTests", "appsettings1_" + Guid.NewGuid().ToString());
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
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAlyzerTests", "appsettings2_" + Guid.NewGuid().ToString());
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
        public void SaveAndLoad_PersistsAllHighlightRuleProperties()
        {
            var tempDir = CreateTempDir("highlight_rules_persistence");
            AppSettingsManager.TestBaseDirectory = null;

            AppSettingsManager.Initialize(tempDir);
            var manager = AppSettingsManager.Instance;
            manager.Settings.SettingsView.HighlightRules =
            [
                new Models.HighlightRule
                {
                    SearchText = "timeout",
                    Color = "#FF0000",
                    IsEnabled = false
                },
                new Models.HighlightRule
                {
                    SearchText = "connection",
                    Color = "#00FF00",
                    IsEnabled = true
                }
            ];
            manager.Save();

            AppSettingsManager.Initialize(tempDir);
            var loadedRules = AppSettingsManager.Instance.Settings.SettingsView.HighlightRules;

            Assert.Equal(2, loadedRules.Count);
            Assert.Equal("timeout", loadedRules[0].SearchText);
            Assert.Equal("#FF0000", loadedRules[0].Color);
            Assert.False(loadedRules[0].IsEnabled);
            Assert.Equal("connection", loadedRules[1].SearchText);
            Assert.Equal("#00FF00", loadedRules[1].Color);
            Assert.True(loadedRules[1].IsEnabled);
        }

        [Fact]
        public void Load_MigratesLegacyHighlightRulesToDefaultProfile()
        {
            var tempDir = CreateTempDir("highlight_rules_migration");
            AppSettingsManager.TestBaseDirectory = null;
            File.WriteAllText(
                Path.Combine(tempDir, "appsettings.json"),
                "{ \"settingsView\": { \"highlightRules\": [ { \"searchText\": \"timeout\", \"color\": \"#FF0000\", \"isEnabled\": false } ] } }");

            AppSettingsManager.Initialize(tempDir);
            var settingsView = AppSettingsManager.Instance.Settings.SettingsView;

            var profile = Assert.Single(settingsView.HighlightRuleProfiles);
            Assert.Equal(Models.HighlightRuleProfile.DefaultName, profile.Name);
            var rule = Assert.Single(profile.Rules);
            Assert.Equal("timeout", rule.SearchText);
            Assert.Equal("#FF0000", rule.Color);
            Assert.False(rule.IsEnabled);
        }

        [Fact]
        public void SaveAndLoad_PersistsMultipleHighlightRuleProfilesAndSelection()
        {
            var tempDir = CreateTempDir("highlight_rule_profiles_persistence");
            AppSettingsManager.TestBaseDirectory = null;

            AppSettingsManager.Initialize(tempDir);
            var manager = AppSettingsManager.Instance;
            manager.Settings.SettingsView.HighlightRuleProfiles =
            [
                new Models.HighlightRuleProfile
                {
                    Name = "Errors",
                    Rules =
                    [
                        new Models.HighlightRule { SearchText = "error", Color = "#FF0000" }
                    ]
                },
                new Models.HighlightRuleProfile
                {
                    Name = "Warnings",
                    Rules =
                    [
                        new Models.HighlightRule { SearchText = "warning", Color = "#FFFF00" }
                    ]
                }
            ];
            manager.Settings.SettingsView.SelectedHighlightRuleProfileName = "Warnings";
            manager.Save();

            AppSettingsManager.Initialize(tempDir);
            var settingsView = AppSettingsManager.Instance.Settings.SettingsView;

            Assert.Equal("Warnings", settingsView.SelectedHighlightRuleProfileName);
            Assert.Equal(2, settingsView.HighlightRuleProfiles.Count);
            Assert.Equal("error", settingsView.HighlightRuleProfiles[0].Rules[0].SearchText);
            Assert.Equal("warning", settingsView.HighlightRuleProfiles[1].Rules[0].SearchText);
        }

        [Fact]
        public void Load_Handles_Invalid_Json_And_Uses_Defaults()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAlyzerTests", "appsettings3_" + Guid.NewGuid().ToString());
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

        [Fact]
        public void DefaultConstructor_SeedsSettingsFile_FromBundledDefaults_OnFirstRun()
        {
            var persistentDir = CreateTempDir("persistent_seed");
            var seedSourceDir = CreateTempDir("seed_source");

            // Bundled default settings that ship next to the application binaries.
            File.WriteAllText(
                Path.Combine(seedSourceDir, "appsettings.json"),
                "{ \"parserProfiles\": [ { \"name\": \"SeededProfile\" } ] }");

            try
            {
                AppSettingsManager.TestBaseDirectory = persistentDir;
                AppSettingsManager.TestSeedSourceDirectory = seedSourceDir;
                AppSettingsManager.ResetForTests();

                var mgr = AppSettingsManager.Instance;

                // The persistent settings file must be created (seeded) even though the user never saved.
                var persistentFile = Path.Combine(persistentDir, "appsettings.json");
                Assert.True(File.Exists(persistentFile));
                Assert.Contains(mgr.Settings.ParserProfiles, p => p.Name == "SeededProfile");
            }
            finally
            {
                ResetStaticHooks();
            }
        }

        [Fact]
        public void SavedPaths_Persist_And_AreNotOverwritten_When_BundledDefaultsChange()
        {
            var persistentDir = CreateTempDir("persistent_persist");
            var seedSourceDir = CreateTempDir("seed_source_persist");

            File.WriteAllText(
                Path.Combine(seedSourceDir, "appsettings.json"),
                "{ \"parserProfiles\": [ { \"name\": \"SeededProfile\" } ] }");

            try
            {
                AppSettingsManager.TestBaseDirectory = persistentDir;
                AppSettingsManager.TestSeedSourceDirectory = seedSourceDir;

                // First run: seed and save a folder path chosen by the user.
                AppSettingsManager.ResetForTests();
                var firstRun = AppSettingsManager.Instance;
                firstRun.Settings.SettingsView.ExplorerRootFolder = @"C:\Users\Someone\Logs";
                firstRun.Save();

                // Simulate a rebuild/update that ships a different bundled default settings file.
                File.WriteAllText(
                    Path.Combine(seedSourceDir, "appsettings.json"),
                    "{ \"parserProfiles\": [ { \"name\": \"DifferentDefault\" } ] }");

                // Second run reuses the same persistent directory.
                AppSettingsManager.ResetForTests();
                var secondRun = AppSettingsManager.Instance;

                // The user's saved path must survive and the bundled defaults must NOT overwrite it.
                Assert.Equal(@"C:\Users\Someone\Logs", secondRun.Settings.SettingsView.ExplorerRootFolder);
                Assert.DoesNotContain(secondRun.Settings.ParserProfiles, p => p.Name == "DifferentDefault");
            }
            finally
            {
                ResetStaticHooks();
            }
        }

        [Fact]
        public void Save_CreatesPersistentDirectory_WhenMissing()
        {
            var parentDir = CreateTempDir("persistent_parent");
            // Point at a not-yet-created subdirectory to verify Save creates it on demand.
            var persistentDir = Path.Combine(parentDir, "LogAlyzer");
            Assert.False(Directory.Exists(persistentDir));

            try
            {
                AppSettingsManager.TestBaseDirectory = persistentDir;
                AppSettingsManager.ResetForTests();

                var mgr = AppSettingsManager.Instance;
                mgr.Settings.SettingsView.ExplorerRootFolder = @"D:\Logs";
                mgr.Save();

                var persistentFile = Path.Combine(persistentDir, "appsettings.json");
                Assert.True(Directory.Exists(persistentDir));
                Assert.True(File.Exists(persistentFile));
                Assert.Contains(@"D:\\Logs", File.ReadAllText(persistentFile));
            }
            finally
            {
                ResetStaticHooks();
            }
        }

        private static string CreateTempDir(string name)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAlyzerTests", name + "_" + Guid.NewGuid().ToString());
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private static void ResetStaticHooks()
        {
            AppSettingsManager.TestBaseDirectory = null;
            AppSettingsManager.TestSeedSourceDirectory = null;
            AppSettingsManager.ResetForTests();
        }
    }
}
