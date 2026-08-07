namespace LogAlyzer.PluginHost;

public sealed record PluginLoadFailure(
    string PluginDirectory,
    string? PluginId,
    string Reason,
    Exception? Exception);