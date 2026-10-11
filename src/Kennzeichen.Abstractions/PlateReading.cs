namespace Kennzeichen.Abstractions;

/// <summary>Rechteck in Pixelkoordinaten des Originalbildes.</summary>
public readonly record struct PlateBox(int X, int Y, int Width, int Height);

/// <summary>Ein erkanntes Kennzeichen in einem Bild.</summary>
/// <param name="Text">Gelesener Text ohne Leer- und Füllzeichen, z. B. "MAB1234".</param>
/// <param name="TextConfidence">Mittlere Zeichen-Konfidenz der OCR, 0..1.</param>
/// <param name="DetectionConfidence">Konfidenz des Detektors für die Kennzeichenposition, 0..1.</param>
/// <param name="Box">Position im Bild.</param>
/// <param name="Region">Vom Modell geschätztes Land oder Region, falls das Modell das liefert.</param>
public sealed record PlateReading(
    string Text,
    float TextConfidence,
    float DetectionConfidence,
    PlateBox Box,
    string? Region = null);

/// <summary>Ergebnis, das an die Ausgaben (<see cref="IResultSink"/>) geht.</summary>
public sealed record RecognitionResult(
    string SourceId,
    DateTimeOffset Timestamp,
    string RecognizerName,
    PlateReading Reading);
