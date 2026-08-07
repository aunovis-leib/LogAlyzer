namespace LogAlyzer.PluginContracts;

public sealed record RemoteSourceDescriptor(
    string Id,
    string DisplayName);

public sealed record RemoteLogFile(
    string Id,
    string Name,
    string? RelativePath,
    long? Size,
    DateTimeOffset? LastModifiedUtc,
    string? ETag);

public interface IRemoteLogSource
{
    RemoteSourceDescriptor Descriptor { get; }

    IAsyncEnumerable<RemoteLogFile> ListLogFilesAsync(
        CancellationToken cancellationToken = default);

    ValueTask<Stream> OpenReadAsync(
        RemoteLogFile file,
        CancellationToken cancellationToken = default);
}

public interface IRemoteLogSourcePlugin : ILogAlyzerPlugin
{
    IReadOnlyList<IRemoteLogSource> Sources { get; }
}