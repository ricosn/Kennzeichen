namespace Kennzeichen.Abstractions;

/// <summary>
/// Ein einzelnes Kamerabild als unkomprimierter BGR24-Puffer (3 Byte pro Pixel, zeilenweise, ohne Padding).
/// Bewusst ohne Abhängigkeit zu einer Bildbibliothek, damit Plugins frei wählen können.
/// </summary>
public sealed class Frame
{
    public Frame(byte[] bgr, int width, int height, DateTimeOffset timestamp, string sourceId)
    {
        ArgumentNullException.ThrowIfNull(bgr);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (bgr.Length != width * height * 3)
        {
            throw new ArgumentException($"Erwartet {width * height * 3} Byte für {width}x{height} BGR24, erhalten {bgr.Length}.", nameof(bgr));
        }

        Bgr = bgr;
        Width = width;
        Height = height;
        Timestamp = timestamp;
        SourceId = sourceId;
    }

    public byte[] Bgr { get; }

    public int Width { get; }

    public int Height { get; }

    public DateTimeOffset Timestamp { get; }

    /// <summary>Kennung der Quelle, z. B. der Kameraname aus der Konfiguration.</summary>
    public string SourceId { get; }
}
