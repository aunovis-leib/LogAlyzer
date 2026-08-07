using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

public sealed class PluginHostServices : IPluginHostServices
{
    private readonly Func<string> _defaultLogDirectoryProvider;
    private readonly RemoteLogSynchronizer _synchronizer;
    private Func<IReadOnlyList<string>, CancellationToken, Task> _loadLogFiles =
        static (_, _) => Task.CompletedTask;

    public PluginHostServices(
        Func<string> defaultLogDirectoryProvider,
        Action<string, Exception?>? log = null)
    {
        _defaultLogDirectoryProvider = defaultLogDirectoryProvider
            ?? throw new ArgumentNullException(nameof(defaultLogDirectoryProvider));
        _synchronizer = new RemoteLogSynchronizer(log);
    }

    public string DefaultLogDirectory
    {
        get
        {
            var directory = _defaultLogDirectoryProvider();
            return string.IsNullOrWhiteSpace(directory)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(directory);
        }
    }

    public ValueTask<RemoteSyncPreview> PreviewRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default)
    {
        return _synchronizer.PreviewAsync(
            source,
            pluginDataDirectory,
            cancellationToken);
    }

    public ValueTask<RemoteSyncResult> SynchronizeRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default,
        IReadOnlySet<string>? approvedFileIds = null)
    {
        return _synchronizer.SynchronizeAsync(
            source,
            DefaultLogDirectory,
            pluginDataDirectory,
            cancellationToken,
            approvedFileIds);
    }

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        return _confirmHandler(title, message, cancellationToken);
    }

    public Task LoadLogFilesAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        return _loadLogFiles(filePaths, cancellationToken);
    }

    public void SetLogFileLoader(
        Func<IReadOnlyList<string>, CancellationToken, Task>? loadLogFiles)
    {
        _loadLogFiles = loadLogFiles ?? ((_, _) => Task.CompletedTask);
    }

    public void SetConfirmationHandler(
        Func<string, string, CancellationToken, Task<bool>>? confirmationHandler)
    {
        _confirmHandler = confirmationHandler
            ?? (static (_, _, _) => Task.FromResult(false));
    }

    private Func<string, string, CancellationToken, Task<bool>> _confirmHandler =
        static (_, _, _) => Task.FromResult(false);
}