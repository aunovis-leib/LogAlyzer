namespace LogAlyzer.PluginHost;

internal sealed class PluginManifest
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string ApiVersion { get; set; } = string.Empty;

    public string EntryAssembly { get; set; } = string.Empty;

    public string EntryType { get; set; } = string.Empty;
}