namespace Kennzeichen.Recognizers.Onnx;

/// <summary>
/// Skalierung mit Seitenverhältnis plus Rand, wie <c>letterbox()</c> in open-image-models.
/// Python rundet mit "round half to even"; <see cref="Math.Round(double)"/> tut dasselbe.
/// </summary>
public readonly record struct Letterbox(
    int TargetSize, double Ratio, int ScaledWidth, int ScaledHeight, double PadX, double PadY,
    int Top, int Bottom, int Left, int Right)
{
    public static Letterbox Compute(int width, int height, int targetSize)
    {
        var ratio = Math.Min((double)targetSize / height, (double)targetSize / width);
        var scaledWidth = (int)Math.Round(width * ratio);
        var scaledHeight = (int)Math.Round(height * ratio);
        var padX = (targetSize - scaledWidth) / 2.0;
        var padY = (targetSize - scaledHeight) / 2.0;
        return new Letterbox(
            targetSize, ratio, scaledWidth, scaledHeight, padX, padY,
            Top: (int)Math.Round(padY - 0.1),
            Bottom: (int)Math.Round(padY + 0.1),
            Left: (int)Math.Round(padX - 0.1),
            Right: (int)Math.Round(padX + 0.1));
    }

    /// <summary>Rechnet einen Punkt aus Modellkoordinaten zurück ins Originalbild.</summary>
    public (double X, double Y) ToOriginal(double x, double y) => ((x - PadX) / Ratio, (y - PadY) / Ratio);
}
