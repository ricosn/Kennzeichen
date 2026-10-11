namespace Kennzeichen.Service;

/// <summary>Abschnitt <c>Recognizer</c>: welches Plugin erkennt, und dessen eigene Einstellungen unter <c>Settings</c>.</summary>
public sealed class RecognizerOptions
{
    /// <summary>Name des Erkenners, wie ihn <c>IPlateRecognizerFactory.Name</c> meldet.</summary>
    public string Name { get; set; } = "Onnx";

    /// <summary>Ordner mit einem Unterordner je Plugin; relativ zum Programmverzeichnis.</summary>
    public string PluginDirectory { get; set; } = "plugins";
}

/// <summary>Abschnitt <c>Results</c>: Filter und Ausgaben.</summary>
public sealed class ResultOptions
{
    /// <summary>Ergebnisse mit geringerer mittlerer Zeichen-Konfidenz werden verworfen.</summary>
    public float MinTextConfidence { get; set; } = 0.5f;

    /// <summary>Kürzester Kennzeichentext, der gemeldet wird.</summary>
    public int MinTextLength { get; set; } = 3;

    /// <summary>Dasselbe Kennzeichen derselben Kamera wird innerhalb dieser Zeit nur einmal gemeldet.</summary>
    public int SuppressRepeatSeconds { get; set; } = 30;

    /// <summary>Ordner für die JSON-Dateien (eine Datei pro Tag); leer schaltet die Dateiausgabe ab.</summary>
    public string? JsonDirectory { get; set; } = "results";
}
