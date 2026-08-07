namespace LogAlyzer.PluginContracts;

public interface IPluginHostServices
{
    string DefaultLogDirectory { get; }

    ValueTask<RemoteSyncResult> SynchronizeRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default);

    Task LoadLogFilesAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default);
}

public sealed record RemoteSyncResult(
    int AddedCount,
    int UpdatedCount,
    int UnchangedCount,
    int MissingCount,
    IReadOnlyList<string> ChangedFilePaths,
    IReadOnlyList<RemoteSyncFailure> Failures)
{
    public bool Succeeded => Failures.Count == 0;

    public static RemoteSyncResult Empty { get; } = new(
        0,
        0,
        0,
        0,
        [],
        []);
}

public sealed record RemoteSyncFailure(
    string RemoteFileId,
    string FileName,
    string Message);