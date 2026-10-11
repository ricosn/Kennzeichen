namespace Kennzeichen.Recognizers.Onnx;

/// <summary>Einstellungen aus <c>Recognizer:Settings</c>. Relative Pfade gelten ab dem Dienstverzeichnis.</summary>
public sealed class OnnxRecognizerOptions
{
    public string DetectorModel { get; set; } = "models/yolo-v9-t-384-license-plates-end2end.onnx";

    public string OcrModel { get; set; } = "models/cct_xs_v2_global.onnx";

    public string OcrConfig { get; set; } = "models/cct_xs_v2_global_plate_config.yaml";

    /// <summary>Mindestkonfidenz des Detektors. 0,25 ist der Standardwert von open-image-models für dieses Modell.</summary>
    public float DetectionThreshold { get; set; } = 0.25f;

    /// <summary>Anzahl CPU-Threads pro Modell; 0 überlässt die Wahl ONNX Runtime.</summary>
    public int IntraOpNumThreads { get; set; }
}
