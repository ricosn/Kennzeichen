using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Kennzeichen.Abstractions;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Kennzeichen.Sources.OpenCv;

/// <summary>
/// Liest einen RTSP-Stream über OpenCV/FFmpeg. Ein eigener Thread liest ununterbrochen, damit der
/// Puffer nicht volläuft; weitergegeben wird jeweils nur das neueste Bild.
/// Um CPU zu sparen, wird jedes Bild nur dekodiert (<c>Grab</c>); in BGR umgerechnet und kopiert
/// (<c>Retrieve</c>) wird es nur, wenn seit dem letzten weitergegebenen Bild <see cref="CameraOptions.IntervalMilliseconds"/>
/// vergangen sind. In OpenCV dekodiert <c>grabFrame</c> das Bild, erst <c>retrieveFrame</c> rechnet es per
/// <c>sws_scale</c> um (modules/videoio/src/cap_ffmpeg_impl.hpp).
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

        try
        {
            await foreach (var frame in latest.Reader.ReadAllAsync(cancellationToken))
            {
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
        var interval = TimeSpan.FromMilliseconds(options.IntervalMilliseconds);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        using var image = new Mat();

        while (!cancellationToken.IsCancellationRequested)
        {
            using (var capture = new VideoCapture(url, VideoCaptureAPIs.FFMPEG, OpenParameters(options)))
            {
                if (!capture.IsOpened())
                {
                    logger.LogWarning("Kamera {Camera}: Stream {Url} lässt sich nicht öffnen.", options.Name, options.Url);
                }
                else
                {
                    logger.LogInformation(
                        "Kamera {Camera}: Stream {Url} verbunden, {Width}x{Height} bei {Fps} Bildern/s, Hardware-Dekodierung: {Acceleration}.",
                        options.Name,
                        options.Url,
                        capture.FrameWidth,
                        capture.FrameHeight,
                        capture.Fps,
                        (VideoAccelerationType)(int)capture.Get(VideoCaptureProperties.HwAcceleration));

                    var lastDelivered = -interval;
                    while (!cancellationToken.IsCancellationRequested && capture.Grab())
                    {
                        if (clock.Elapsed - lastDelivered < interval)
                        {
                            continue;
                        }

                        if (!capture.Retrieve(image) || image.Empty())
                        {
                            break;
                        }

                        lastDelivered = clock.Elapsed;
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

    /// <summary>Parameter beim Öffnen (Paare aus Eigenschaft und Wert), siehe <c>cv::VideoCaptureProperties</c>.</summary>
    public static int[] OpenParameters(CameraOptions options)
    {
        // VIDEO_ACCELERATION_ANY: "Prefer to use H/W acceleration. If no one supported, then fallback to software
        // processing." (opencv2/videoio.hpp). Ohne Angabe nutzt das FFmpeg-Backend keine Hardware.
        var acceleration = options.HardwareDecoding ? VideoAccelerationType.Any : VideoAccelerationType.None;
        return [(int)VideoCaptureProperties.HwAcceleration, (int)acceleration];
    }
}
