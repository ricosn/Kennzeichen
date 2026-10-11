namespace Kennzeichen.Abstractions;

/// <summary>Liefert fortlaufend Bilder, z. B. von einer Netzwerkkamera.</summary>
public interface IFrameSource
{
    /// <summary>
    /// Liefert Bilder, bis <paramref name="cancellationToken"/> ausgelöst wird.
    /// Verbindungsabbrüche behandelt die Quelle selbst (erneut verbinden), statt die Aufzählung zu beenden.
    /// </summary>
    IAsyncEnumerable<Frame> ReadFramesAsync(CancellationToken cancellationToken);
}

/// <summary>Nimmt Erkennungsergebnisse entgegen, z. B. Log, Datei, Datenbank oder Schnittstelle.</summary>
public interface IResultSink
{
    Task PublishAsync(RecognitionResult result, CancellationToken cancellationToken);
}
