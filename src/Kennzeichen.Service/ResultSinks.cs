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

/// <summary>
/// Schreibt die Ergebnisse eines Tages als JSON-Array nach <c>kennzeichen-JJJJ-MM-TT.json</c>.
/// Neue Einträge werden vor der schließenden Klammer angefügt, die Datei ist also jederzeit gültiges JSON.
/// </summary>
internal sealed class JsonFileResultSink(string directory, ILogger<JsonFileResultSink> logger) : IResultSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task PublishAsync(RecognitionResult result, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"kennzeichen-{result.Timestamp:yyyy-MM-dd}.json");
        var entry = Indent(JsonSerializer.Serialize(result, JsonOptions));

        await using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        var closingBracket = FindClosingBracket(stream);
        if (stream.Length > 0 && closingBracket < 0)
        {
            await stream.DisposeAsync();
            var damaged = path + $".defekt-{DateTime.Now:HHmmss}";
            File.Move(path, damaged);
            logger.LogWarning("Ergebnisdatei {Path} war kein gültiges JSON-Array; umbenannt in {Damaged}.", path, damaged);
            await PublishAsync(result, cancellationToken);
            return;
        }

        string text;
        if (closingBracket < 0)
        {
            text = "[\n" + entry + "\n]\n";
        }
        else
        {
            stream.Position = closingBracket;
            var isEmptyArray = IsEmptyArray(stream, closingBracket);
            text = (isEmptyArray ? "" : ",") + "\n" + entry + "\n]\n";
            stream.Position = isEmptyArray ? closingBracket : TrimTrailingWhitespace(stream, closingBracket);
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes, cancellationToken);
        stream.SetLength(stream.Position);
    }

    private static string Indent(string json) => "  " + json.Replace("\n", "\n  ", StringComparison.Ordinal);

    /// <summary>Position der letzten <c>]</c>, wenn danach nur Leerraum folgt; sonst -1.</summary>
    private static long FindClosingBracket(FileStream stream)
    {
        for (var position = stream.Length - 1; position >= 0; position--)
        {
            stream.Position = position;
            var b = stream.ReadByte();
            if (b == ']')
            {
                return position;
            }

            if (!char.IsWhiteSpace((char)b))
            {
                return -1;
            }
        }

        return -1;
    }

    /// <summary>Wahr, wenn vor der schließenden Klammer nur Leerraum und die öffnende Klammer stehen.</summary>
    private static bool IsEmptyArray(FileStream stream, long closingBracket)
    {
        for (var position = closingBracket - 1; position >= 0; position--)
        {
            stream.Position = position;
            var b = stream.ReadByte();
            if (!char.IsWhiteSpace((char)b))
            {
                return b == '[';
            }
        }

        return false;
    }

    /// <summary>Position direkt nach dem letzten Eintrag, also vor dem Leerraum vor der schließenden Klammer.</summary>
    private static long TrimTrailingWhitespace(FileStream stream, long closingBracket)
    {
        var position = closingBracket;
        while (position > 0)
        {
            stream.Position = position - 1;
            if (!char.IsWhiteSpace((char)stream.ReadByte()))
            {
                break;
            }

            position--;
        }

        return position;
    }
}
