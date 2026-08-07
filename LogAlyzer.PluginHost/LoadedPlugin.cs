using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

public sealed class LoadedPlugin
{
    internal LoadedPlugin(
        PluginInfo info,
        string directoryPath,
        ILogAlyzerPlugin instance,
        PluginLoadContext loadContext)
    {
        Info = info;
        DirectoryPath = directoryPath;
        Instance = instance;
        LoadContext = loadContext;
    }

    public PluginInfo Info { get; }

    public string DirectoryPath { get; }

    public ILogAlyzerPlugin Instance { get; }

    internal PluginLoadContext LoadContext { get; }
}