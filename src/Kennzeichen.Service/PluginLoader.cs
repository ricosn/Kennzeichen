using Kennzeichen.Abstractions;

namespace Kennzeichen.Service;

/// <summary>
/// Sucht Erkenner-Plugins. Erwartete Ablage: <c>&lt;PluginDirectory&gt;/&lt;Name&gt;/&lt;Name&gt;.dll</c>,
/// also ein Unterordner je Plugin mit der Haupt-DLL gleichen Namens und allen Abhängigkeiten.
/// </summary>
internal sealed class PluginLoader(ILogger<PluginLoader> logger)
{
    public IReadOnlyList<IPlateRecognizerFactory> LoadFactories(string pluginDirectory)
    {
        var factories = new List<IPlateRecognizerFactory>();
        if (!Directory.Exists(pluginDirectory))
        {
            logger.LogWarning("Plugin-Ordner {Directory} existiert nicht.", pluginDirectory);
            return factories;
        }

        foreach (var directory in Directory.EnumerateDirectories(pluginDirectory))
        {
            var mainAssembly = Path.Combine(directory, Path.GetFileName(directory) + ".dll");
            if (!File.Exists(mainAssembly))
            {
                logger.LogWarning("Plugin-Ordner {Directory} enthält keine {File}; übersprungen.", directory, Path.GetFileName(mainAssembly));
                continue;
            }

            var context = new PluginLoadContext(mainAssembly);
            var assembly = context.LoadFromAssemblyPath(mainAssembly);
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type is { IsAbstract: false, IsInterface: false }
                    && typeof(IPlateRecognizerFactory).IsAssignableFrom(type)
                    && type.GetConstructor(Type.EmptyTypes) is not null)
                {
                    var factory = (IPlateRecognizerFactory)Activator.CreateInstance(type)!;
                    logger.LogInformation("Erkenner {Name} gefunden in {Assembly}.", factory.Name, Path.GetFileName(mainAssembly));
                    factories.Add(factory);
                }
            }
        }

        return factories;
    }
}
