using LogAlyzer.TestPluginFixture;
using LogAlyzer.PluginHost;
using Xunit;

namespace LogAlyzer.Tests.PluginHost;

public sealed class PluginManagerTests
{
    [Fact]
    public async Task LoadPluginsAsync_WhenPluginDirectoryIsMissing_KeepsApplicationAvailable()
    {
        var rootDirectory = CreateTestDirectory();
        var pluginsDirectory = Path.Combine(rootDirectory, "Plugins");
        var dataDirectory = Path.Combine(rootDirectory, "PluginData");
        var manager = new PluginManager(pluginsDirectory, dataDirectory);

        try
        {
            await manager.LoadPluginsAsync(TestContext.Current.CancellationToken);

            Assert.Empty(manager.LoadedPlugins);
            Assert.Empty(manager.Failures);
        }
        finally
        {
            await manager.DisposeAsync();
            DeleteTestDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadPluginsAsync_WhenPluginIsValid_LoadsPluginAndCreatesDataDirectory()
    {
        var rootDirectory = CreateTestDirectory();
        var pluginDirectory = CreatePluginDirectory(rootDirectory, "ValidPlugin");
        var entryAssemblyName = Path.GetFileName(TestPluginAssemblyPath);
        File.Copy(TestPluginAssemblyPath, Path.Combine(pluginDirectory, entryAssemblyName));
        await WriteManifestAsync(
            pluginDirectory,
            "tests.valid-plugin",
            apiVersion: "1.0",
            entryAssembly: entryAssemblyName,
            entryType: "LogAlyzer.TestPluginFixture.TestPlugin");

        var manager = CreateManager(rootDirectory);

        LoadedPlugin? loadedPlugin = null;

        try
        {
            await manager.LoadPluginsAsync(TestContext.Current.CancellationToken);

            loadedPlugin = Assert.Single(manager.LoadedPlugins);
            Assert.Equal("tests.valid-plugin", loadedPlugin.Info.Id);
            Assert.Equal("Valid test plugin", loadedPlugin.Info.Name);
            Assert.True(Directory.Exists(Path.Combine(
                rootDirectory,
                "PluginData",
                "tests.valid-plugin")));
            Assert.Empty(manager.Failures);
        }
        finally
        {
            await manager.DisposeAsync();
            loadedPlugin = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            DeleteTestDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadPluginsAsync_WhenManifestIsInvalid_RecordsFailureAndContinues()
    {
        var rootDirectory = CreateTestDirectory();
        var pluginsDirectory = Path.Combine(rootDirectory, "Plugins");
        var pluginDirectory = Path.Combine(pluginsDirectory, "InvalidPlugin");
        var dataDirectory = Path.Combine(rootDirectory, "PluginData");
        Directory.CreateDirectory(pluginDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, PluginManager.ManifestFileName),
            "{}",
            TestContext.Current.CancellationToken);

        var manager = new PluginManager(pluginsDirectory, dataDirectory);

        try
        {
            await manager.LoadPluginsAsync(TestContext.Current.CancellationToken);

            Assert.Empty(manager.LoadedPlugins);
            var failure = Assert.Single(manager.Failures);
            Assert.Equal(pluginDirectory, failure.PluginDirectory);
            Assert.Null(failure.PluginId);
        }
        finally
        {
            await manager.DisposeAsync();
            DeleteTestDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadPluginsAsync_WhenApiVersionIsUnsupported_RecordsPluginId()
    {
        var rootDirectory = CreateTestDirectory();
        var pluginDirectory = CreatePluginDirectory(rootDirectory, "UnsupportedApi");
        await WriteManifestAsync(
            pluginDirectory,
            "unsupported.api",
            apiVersion: "99.0",
            entryAssembly: "Plugin.dll",
            entryType: "Test.Plugin");

        var manager = CreateManager(rootDirectory);

        try
        {
            await manager.LoadPluginsAsync(TestContext.Current.CancellationToken);

            Assert.Empty(manager.LoadedPlugins);
            var failure = Assert.Single(manager.Failures);
            Assert.Equal("unsupported.api", failure.PluginId);
            Assert.Contains("konnte nicht geladen werden", failure.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await manager.DisposeAsync();
            DeleteTestDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadPluginsAsync_WhenEntryAssemblyIsMissing_RecordsFailure()
    {
        var rootDirectory = CreateTestDirectory();
        var pluginDirectory = CreatePluginDirectory(rootDirectory, "MissingAssembly");
        await WriteManifestAsync(
            pluginDirectory,
            "missing.assembly",
            apiVersion: "1.0",
            entryAssembly: "Plugin.dll",
            entryType: "Test.Plugin");

        var manager = CreateManager(rootDirectory);

        try
        {
            await manager.LoadPluginsAsync(TestContext.Current.CancellationToken);

            Assert.Empty(manager.LoadedPlugins);
            var failure = Assert.Single(manager.Failures);
            Assert.Equal("missing.assembly", failure.PluginId);
            Assert.NotNull(failure.Exception);
            Assert.IsType<FileNotFoundException>(failure.Exception);
        }
        finally
        {
            await manager.DisposeAsync();
            DeleteTestDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadPluginsAsync_WhenEntryAssemblyEscapesPluginDirectory_RecordsFailure()
    {
        var rootDirectory = CreateTestDirectory();
        var pluginDirectory = CreatePluginDirectory(rootDirectory, "PathTraversal");
        await WriteManifestAsync(
            pluginDirectory,
            "path.traversal",
            apiVersion: "1.0",
            entryAssembly: "..\\\\Plugin.dll",
            entryType: "Test.Plugin");

        var manager = CreateManager(rootDirectory);

        try
        {
            await manager.LoadPluginsAsync(TestContext.Current.CancellationToken);

            Assert.Empty(manager.LoadedPlugins);
            var failure = Assert.Single(manager.Failures);
            Assert.Equal("path.traversal", failure.PluginId);
            Assert.NotNull(failure.Exception);
            Assert.IsType<InvalidDataException>(failure.Exception);
        }
        finally
        {
            await manager.DisposeAsync();
            DeleteTestDirectory(rootDirectory);
        }
    }

    private static PluginManager CreateManager(string rootDirectory)
    {
        return new PluginManager(
            Path.Combine(rootDirectory, "Plugins"),
            Path.Combine(rootDirectory, "PluginData"));
    }

    private static string TestPluginAssemblyPath => typeof(TestPlugin).Assembly.Location;

    private static string CreatePluginDirectory(string rootDirectory, string name)
    {
        var directory = Path.Combine(rootDirectory, "Plugins", name);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task WriteManifestAsync(
        string pluginDirectory,
        string id,
        string apiVersion,
        string entryAssembly,
        string entryType)
    {
        var manifest = $$"""
            {
              "id": "{{id}}",
              "name": "Test plugin",
              "version": "1.0.0",
              "apiVersion": "{{apiVersion}}",
              "entryAssembly": "{{entryAssembly}}",
              "entryType": "{{entryType}}"
            }
            """;

        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, PluginManager.ManifestFileName),
            manifest,
            TestContext.Current.CancellationToken);
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "LogAlyzerTests",
            "PluginManager_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTestDirectory(string directory)
    {
        for (var attempt = 0; attempt < 5 && Directory.Exists(directory); attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                if (attempt == 4)
                {
                    return;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 4)
                {
                    return;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}