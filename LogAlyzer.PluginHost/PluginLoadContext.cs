using System.Reflection;
using System.Runtime.Loader;
using LogAlyzer.PluginContracts;

namespace LogAlyzer.PluginHost;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _contractAssemblyName = typeof(ILogAlyzerPlugin).Assembly.GetName().Name!;

    public PluginLoadContext(string mainAssemblyPath)
        : base($"LogAlyzer.Plugin.{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, _contractAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return typeof(ILogAlyzerPlugin).Assembly;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    protected override IntPtr LoadUnmanagedDll(string name)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(name);
        return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
    }
}