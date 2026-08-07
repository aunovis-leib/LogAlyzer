using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

internal sealed class NullPluginHostServices : IPluginHostServices
{
    public string DefaultLogDirectory => Environment.CurrentDirectory;

    public ValueTask<RemoteSyncPreview> PreviewRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(RemoteSyncPreview.Empty);
    }

    public ValueTask<RemoteSyncResult> SynchronizeRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default,
        IReadOnlySet<string>? approvedFileIds = null)
    {
        return ValueTask.FromResult(RemoteSyncResult.Empty);
    }

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task LoadLogFilesAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}