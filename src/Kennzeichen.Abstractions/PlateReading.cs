namespace Kennzeichen.Abstractions;

/// <summary>Rechteck in Pixelkoordinaten des Originalbildes.</summary>
public readonly record struct PlateBox(int X, int Y, int Width, int Height);

/// <summary>Ein erkanntes Kennzeichen in einem Bild.</summary>
/// <param name="Text">Gelesener Text, Gruppen durch einzelne Leerzeichen getrennt, z. B. "M AB 1234" (oder "MAB1234", wenn der Erkenner keine Leerzeichen setzt).</param>
/// <param name="TextConfidence">Mittlere Zeichen-Konfidenz der OCR, 0..1.</param>
/// <param name="DetectionConfidence">Konfidenz des Detektors für die Kennzeichenposition, 0..1.</param>
/// <param name="Box">Position im Bild.</param>
/// <param name="Region">Vom Modell geschätztes Land oder Region, falls das Modell das liefert.</param>
public sealed record PlateReading(
    string Text,
    float TextConfidence,
    float DetectionConfidence,
    PlateBox Box,
    string? Region = null)
{
    /// <summary>Text ohne Leerzeichen; zum Vergleichen von Sichtungen.</summary>
    public string CompactText => Text.Replace(" ", "", StringComparison.Ordinal);
}

/// <summary>Ergebnis, das an die Ausgaben (<see cref="IResultSink"/>) geht.</summary>
public sealed record RecognitionResult(
    string SourceId,
    DateTimeOffset Timestamp,
    string RecognizerName,
    PlateReading Reading);
