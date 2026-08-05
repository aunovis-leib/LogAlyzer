using System.Configuration;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Windows;
using LogAnalyzer.Services;
using Microsoft.Extensions.Logging;

namespace LogAnalyzer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static LogPatternService? _patternService;
        private static ILogger<App> Logger => AppServices.CreateLogger<App>();

        public static LogPatternService? PatternService => _patternService;

        protected override void OnStartup(StartupEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("de");
            AppServices.InitializeLogging();
            base.OnStartup(e);
            Logger.LogInformation("LogAnalyzer gestartet");

            // Initialize Pattern Service
            _patternService ??= new LogPatternService("LogPatterns");
            InitializePatternServiceAsync();
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
            Logger.LogInformation("LogAnalyzer wird beendet");
            AppServices.ShutdownLogging();
            base.OnExit(e);
        }
    }

}
