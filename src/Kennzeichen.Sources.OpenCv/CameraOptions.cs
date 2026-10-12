namespace Kennzeichen.Sources.OpenCv;

public enum CameraSourceType
{
    /// <summary>Videostream, bei Axis z. B. <c>rtsp://&lt;ip&gt;/axis-media/media.amp</c>.</summary>
    Rtsp,

    /// <summary>Einzelbild per HTTP-GET (JPEG), in festem Abstand abgefragt.</summary>
    Snapshot,

    /// <summary>Bilddateien aus einem Ordner (<see cref="CameraOptions.Url"/> ist dann der Ordnerpfad); zum Testen ohne Kamera.</summary>
    Folder,
}

/// <summary>Abschnitt <c>Camera</c> der Dienstkonfiguration.</summary>
public sealed class CameraOptions
{
    public CameraSourceType Type { get; set; } = CameraSourceType.Rtsp;

    /// <summary>Name der Kamera; erscheint in Log und Ergebnissen.</summary>
    public string Name { get; set; } = "kamera1";

    /// <summary>Adresse ohne Zugangsdaten, z. B. <c>rtsp://192.168.0.90/axis-media/media.amp</c>.</summary>
    public string Url { get; set; } = "";

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// Mindestabstand zwischen zwei weitergegebenen Bildern. Bei RTSP wird immer das neueste Bild genommen,
    /// ältere werden verworfen; bei Snapshot ist es das Abfrageintervall.
    /// </summary>
    public int IntervalMilliseconds { get; set; } = 250;

    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Nur RTSP: Video über die Grafikeinheit dekodieren lassen (unter Windows D3D11), falls vorhanden.
    /// Ist keine nutzbar, dekodiert OpenCV laut Dokumentation in Software weiter. Welche Art aktiv ist, steht beim Verbinden im Log.
    /// </summary>
    public bool HardwareDecoding { get; set; } = true;
}
