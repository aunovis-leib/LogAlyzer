using LogAnalyzer.Services;

namespace LogAnalyzer.Services
{
    public static class AppServices
    {
        public static AppSettingsManager AppSettings { get; } = AppSettingsManager.Instance;
    }
}
