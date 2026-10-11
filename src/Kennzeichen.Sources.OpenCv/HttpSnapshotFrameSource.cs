using System.Net;
using System.Runtime.CompilerServices;
using Kennzeichen.Abstractions;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Kennzeichen.Sources.OpenCv;

/// <summary>Fragt in festem Abstand ein JPEG per HTTP ab. Basic- und Digest-Anmeldung übernimmt <see cref="HttpClientHandler"/>.</summary>
public sealed class HttpSnapshotFrameSource(CameraOptions options, ILogger<HttpSnapshotFrameSource> logger) : IFrameSource
{
    public async IAsyncEnumerable<Frame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(options.Username))
        {
            handler.Credentials = new NetworkCredential(options.Username, options.Password);
        }

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.IntervalMilliseconds));

        do
        {
            var frame = await TryFetchAsync(client, cancellationToken);
            if (frame is not null)
            {
                yield return frame;
            }
        }
        while (await WaitAsync(timer, cancellationToken));
    }

    private async Task<Frame?> TryFetchAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await client.GetByteArrayAsync(options.Url, cancellationToken);
            using var image = Cv2.ImDecode(bytes, ImreadModes.Color);
            if (image.Empty())
            {
                logger.LogWarning("Kamera {Camera}: Antwort ist kein lesbares Bild ({Length} Byte).", options.Name, bytes.Length);
                return null;
            }

            return FrameConverter.ToFrame(image, options.Name);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Kamera {Camera}: Abruf von {Url} fehlgeschlagen: {Message}", options.Name, options.Url, ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Kamera {Camera}: Zeitüberschreitung bei {Url}.", options.Name, options.Url);
        }

        return null;
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
