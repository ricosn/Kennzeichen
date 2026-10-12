using System.Reflection;
using System.Runtime.Loader;

namespace Kennzeichen.Service;

/// <summary>
/// Lädt ein Plugin mit seinen eigenen Abhängigkeiten isoliert vom Dienst, nach
/// https://learn.microsoft.com/dotnet/core/tutorials/creating-app-with-plugin-support.
/// Die Schnittstellen und Microsoft.Extensions.* kommen immer vom Dienst, damit die Typen auf beiden Seiten gleich sind.
/// </summary>
internal sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(Path.GetFileNameWithoutExtension(pluginPath))
{
    private static readonly string[] SharedPrefixes = ["Kennzeichen.Abstractions", "Microsoft.Extensions."];

    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    internal static bool IsShared(string? assemblyName) =>
        assemblyName is not null && SharedPrefixes.Any(p => assemblyName.StartsWith(p, StringComparison.Ordinal));

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsShared(assemblyName.Name))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
