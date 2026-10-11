using System.Text.Json;
using Kennzeichen.Abstractions;

namespace Kennzeichen.Service;

/// <summary>Schreibt jedes Ergebnis ins Log (als Windows-Dienst: Ereignisanzeige, Anwendung).</summary>
internal sealed class LogResultSink(ILogger<LogResultSink> logger) : IResultSink
{
    public Task PublishAsync(RecognitionResult result, CancellationToken cancellationToken)
    {
        var r = result.Reading;
        logger.LogInformation(
            "Kennzeichen {Text} an {Source} (OCR {TextConfidence:P0}, Detektor {DetectionConfidence:P0}, Region {Region})",
            r.Text, result.SourceId, r.TextConfidence, r.DetectionConfidence, r.Region ?? "-");
        return Task.CompletedTask;
    }
}

/// <summary>Hängt jedes Ergebnis als JSON-Zeile an <c>kennzeichen-JJJJ-MM-TT.jsonl</c> an.</summary>
internal sealed class JsonLinesResultSink(string directory) : IResultSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(RecognitionResult result, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"kennzeichen-{result.Timestamp:yyyy-MM-dd}.jsonl");
        var line = JsonSerializer.Serialize(result, JsonOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(path, line, cancellationToken);
    }
}
