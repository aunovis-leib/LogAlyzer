using LogAnalyzer.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LogAnalyzer.Services
{
    public sealed class AppSettingsManager
    {
        private static readonly Lazy<AppSettingsManager> _lazy = new(() => new AppSettingsManager());
        public static AppSettingsManager Instance => _lazy.Value;

        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private AppSettingsManager()
        {
            var baseDir = AppContext.BaseDirectory;
            _settingsPath = Path.Combine(baseDir, "appsettings.json");
            Load();
        }

        public AppSettings Settings { get; private set; } = new();

        public IReadOnlyList<ParserProfile> ParserProfiles => Settings.ParserProfiles;

        private void Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    Settings = new AppSettings();
                    EnsureDefaults();
                    return;
                }

                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                Settings = settings ?? new AppSettings();
                EnsureDefaults();
            }
            catch
            {
                Settings = new AppSettings();
                EnsureDefaults();
            }
        }

        public void Save()
        {
            EnsureDefaults();
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
        }

        private void EnsureDefaults()
        {
            Settings.LivChart ??= new LiveChartSettings();
            Settings.ParserProfiles ??= new List<ParserProfile>();
            Settings.SettingsView ??= new SettingsViewSettings();
        }
    }
}
