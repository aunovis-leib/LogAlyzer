using System.Text.Json;
using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

public sealed class PluginManager : IAsyncDisposable
{
    public const string ManifestFileName = "plugin.json";
    public const string PluginsDirectoryName = "Plugins";
    public const string PluginDataDirectoryName = "PluginData";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _pluginsDirectory;
    private readonly string _dataDirectory;
    private readonly Action<PluginLogLevel, string, Exception?> _log;
    private readonly IPluginHostServices _hostServices;
    private readonly List<LoadedPlugin> _loadedPlugins = [];
    private readonly List<PluginLoadFailure> _failures = [];
    private readonly HashSet<string> _loadedPluginIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadAttempted;

    public PluginManager(
        string pluginsDirectory,
        string dataDirectory,
        Action<PluginLogLevel, string, Exception?>? log = null,
        IPluginHostServices? hostServices = null)
    {
        if (string.IsNullOrWhiteSpace(pluginsDirectory))
        {
            throw new ArgumentException("A plugin directory is required.", nameof(pluginsDirectory));
        }

        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("A plugin data directory is required.", nameof(dataDirectory));
        }

        _pluginsDirectory = Path.GetFullPath(pluginsDirectory);
        _dataDirectory = Path.GetFullPath(dataDirectory);
        _log = log ?? ((_, _, _) => { });
        _hostServices = hostServices ?? new NullPluginHostServices();
    }

    public IReadOnlyList<LoadedPlugin> LoadedPlugins => _loadedPlugins;

    public IReadOnlyList<PluginLoadFailure> Failures => _failures;

    public event EventHandler? PluginsChanged;

    public async Task LoadPluginsAsync(CancellationToken cancellationToken = default)
    {
        if (_loadAttempted)
        {
            return;
        }

        _loadAttempted = true;

        if (!Directory.Exists(_pluginsDirectory))
        {
            Log(PluginLogLevel.Debug, $"Kein Plugin-Verzeichnis vorhanden: {_pluginsDirectory}");
            return;
        }

        string[] pluginDirectories;
        try
        {
            pluginDirectories = Directory.EnumerateDirectories(_pluginsDirectory)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            RegisterFailure(_pluginsDirectory, null, "Das Plugin-Verzeichnis konnte nicht gelesen werden.", ex);
            return;
        }

        foreach (var pluginDirectory in pluginDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadPluginAsync(pluginDirectory, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        for (var index = _loadedPlugins.Count - 1; index >= 0; index--)
        {
            var loadedPlugin = _loadedPlugins[index];

            try
            {
                await loadedPlugin.Instance.ShutdownAsync();
            }
            catch (Exception ex)
            {
                Log(PluginLogLevel.Error, $"Plugin konnte nicht beendet werden: {loadedPlugin.Info.Id}", ex);
            }

            loadedPlugin.LoadContext.Unload();
        }

        _loadedPlugins.Clear();
        _loadedPluginIds.Clear();
    }

    private async Task LoadPluginAsync(
        string pluginDirectory,
        CancellationToken cancellationToken)
    {
        PluginManifest? manifest = null;
        PluginLoadContext? loadContext = null;
        var pluginIdAdded = false;

        try
        {
            var manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("Das Plugin-Manifest fehlt.", manifestPath);
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, ManifestJsonOptions)
                ?? throw new InvalidDataException("Das Plugin-Manifest ist leer.");

            ValidateManifest(manifest);

            var assemblyPath = ResolvePathInsideDirectory(pluginDirectory, manifest.EntryAssembly);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("Die Plugin-Assembly fehlt.", assemblyPath);
            }

            loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var pluginType = assembly.GetType(manifest.EntryType, throwOnError: true, ignoreCase: false)
                ?? throw new TypeLoadException($"Plugin-Typ nicht gefunden: {manifest.EntryType}");

            if (Activator.CreateInstance(pluginType) is not ILogAlyzerPlugin plugin)
            {
                throw new InvalidOperationException(
                    $"Der Einstiegstyp implementiert ILogAlyzerPlugin nicht: {manifest.EntryType}");
            }

            ValidatePluginInfo(manifest, plugin.Info);

            if (!_loadedPluginIds.Add(plugin.Info.Id))
            {
                throw new InvalidOperationException($"Plugin-ID ist bereits geladen: {plugin.Info.Id}");
            }

            pluginIdAdded = true;
            var dataDirectory = Path.Combine(_dataDirectory, plugin.Info.Id);
            Directory.CreateDirectory(dataDirectory);

            var context = new PluginContext(
                pluginDirectory,
                dataDirectory,
                _hostServices,
                (level, message, exception) => Log(level, $"[{plugin.Info.Id}] {message}", exception));

            await plugin.InitializeAsync(context, cancellationToken);

            _loadedPlugins.Add(new LoadedPlugin(
                plugin.Info,
                pluginDirectory,
                plugin,
                loadContext));
            PluginsChanged?.Invoke(this, EventArgs.Empty);

            Log(PluginLogLevel.Information, $"Plugin geladen: {plugin.Info.Id} ({plugin.Info.Version})");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (pluginIdAdded && manifest is not null)
            {
                _loadedPluginIds.Remove(manifest.Id);
            }

            loadContext?.Unload();
            throw;
        }
        catch (Exception ex)
        {
            if (pluginIdAdded && manifest is not null)
            {
                _loadedPluginIds.Remove(manifest.Id);
            }

            loadContext?.Unload();
            RegisterFailure(
                pluginDirectory,
                manifest is null || string.IsNullOrWhiteSpace(manifest.Id) ? null : manifest.Id,
                "Das Plugin konnte nicht geladen werden.",
                ex);
        }
    }

    private static void ValidateManifest(PluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidDataException("Das Plugin-Manifest enthält keine ID.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ApiVersion))
        {
            throw new InvalidDataException("Das Plugin-Manifest enthält keine API-Version.");
        }

        if (!string.Equals(manifest.ApiVersion, PluginApi.CurrentVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Nicht unterstützte Plugin-API-Version: {manifest.ApiVersion}");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            throw new InvalidDataException("Das Plugin-Manifest enthält keine EntryAssembly.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryType))
        {
            throw new InvalidDataException("Das Plugin-Manifest enthält keinen EntryType.");
        }
    }

    private static void ValidatePluginInfo(PluginManifest manifest, PluginInfo info)
    {
        if (info is null)
        {
            throw new InvalidDataException("Das Plugin liefert keine Plugin-Informationen.");
        }

        if (!string.Equals(manifest.Id, info.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Manifest-ID und Plugin-ID stimmen nicht überein.");
        }

        if (!string.Equals(info.ApiVersion, PluginApi.CurrentVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Nicht unterstützte Plugin-API-Version: {info.ApiVersion}");
        }
    }

    private static string ResolvePathInsideDirectory(string directory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("EntryAssembly muss ein relativer Pfad sein.");
        }

        var fullDirectory = Path.GetFullPath(directory);
        var fullPath = Path.GetFullPath(Path.Combine(fullDirectory, relativePath));
        var relativeToDirectory = Path.GetRelativePath(fullDirectory, fullPath);

        if (relativeToDirectory == ".."
            || relativeToDirectory.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToDirectory))
        {
            throw new InvalidDataException("EntryAssembly liegt außerhalb des Plugin-Verzeichnisses.");
        }

        return fullPath;
    }

    private void RegisterFailure(
        string pluginDirectory,
        string? pluginId,
        string reason,
        Exception exception)
    {
        _failures.Add(new PluginLoadFailure(pluginDirectory, pluginId, reason, exception));
        Log(PluginLogLevel.Error, reason, exception);
    }

    private void Log(PluginLogLevel level, string message, Exception? exception = null)
    {
        try
        {
            _log(level, message, exception);
        }
        catch
        {
        }
    }
}