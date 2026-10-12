using Kennzeichen.Abstractions;
using Kennzeichen.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kennzeichen.Tests;

public class PluginLoaderTests
{
    [Fact]
    public void Loads_onnx_plugin_from_its_own_folder_with_shared_interface_types()
    {
        // Ausgabe des Plugin-Projekts in denselben Aufbau kopieren, den der Dienst erwartet: plugins/<Name>/<Name>.dll
        var pluginBuild = AppContext.BaseDirectory.Replace(
            Path.Combine("tests", "Kennzeichen.Tests"), Path.Combine("plugins", "Kennzeichen.Recognizers.Onnx"));
        Assert.True(File.Exists(Path.Combine(pluginBuild, "Kennzeichen.Recognizers.Onnx.dll")), $"Plugin nicht gebaut: {pluginBuild}");

        var root = Directory.CreateTempSubdirectory("kennzeichen-plugins-");
        try
        {
            var target = Directory.CreateDirectory(Path.Combine(root.FullName, "Kennzeichen.Recognizers.Onnx"));
            foreach (var file in Directory.EnumerateFiles(pluginBuild))
            {
                File.Copy(file, Path.Combine(target.FullName, Path.GetFileName(file)));
            }

            var factories = new PluginLoader(NullLogger<PluginLoader>.Instance).LoadFactories(root.FullName);

            var factory = Assert.Single(factories);
            Assert.Equal("Onnx", factory.Name);

            // Das Plugin kommt aus einem eigenen Ladekontext, die Schnittstelle aber vom Dienst.
            Assert.IsAssignableFrom<IPlateRecognizerFactory>(factory);
            Assert.NotSame(
                System.Runtime.Loader.AssemblyLoadContext.Default,
                System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(factory.GetType().Assembly));
        }
        finally
        {
            // Unter Windows bleibt die geladene Plugin-DLL bis Prozessende gesperrt; Aufräumen ist daher nur ein Versuch.
            try
            {
                root.Delete(recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    [Fact]
    public void Ignores_missing_plugin_directory()
    {
        var factories = new PluginLoader(NullLogger<PluginLoader>.Instance).LoadFactories(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.Empty(factories);
    }

    [Theory]
    [InlineData("Kennzeichen.Abstractions", true)]
    [InlineData("Microsoft.Extensions.Logging.Abstractions", true)]
    [InlineData("OpenCvSharp", false)]
    [InlineData("Microsoft.ML.OnnxRuntime", false)]
    public void Shares_only_contract_assemblies_with_the_host(string name, bool shared)
    {
        Assert.Equal(shared, PluginLoadContext.IsShared(name));
    }
}
