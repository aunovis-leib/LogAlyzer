using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using LogAlyzer.PluginContracts;
using LogAlyzer.PluginHost;
using LogAlyzer.Services;
using Microsoft.Extensions.Logging;

namespace LogAlyzer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static LogPatternService? _patternService;
        private static PluginManager? _pluginManager;
        private static PluginHostServices? _pluginHostServices;
        private static ILogger<App> Logger => AppServices.CreateLogger<App>();

        public static LogPatternService? PatternService => _patternService;
        public static PluginManager? PluginManager => _pluginManager;
        public static PluginHostServices? PluginHostServices => _pluginHostServices;

        protected override void OnStartup(StartupEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("de");
            AppServices.InitializeLogging();
            base.OnStartup(e);
            Logger.LogInformation("LogAlyzer gestartet");

            _pluginHostServices = new PluginHostServices(
                GetDefaultLogDirectory,
                (message, exception) => Logger.LogError(exception, "{Message}", message));
            _pluginManager = new PluginManager(
                Path.Combine(AppContext.BaseDirectory, PluginManager.PluginsDirectoryName),
                GetPluginDataDirectory(),
                LogPluginMessage,
                _pluginHostServices);
            _ = InitializePluginsAsync();

            // Initialize Pattern Service
            _patternService ??= new LogPatternService("LogPatterns");
            InitializePatternServiceAsync();
        }

        private static string GetPluginDataDirectory()
        {
            var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(appDataDirectory)
                ? Path.Combine(AppContext.BaseDirectory, PluginManager.PluginDataDirectoryName)
                : Path.Combine(appDataDirectory, "LogAlyzer", PluginManager.PluginDataDirectoryName);
        }

        private static string GetDefaultLogDirectory()
        {
            var configuredDirectory = AppServices.AppSettings.Settings.SettingsView?.ExplorerRootFolder;
            return string.IsNullOrWhiteSpace(configuredDirectory)
                ? Environment.CurrentDirectory
                : configuredDirectory;
        }

        private static async Task InitializePluginsAsync()
        {
            try
            {
                if (_pluginManager is null)
                {
                    return;
                }

                await _pluginManager.LoadPluginsAsync();
                Logger.LogInformation(
                    "{PluginCount} Plugins geladen, {FailureCount} Plugins übersprungen",
                    _pluginManager.LoadedPlugins.Count,
                    _pluginManager.Failures.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Initialisieren der Plugins");
            }
        }

        private static void LogPluginMessage(
            PluginLogLevel level,
            string message,
            Exception? exception)
        {
            switch (level)
            {
                case PluginLogLevel.Trace:
                    Logger.LogTrace(exception, "{Message}", message);
                    break;
                case PluginLogLevel.Debug:
                    Logger.LogDebug(exception, "{Message}", message);
                    break;
                case PluginLogLevel.Information:
                    Logger.LogInformation(exception, "{Message}", message);
                    break;
                case PluginLogLevel.Warning:
                    Logger.LogWarning(exception, "{Message}", message);
                    break;
                case PluginLogLevel.Error:
                    Logger.LogError(exception, "{Message}", message);
                    break;
            }
        }

        private async void InitializePatternServiceAsync()
        {
            try
            {
                if (_patternService is null)
                {
                    _patternService = new LogPatternService("LogPatterns");
                }

                await _patternService.LoadPatternsAsync();
                Logger.LogInformation("{PatternCount} Patterns geladen", _patternService.GetPatterns().Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden der Patterns");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.LogInformation("LogAlyzer wird beendet");
            _pluginManager?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            AppServices.ShutdownLogging();
            base.OnExit(e);
        }
    }

}
