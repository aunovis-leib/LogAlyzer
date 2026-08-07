using System.Runtime.CompilerServices;
using System.Text;
using LogAlyzer.PluginContracts;
using LogAlyzer.PluginHost;
using Xunit;

namespace LogAlyzer.Tests.PluginHost;

public sealed class RemoteLogSynchronizerTests
{
    [Fact]
    public async Task PreviewAsync_DoesNotDownloadUntilFilesAreApproved()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "LogAlyzerTests",
            "RemotePreview_" + Guid.NewGuid().ToString("N"));
        var destinationDirectory = Path.Combine(rootDirectory, "Logs");
        var pluginDataDirectory = Path.Combine(rootDirectory, "PluginData");
        Directory.CreateDirectory(rootDirectory);

        var source = new FakeRemoteLogSource(
            [new RemoteLogFile("new-file", "new.log", "new.log", 3, null, "v1")],
            new Dictionary<string, string> { ["new-file"] = "new" });
        var synchronizer = new RemoteLogSynchronizer();

        try
        {
            var preview = await synchronizer.PreviewAsync(
                source,
                pluginDataDirectory,
                TestContext.Current.CancellationToken);

            Assert.True(preview.HasPendingFiles);
            Assert.Equal(1, preview.NewCount);
            Assert.False(Directory.Exists(destinationDirectory));

            var result = await synchronizer.SynchronizeAsync(
                source,
                destinationDirectory,
                pluginDataDirectory,
                TestContext.Current.CancellationToken,
                preview.PendingFiles
                    .Select(file => file.File.Id)
                    .ToHashSet(StringComparer.Ordinal));

            Assert.Equal(1, result.AddedCount);
            Assert.Equal(
                "new",
                await File.ReadAllTextAsync(
                    Path.Combine(destinationDirectory, "onedrive", "new.log"),
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SynchronizeAsync_UsesRemoteDateAndRetainsFilesMissingRemotely()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "LogAlyzerTests",
            "RemoteSync_" + Guid.NewGuid().ToString("N"));
        var destinationDirectory = Path.Combine(rootDirectory, "Logs");
        var pluginDataDirectory = Path.Combine(rootDirectory, "PluginData");
        Directory.CreateDirectory(rootDirectory);

        var firstModified = new DateTimeOffset(2026, 8, 7, 8, 0, 0, TimeSpan.Zero);
        var secondModified = firstModified.AddMinutes(10);
        var source = new FakeRemoteLogSource(
            [
                new RemoteLogFile("app", "app.log", "services/app.log", 2, firstModified, "v1"),
                new RemoteLogFile("old", "old.log", "archive/old.log", 3, firstModified, "v1")
            ],
            new Dictionary<string, string>
            {
                ["app"] = "v1",
                ["old"] = "old"
            });
        var synchronizer = new RemoteLogSynchronizer();

        try
        {
            var firstResult = await synchronizer.SynchronizeAsync(
                source,
                destinationDirectory,
                pluginDataDirectory,
                TestContext.Current.CancellationToken);

            Assert.Equal(2, firstResult.AddedCount);
            Assert.Equal(0, firstResult.UpdatedCount);
            Assert.Equal(2, firstResult.ChangedFilePaths.Count);

            var appPath = Path.Combine(destinationDirectory, "onedrive", "services", "app.log");
            var oldPath = Path.Combine(destinationDirectory, "onedrive", "archive", "old.log");
            Assert.Equal("v1", await File.ReadAllTextAsync(appPath, TestContext.Current.CancellationToken));
            Assert.True(File.Exists(oldPath));

            source.SetFiles(
                [new RemoteLogFile("app", "app.log", "services/app.log", 2, firstModified, "v1")]);
            var unchangedResult = await synchronizer.SynchronizeAsync(
                source,
                destinationDirectory,
                pluginDataDirectory,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, unchangedResult.UnchangedCount);
            Assert.Equal(1, unchangedResult.MissingCount);
            Assert.Empty(unchangedResult.ChangedFilePaths);
            Assert.True(File.Exists(oldPath));

            source.SetFileContent("app", "v2");
            source.SetFiles(
                [new RemoteLogFile("app", "app.log", "services/app.log", 2, secondModified, "v2")]);
            var updatedResult = await synchronizer.SynchronizeAsync(
                source,
                destinationDirectory,
                pluginDataDirectory,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, updatedResult.UpdatedCount);
            Assert.Equal(appPath, Assert.Single(updatedResult.ChangedFilePaths));
            Assert.Equal("v2", await File.ReadAllTextAsync(appPath, TestContext.Current.CancellationToken));

            source.SetFileContent("app", "older");
            source.SetFiles(
                [new RemoteLogFile(
                    "app",
                    "app.log",
                    "services/app.log",
                    5,
                    firstModified,
                    "v3")]);
            var olderResult = await synchronizer.SynchronizeAsync(
                source,
                destinationDirectory,
                pluginDataDirectory,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, olderResult.UnchangedCount);
            Assert.Empty(olderResult.ChangedFilePaths);
            Assert.Equal("v2", await File.ReadAllTextAsync(appPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    private sealed class FakeRemoteLogSource : IRemoteLogSource
    {
        private IReadOnlyList<RemoteLogFile> _files;
        private readonly Dictionary<string, string> _contents;

        public FakeRemoteLogSource(
            IReadOnlyList<RemoteLogFile> files,
            Dictionary<string, string> contents)
        {
            _files = files;
            _contents = contents;
        }

        public RemoteSourceDescriptor Descriptor { get; } = new("onedrive", "OneDrive");

        public async IAsyncEnumerable<RemoteLogFile> ListLogFilesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var file in _files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
                await Task.Yield();
            }
        }

        public ValueTask<Stream> OpenReadAsync(
            RemoteLogFile file,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(
                new MemoryStream(Encoding.UTF8.GetBytes(_contents[file.Id])));
        }

        public void SetFiles(IReadOnlyList<RemoteLogFile> files)
        {
            _files = files;
        }

        public void SetFileContent(string id, string content)
        {
            _contents[id] = content;
        }
    }
}