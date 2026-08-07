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

    public ValueTask<RemoteSyncResult> SynchronizeRemoteLogsAsync(
        IRemoteLogSource source,
        string pluginDataDirectory,
        CancellationToken cancellationToken = default)
    {
        return _synchronizer.SynchronizeAsync(
            source,
            DefaultLogDirectory,
            pluginDataDirectory,
            cancellationToken);
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
}