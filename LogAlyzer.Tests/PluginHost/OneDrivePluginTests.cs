using LogAlyzer.Plugins.OneDrive;
using LogAlyzer.PluginContracts;
using Xunit;

namespace LogAlyzer.Tests.PluginHost;

public sealed class OneDrivePluginTests
{
    [Fact]
    public async Task InitializeAsync_WithoutSettings_RegistersPanelAndReportsConfiguration()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "LogAlyzerTests",
            "OneDrivePlugin_" + Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(rootDirectory, "Plugin");
        var dataDirectory = Path.Combine(rootDirectory, "Data");
        Directory.CreateDirectory(pluginDirectory);
        var plugin = new OneDrivePlugin();

        try
        {
            await plugin.InitializeAsync(
                new TestPluginContext(pluginDirectory, dataDirectory),
                TestContext.Current.CancellationToken);

            var sourcePlugin = Assert.IsAssignableFrom<IRemoteLogSourcePlugin>(plugin);
            Assert.Single(sourcePlugin.Sources);

            var uiPlugin = Assert.IsAssignableFrom<IPluginUiContribution>(plugin);
            Assert.NotNull(uiPlugin.Panel);
            var action = Assert.Single(uiPlugin.Panel!.Actions);
            Assert.Equal("Aktualisieren", action.Descriptor.DisplayName);

            var result = await action.ExecuteAsync(TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Contains("ClientId fehlt", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await plugin.ShutdownAsync(TestContext.Current.CancellationToken);
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    private sealed class TestPluginContext(
        string pluginDirectory,
        string dataDirectory) : IPluginContext
    {
        public string PluginDirectory { get; } = pluginDirectory;

        public string DataDirectory { get; } = dataDirectory;

        public IPluginHostServices HostServices { get; } = new TestHostServices();

        public void Log(PluginLogLevel level, string message, Exception? exception = null)
        {
        }
    }

    private sealed class TestHostServices : IPluginHostServices
    {
        public string DefaultLogDirectory => Path.GetTempPath();

        public ValueTask<RemoteSyncPreview> PreviewRemoteLogsAsync(
            IRemoteLogSource source,
            string pluginDataDirectory,
            CancellationToken cancellationToken = default)
        {
            return PreviewAsync(source, cancellationToken);
        }

        private static async ValueTask<RemoteSyncPreview> PreviewAsync(
            IRemoteLogSource source,
            CancellationToken cancellationToken)
        {
            await foreach (var _ in source.ListLogFilesAsync(cancellationToken))
            {
                break;
            }

            return RemoteSyncPreview.Empty;
        }

        public async ValueTask<RemoteSyncResult> SynchronizeRemoteLogsAsync(
            IRemoteLogSource source,
            string pluginDataDirectory,
            CancellationToken cancellationToken = default,
            IReadOnlySet<string>? approvedFileIds = null)
        {
            await foreach (var _ in source.ListLogFilesAsync(cancellationToken))
            {
                break;
            }

            return RemoteSyncResult.Empty;
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
}