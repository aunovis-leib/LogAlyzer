namespace LogAlyzer.PluginContracts;

public interface IPluginHostServices
{
    string DefaultLogDirectory { get; }

    ValueTask<RemoteSyncPreview> PreviewRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteSyncResult> SynchronizeRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default,
        IReadOnlySet<string>? approvedFileIds = null);

    Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default);

    Task LoadLogFilesAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default);
}

public sealed record RemoteSyncPreview(
    IReadOnlyList<RemoteSyncPreviewFile> PendingFiles,
    int UnchangedCount,
    int MissingCount,
    IReadOnlyList<RemoteSyncFailure> Failures)
{
    public int NewCount => PendingFiles.Count(file => file.IsNew);

    public int UpdatedCount => PendingFiles.Count(file => !file.IsNew);

    public bool HasPendingFiles => PendingFiles.Count > 0;

    public bool Succeeded => Failures.Count == 0;

    public static RemoteSyncPreview Empty { get; } = new(
        [],
        0,
        0,
        []);
}

public sealed record RemoteSyncPreviewFile(
    RemoteLogFile File,
    bool IsNew);

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