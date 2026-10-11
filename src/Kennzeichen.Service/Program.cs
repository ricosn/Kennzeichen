using Kennzeichen.Abstractions;
using Kennzeichen.Service;
using Kennzeichen.Sources.OpenCv;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,

    // Als Dienst ist das Arbeitsverzeichnis C:\Windows\System32; Konfiguration und relative Pfade gelten ab dem Programmordner.
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddWindowsService(options => options.ServiceName = "Kennzeichenerkennung");

builder.Services.Configure<CameraOptions>(builder.Configuration.GetSection("Camera"));
builder.Services.Configure<RecognizerOptions>(builder.Configuration.GetSection("Recognizer"));
builder.Services.Configure<ResultOptions>(builder.Configuration.GetSection("Results"));

builder.Services.AddSingleton<IFrameSource>(sp =>
{
    var camera = sp.GetRequiredService<IOptions<CameraOptions>>().Value;
    if (string.IsNullOrWhiteSpace(camera.Url))
    {
        throw new InvalidOperationException("Camera:Url ist nicht gesetzt.");
    }

    return camera.Type switch
    {
        CameraSourceType.Rtsp => ActivatorUtilities.CreateInstance<RtspFrameSource>(sp, camera),
        CameraSourceType.Snapshot => ActivatorUtilities.CreateInstance<HttpSnapshotFrameSource>(sp, camera),
        _ => throw new InvalidOperationException($"Unbekannter Camera:Type '{camera.Type}'."),
    };
});

builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<IPlateRecognizer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RecognizerOptions>>().Value;
    var baseDirectory = AppContext.BaseDirectory;
    var pluginDirectory = Path.GetFullPath(options.PluginDirectory, baseDirectory);
    var factories = sp.GetRequiredService<PluginLoader>().LoadFactories(pluginDirectory);
    var factory = factories.FirstOrDefault(f => string.Equals(f.Name, options.Name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Erkenner '{options.Name}' nicht gefunden. Gefunden in {pluginDirectory}: {string.Join(", ", factories.Select(f => f.Name))}");

    return factory.Create(
        builder.Configuration.GetSection("Recognizer:Settings"), baseDirectory, sp.GetRequiredService<ILoggerFactory>());
});

builder.Services.AddSingleton<IResultSink, LogResultSink>();
var jsonDirectory = builder.Configuration["Results:JsonLinesDirectory"] ?? new ResultOptions().JsonLinesDirectory;
if (!string.IsNullOrWhiteSpace(jsonDirectory))
{
    builder.Services.AddSingleton<IResultSink>(new JsonLinesResultSink(Path.GetFullPath(jsonDirectory, AppContext.BaseDirectory)));
}

builder.Services.AddHostedService<RecognitionWorker>();

builder.Build().Run();
