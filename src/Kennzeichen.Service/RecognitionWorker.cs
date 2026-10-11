using Kennzeichen.Abstractions;
using Microsoft.Extensions.Options;

namespace Kennzeichen.Service;

/// <summary>Holt Bilder von der Kamera, lässt sie erkennen, filtert und gibt Ergebnisse aus.</summary>
internal sealed class RecognitionWorker(
    IFrameSource source,
    IPlateRecognizer recognizer,
    IEnumerable<IResultSink> sinks,
    IOptions<RecognizerOptions> recognizerOptions,
    IOptions<ResultOptions> resultOptions,
    ILogger<RecognitionWorker> logger) : BackgroundService
{
    private readonly ResultOptions _results = resultOptions.Value;
    private readonly RepeatFilter _repeats = new(TimeSpan.FromSeconds(resultOptions.Value.SuppressRepeatSeconds));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var frame in source.ReadFramesAsync(stoppingToken))
            {
                await ProcessAsync(frame, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Dienst wird beendet.
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Erkennung abgebrochen.");

            // Nur bei Exitcode ungleich 0 greifen die Wiederherstellungsoptionen des Windows-Dienstes,
            // siehe https://learn.microsoft.com/dotnet/core/extensions/windows-service
            Environment.Exit(1);
        }
    }

    internal async Task ProcessAsync(Frame frame, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlateReading> readings;
        try
        {
            readings = await recognizer.RecognizeAsync(frame, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erkennung für ein Bild von {Source} fehlgeschlagen.", frame.SourceId);
            return;
        }

        foreach (var reading in readings)
        {
            if (reading.CompactText.Length < _results.MinTextLength || reading.TextConfidence < _results.MinTextConfidence)
            {
                logger.LogDebug("Verworfen: {Text} ({Confidence:P0})", reading.Text, reading.TextConfidence);
                continue;
            }

            if (!_repeats.ShouldReport(frame.SourceId, reading.CompactText, frame.Timestamp))
            {
                continue;
            }

            var result = new RecognitionResult(frame.SourceId, frame.Timestamp, recognizerOptions.Value.Name, reading);
            foreach (var sink in sinks)
            {
                try
                {
                    await sink.PublishAsync(result, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Ausgabe {Sink} fehlgeschlagen.", sink.GetType().Name);
                }
            }
        }
    }
}
