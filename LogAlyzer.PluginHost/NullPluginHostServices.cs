using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

internal sealed class NullPluginHostServices : IPluginHostServices
{
    public string DefaultLogDirectory => Environment.CurrentDirectory;

    public ValueTask<RemoteSyncResult> SynchronizeRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(RemoteSyncResult.Empty);
    }

    public Task LoadLogFilesAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}