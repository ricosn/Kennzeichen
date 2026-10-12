using System.Runtime.CompilerServices;
using Kennzeichen.Abstractions;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Kennzeichen.Sources.OpenCv;

/// <summary>
/// Liest Bilddateien aus einem Ordner, zum Testen ohne Kamera: erst alle vorhandenen, danach jede neu
/// hinzugefügte Datei. Die Quellkennung ist <c>&lt;Kameraname&gt;/&lt;Dateiname&gt;</c>, damit jedes Bild einzeln gemeldet wird.
/// </summary>
public sealed class FolderFrameSource(CameraOptions options, ILogger<FolderFrameSource> logger) : IFrameSource
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };

    public async IAsyncEnumerable<Frame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var folder = Path.GetFullPath(options.Url, AppContext.BaseDirectory);
        Directory.CreateDirectory(folder);
        logger.LogInformation("Kamera {Camera}: lese Bilder aus Ordner {Folder}.", options.Name, folder);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var interval = TimeSpan.FromMilliseconds(Math.Max(options.IntervalMilliseconds, 100));
        while (!cancellationToken.IsCancellationRequested)
        {
            var files = Directory.EnumerateFiles(folder)
                .Where(f => Extensions.Contains(Path.GetExtension(f)) && seen.Add(f))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files)
            {
                var frame = TryRead(file);
                if (frame is not null)
                {
                    yield return frame;
                }
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    private Frame? TryRead(string file)
    {
        using var image = Cv2.ImRead(file, ImreadModes.Color);
        if (image.Empty())
        {
            logger.LogWarning("Kamera {Camera}: {File} ist kein lesbares Bild.", options.Name, Path.GetFileName(file));
            return null;
        }

        logger.LogInformation("Kamera {Camera}: werte {File} aus ({Width}x{Height}).", options.Name, Path.GetFileName(file), image.Width, image.Height);
        return FrameConverter.ToFrame(image, $"{options.Name}/{Path.GetFileName(file)}");
    }
}
