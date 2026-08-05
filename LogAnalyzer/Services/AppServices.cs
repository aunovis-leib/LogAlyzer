using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace LogAnalyzer.Services
{
    public static class AppServices
    {
        public static AppSettingsManager AppSettings { get; } = AppSettingsManager.Instance;

        public static ILoggerFactory LoggerFactory { get; private set; } =
            Microsoft.Extensions.Logging.LoggerFactory.Create(_ => { });

        public static string LogDirectory { get; private set; } = string.Empty;

        public static void InitializeLogging()
        {
            LogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LogAnalyzer",
                "Logs");
            Directory.CreateDirectory(LogDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    Path.Combine(LogDirectory, "LogAnalyzer-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-ddTHH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            LoggerFactory.Dispose();
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: false);
            });
        }

        public static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

        public static void ShutdownLogging()
        {
            LoggerFactory.Dispose();
            Log.CloseAndFlush();
        }
    }
}
