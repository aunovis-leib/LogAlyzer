using LogAnalyzer.Models;
using System.IO;
using System.Text.Json;

namespace LogAnalyzer.Services
{
    public sealed class AppSettingsManager
    {
        internal const string SettingsFileName = "appsettings.json";

        // Folder name used under the per-user application data directory to store settings.
        internal const string AppDataFolderName = "LogAlyzer";

        // Test hook: when set, the manager will use this base directory instead of the persistent
        // per-user application data directory.
        // Prefer calling Initialize(baseDirectory) from tests instead of manipulating this field.
        internal static string? TestBaseDirectory;

        // Test hook: when set, the default constructor seeds a missing settings file from this
        // directory instead of AppContext.BaseDirectory. Used to exercise the seeding behavior.
        internal static string? TestSeedSourceDirectory;

        // Not readonly so tests can reinitialize the lazy instance
        private static Lazy<AppSettingsManager> _lazy = new(() => new AppSettingsManager());
        public static AppSettingsManager Instance => _lazy.Value;

        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        // Default (production) constructor. Persists settings in a per-user application data
        // directory so that saved folder paths survive application rebuilds and updates, and are
        // not overwritten by the bundled default appsettings.json shipped with the binaries.
        private AppSettingsManager()
        {
            var baseDir = TestBaseDirectory ?? GetPersistentSettingsDirectory();
            _settingsPath = Path.Combine(baseDir, SettingsFileName);
            EnsureSettingsFileSeeded();
            Load();
        }

        // Internal constructor that accepts an explicit base directory. Tests and callers can use
        // Initialize to replace the singleton factory so the manager uses a custom base directory.
        private AppSettingsManager(string baseDirectory)
        {
            var baseDir = TestBaseDirectory ?? baseDirectory;
            _settingsPath = Path.Combine(baseDir, SettingsFileName);
            Load();
        }

        // Resolves the per-user directory where the mutable settings file is persisted. This lives
        // outside the application's binary output directory so it is not overwritten when the app is
        // rebuilt/updated and remains writable when the app is installed to a protected location.
        private static string GetPersistentSettingsDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                // Fall back to the binary directory only if no application data directory is available.
                return AppContext.BaseDirectory;
            }

            return Path.Combine(appData, AppDataFolderName);
        }

        // On first run there is no user settings file yet. Seed it from the default appsettings.json
        // that ships next to the application binaries so that the built-in parser profiles and other
        // defaults are preserved for the user.
        private void EnsureSettingsFileSeeded()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    return;
                }

                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var seedSource = TestSeedSourceDirectory ?? AppContext.BaseDirectory;
                var bundledDefaults = Path.Combine(seedSource, SettingsFileName);
                if (File.Exists(bundledDefaults) &&
                    !string.Equals(Path.GetFullPath(bundledDefaults), Path.GetFullPath(_settingsPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(bundledDefaults, _settingsPath);
                }
            }
            catch
            {
                // Seeding is best-effort; if it fails the defaults are still applied in Load/EnsureDefaults.
            }
        }

        // Public API to allow tests or application startup to configure where settings are stored.
        // Call this before accessing AppSettingsManager.Instance.
        public static void Initialize(string baseDirectory)
        {
            if (string.IsNullOrEmpty(baseDirectory))
                throw new System.ArgumentException("baseDirectory must be provided", nameof(baseDirectory));

            _lazy = new System.Lazy<AppSettingsManager>(() => new AppSettingsManager(baseDirectory));
        }

        // Test helper: resets the singleton so the next access rebuilds it via the default
        // (production) constructor, exercising the persistent-directory and seeding behavior.
        internal static void ResetForTests()
        {
            _lazy = new Lazy<AppSettingsManager>(() => new AppSettingsManager());
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

            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

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
