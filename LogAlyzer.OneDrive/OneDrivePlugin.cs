using LogAlyzer.PluginContracts;

namespace LogAlyzer.Plugins.OneDrive;

public sealed class OneDrivePlugin : IRemoteLogSourcePlugin, IPluginUiContribution
{
    private IPluginContext? _context;
    private OneDriveLogSource? _source;
    private OneDrivePanel? _panel;

    public PluginInfo Info { get; } = new(
        "logalyzer.onedrive",
        "OneDrive",
        "1.0.0",
        PluginApi.CurrentVersion);

    public IReadOnlyList<IRemoteLogSource> Sources =>
        _source is null ? [] : [_source];

    public IPluginPanelContribution? Panel => _panel;

    public async ValueTask InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken = default)
    {
        _context = context;
        var options = await OneDriveOptions.LoadAsync(context.DataDirectory, cancellationToken);
        _source = new OneDriveLogSource(options, context.Log);
        _panel = new OneDrivePanel(RefreshAsync);
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _context = null;
        _source = null;
        _panel = null;
        return ValueTask.CompletedTask;
    }

    private async ValueTask<PluginActionResult> RefreshAsync(CancellationToken cancellationToken)
    {
        if (_context is null || _source is null)
        {
            return new PluginActionResult(false, "OneDrive-Plugin ist nicht initialisiert.");
        }

        try
        {
            var result = await _context.HostServices.SynchronizeRemoteLogsAsync(
                _source,
                _context.DataDirectory,
                cancellationToken);

            if (result.ChangedFilePaths.Count > 0)
            {
                await _context.HostServices.LoadLogFilesAsync(
                    result.ChangedFilePaths,
                    cancellationToken);
            }

            var message = $"{result.AddedCount} neu, {result.UpdatedCount} aktualisiert, "
                + $"{result.UnchangedCount} unverändert, {result.MissingCount} remote entfernt."
                + (result.Failures.Count == 0 ? string.Empty : $" {result.Failures.Count} Fehler.");
            return new PluginActionResult(result.Succeeded, message);
        }
        catch (OperationCanceledException)
        {
            return new PluginActionResult(false, "OneDrive-Aktualisierung abgebrochen.");
        }
        catch (Exception ex)
        {
            _context.Log(PluginLogLevel.Error, "OneDrive-Aktualisierung fehlgeschlagen.", ex);
            return new PluginActionResult(false, ex.Message);
        }
    }

    private sealed class OneDrivePanel(
        Func<CancellationToken, ValueTask<PluginActionResult>> refresh) : IPluginPanelContribution
    {
        public string PanelId => "logalyzer.onedrive";

        public string DisplayName => "OneDrive";

        public IReadOnlyList<IPluginAction> Actions { get; } =
        [
            new OneDriveRefreshAction(refresh)
        ];
    }

    private sealed class OneDriveRefreshAction(
        Func<CancellationToken, ValueTask<PluginActionResult>> refresh) : IPluginAction
    {
        public PluginActionDescriptor Descriptor { get; } = new(
            "logalyzer.onedrive.refresh",
            "Aktualisieren",
            IsPrimary: true);

        public ValueTask<PluginActionResult> ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            return refresh(cancellationToken);
        }
    }
}