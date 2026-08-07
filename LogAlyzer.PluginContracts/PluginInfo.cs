namespace LogAlyzer.PluginContracts;

public sealed record PluginInfo(
    string Id,
    string Name,
    string Version,
    string ApiVersion);

public enum PluginLogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error
}

public interface IPluginContext
{
    string PluginDirectory { get; }

    string DataDirectory { get; }

    void Log(PluginLogLevel level, string message, Exception? exception = null);
}

public interface ILogAlyzerPlugin
{
    PluginInfo Info { get; }

    ValueTask InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken = default);

    ValueTask ShutdownAsync(CancellationToken cancellationToken = default);
}