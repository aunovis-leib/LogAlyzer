using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

internal sealed class PluginContext(
    string pluginDirectory,
    string dataDirectory,
    IPluginHostServices hostServices,
    Action<PluginLogLevel, string, Exception?> log) : IPluginContext
{
    public string PluginDirectory { get; } = pluginDirectory;

    public string DataDirectory { get; } = dataDirectory;

    public IPluginHostServices HostServices { get; } = hostServices;

    public void Log(PluginLogLevel level, string message, Exception? exception = null)
    {
        log(level, message, exception);
    }
}