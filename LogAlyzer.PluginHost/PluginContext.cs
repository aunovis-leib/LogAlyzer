using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

internal sealed class PluginContext(
    string pluginDirectory,
    string dataDirectory,
    Action<PluginLogLevel, string, Exception?> log) : IPluginContext
{
    public string PluginDirectory { get; } = pluginDirectory;

    public string DataDirectory { get; } = dataDirectory;

    public void Log(PluginLogLevel level, string message, Exception? exception = null)
    {
        log(level, message, exception);
    }
}