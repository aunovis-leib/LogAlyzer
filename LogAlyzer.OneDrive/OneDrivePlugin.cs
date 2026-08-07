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
            var preview = await _context.HostServices.PreviewRemoteLogsAsync(
                _source,
                _context.DataDirectory,
                cancellationToken);

            if (!preview.Succeeded)
            {
                return new PluginActionResult(false, FormatPreviewFailures(preview));
            }

            if (!preview.HasPendingFiles)
            {
                return new PluginActionResult(
                    true,
                    $"Keine neuen oder geänderten Dateien. {preview.UnchangedCount} unverändert, "
                    + $"{preview.MissingCount} remote entfernt.");
            }

            var confirmationMessage = BuildConfirmationMessage(preview);
            var confirmed = await _context.HostServices.ConfirmAsync(
                "OneDrive-Download bestätigen",
                confirmationMessage,
                cancellationToken);
            if (!confirmed)
            {
                return new PluginActionResult(
                    true,
                    $"Download nicht bestätigt. {preview.PendingFiles.Count} Dateien bleiben ausstehend.");
            }

            var approvedFileIds = preview.PendingFiles
                .Select(file => file.File.Id)
                .ToHashSet(StringComparer.Ordinal);
            var result = await _context.HostServices.SynchronizeRemoteLogsAsync(
                _source,
                _context.DataDirectory,
                cancellationToken,
                approvedFileIds);

            if (result.ChangedFilePaths.Count > 0)
            {
                await _context.HostServices.LoadLogFilesAsync(
                    result.ChangedFilePaths,
                    cancellationToken);
            }

            var message = FormatSyncResult(result);
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

    private static string BuildConfirmationMessage(RemoteSyncPreview preview)
    {
        const int maxDisplayedFiles = 20;
        var displayedFiles = preview.PendingFiles
            .Take(maxDisplayedFiles)
            .Select(file => $"- {file.File.RelativePath ?? file.File.Name}");
        var hiddenCount = preview.PendingFiles.Count - maxDisplayedFiles;
        var remainingMessage = hiddenCount > 0
            ? Environment.NewLine + $"... und {hiddenCount} weitere Dateien"
            : string.Empty;

        return $"{preview.NewCount} neue und {preview.UpdatedCount} geänderte Dateien gefunden."
            + Environment.NewLine + Environment.NewLine
            + string.Join(Environment.NewLine, displayedFiles)
            + remainingMessage
            + Environment.NewLine + Environment.NewLine
            + "Sollen diese Dateien heruntergeladen werden?";
    }

    private static string FormatPreviewFailures(RemoteSyncPreview preview)
    {
        var details = string.Join(
            Environment.NewLine,
            preview.Failures.Select(failure => $"- {failure.FileName}: {failure.Message}"));
        return $"Remote-Dateien konnten nicht geprüft werden.{Environment.NewLine}{details}";
    }

    private static string FormatSyncResult(RemoteSyncResult result)
    {
        return $"{result.AddedCount} neu, {result.UpdatedCount} aktualisiert, "
            + $"{result.UnchangedCount} unverändert, {result.MissingCount} remote entfernt."
            + (result.Failures.Count == 0 ? string.Empty : $" {result.Failures.Count} Fehler.");
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