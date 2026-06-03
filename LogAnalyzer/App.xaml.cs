using System.Configuration;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Windows;
using LogAnalyzer.Services;

namespace LogAnalyzer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static LogPatternService? _patternService;

        public static LogPatternService? PatternService => _patternService;

        protected override void OnStartup(StartupEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("de");
            base.OnStartup(e);

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
                System.Diagnostics.Debug.WriteLine($"✓ Loaded {_patternService.GetPatterns().Count} patterns");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading patterns: {ex.Message}");
            }
        }
    }

}
