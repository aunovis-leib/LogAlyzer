using LogAnalyzer.Models;
using System.IO;
using System.Text.Json;

namespace LogAnalyzer.Services
{
    public sealed class AppSettingsManager
    {
        // Test hook: when set, the manager will use this base directory instead of AppContext.BaseDirectory
        // Prefer calling Initialize(baseDirectory) from tests instead of manipulating this field.
        internal static string? TestBaseDirectory;

        // Not readonly so tests can reinitialize the lazy instance
        private static Lazy<AppSettingsManager> _lazy = new(() => new AppSettingsManager());
        public static AppSettingsManager Instance => _lazy.Value;

        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private AppSettingsManager()
            : this(AppContext.BaseDirectory)
        {
        }

        // Internal constructor that accepts a base directory. Tests and callers can use Initialize to
        // replace the singleton factory so the manager uses a custom base directory.
        private AppSettingsManager(string baseDirectory)
        {
            var baseDir = TestBaseDirectory ?? baseDirectory;
            _settingsPath = Path.Combine(baseDir, "appsettings.json");
            Load();
        }

        // Public API to allow tests or application startup to configure where settings are stored.
        // Call this before accessing AppSettingsManager.Instance.
        public static void Initialize(string baseDirectory)
        {
            if (string.IsNullOrEmpty(baseDirectory))
                throw new System.ArgumentException("baseDirectory must be provided", nameof(baseDirectory));

            _lazy = new System.Lazy<AppSettingsManager>(() => new AppSettingsManager(baseDirectory));
        }

        internal AppSettings Settings { get; private set; } = new();

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
