using LogAlyzer.PluginContracts;

namespace LogAlyzer.TestPluginFixture;

public sealed class TestPlugin : ILogAlyzerPlugin
{
    public PluginInfo Info { get; } = new(
        "tests.valid-plugin",
        "Valid test plugin",
        "1.0.0",
        PluginApi.CurrentVersion);

    public ValueTask InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken = default)
    {
        context.Log(PluginLogLevel.Information, "Test plugin initialized.");
        return ValueTask.CompletedTask;
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}