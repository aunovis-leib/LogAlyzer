using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services
{
    public sealed class AppSettingsManager
    {
        private static readonly Lazy<AppSettingsManager> _lazy = new(() => new AppSettingsManager());
        public static AppSettingsManager Instance => _lazy.Value;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private AppSettingsManager()
        {
            Load();
        }

        public IReadOnlyList<ParserProfile> ParserProfiles { get; private set; } = [];

        private void Load()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var path = Path.Combine(baseDir, "appsettings.json");
                if (!File.Exists(path))
                {
                    ParserProfiles = new List<ParserProfile>();
                    return;
                }

                var json = File.ReadAllText(path);
                var root = JsonSerializer.Deserialize<ConfigRoot>(json, _jsonOptions);
                ParserProfiles = root?.LogAnalyzer?.ParserProfiles ?? new List<ParserProfile>();
            }
            catch
            {
                ParserProfiles = new List<ParserProfile>();
            }
        }

        private class ConfigRoot
        {
            public LogAnalyzerSection? LogAnalyzer { get; set; }
        }

        private class LogAnalyzerSection
        {
            public List<ParserProfile> ParserProfiles { get; set; } = new List<ParserProfile>();
        }
    }
}
