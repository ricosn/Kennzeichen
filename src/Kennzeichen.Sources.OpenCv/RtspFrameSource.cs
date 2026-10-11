using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Kennzeichen.Abstractions;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Kennzeichen.Sources.OpenCv;

/// <summary>
/// Liest einen RTSP-Stream über OpenCV/FFmpeg. Ein eigener Thread liest ununterbrochen, damit der
/// Puffer nicht volläuft; weitergegeben wird jeweils nur das neueste Bild.
/// </summary>
public sealed class RtspFrameSource(CameraOptions options, ILogger<RtspFrameSource> logger) : IFrameSource
{
    public async IAsyncEnumerable<Frame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var latest = Channel.CreateBounded<Frame>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

        var reader = Task.Factory.StartNew(
            () => ReadLoop(latest.Writer, cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        var interval = TimeSpan.FromMilliseconds(options.IntervalMilliseconds);
        var lastDelivered = DateTimeOffset.MinValue;
        try
        {
            await foreach (var frame in latest.Reader.ReadAllAsync(cancellationToken))
            {
                if (frame.Timestamp - lastDelivered < interval)
                {
                    continue;
                }

                lastDelivered = frame.Timestamp;
                yield return frame;
            }
        }
        finally
        {
            await reader.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private void ReadLoop(ChannelWriter<Frame> writer, CancellationToken cancellationToken)
    {
        var url = CameraUrl.WithCredentials(options.Url, options.Username, options.Password);
        var reconnectDelay = TimeSpan.FromSeconds(options.ReconnectDelaySeconds);
        using var image = new Mat();

        while (!cancellationToken.IsCancellationRequested)
        {
            using (var capture = new VideoCapture(url, VideoCaptureAPIs.FFMPEG))
            {
                if (!capture.IsOpened())
                {
                    logger.LogWarning("Kamera {Camera}: Stream {Url} lässt sich nicht öffnen.", options.Name, options.Url);
                }
                else
                {
                    logger.LogInformation("Kamera {Camera}: Stream {Url} verbunden.", options.Name, options.Url);
                    while (!cancellationToken.IsCancellationRequested && capture.Read(image) && !image.Empty())
                    {
                        writer.TryWrite(FrameConverter.ToFrame(image, options.Name));
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        logger.LogWarning("Kamera {Camera}: Stream abgebrochen.", options.Name);
                    }
                }
            }

            if (cancellationToken.WaitHandle.WaitOne(reconnectDelay))
            {
                break;
            }
        }

        writer.TryComplete();
    }
}
