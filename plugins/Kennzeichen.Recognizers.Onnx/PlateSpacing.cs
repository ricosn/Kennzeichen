using OpenCvSharp;

namespace Kennzeichen.Recognizers.Onnx;

/// <summary>
/// Setzt Leerzeichen in den gelesenen Text, denn die OCR-Modelle kennen kein Leerzeichen (Alphabet 0-9, A-Z).
/// Vorgehen: Zeichen im Ausschnitt als zusammenhängende Flächen finden; passt ihre Anzahl zur Textlänge,
/// kommt ein Leerzeichen dorthin, wo der Abstand zwischen zwei Zeichen deutlich größer ist als üblich.
/// Beim Wechsel von Buchstabe zu Ziffer wird immer getrennt, auch wenn die Abstände nicht auswertbar sind.
/// </summary>
public static class PlateSpacing
{
    private const int WorkHeight = 96;

    public static string Insert(Mat plateBgr, string text, double gapFactor)
    {
        if (text.Length < 2)
        {
            return text;
        }

        var boxes = FindCharacterBoxes(plateBgr);
        return boxes.Count == text.Length ? InsertByGaps(text, boxes, gapFactor) : InsertAtLetterDigitBoundary(text);
    }

    /// <summary>Leerzeichen dort, wo der Abstand größer ist als <paramref name="gapFactor"/> mal der mittlere Abstand
    /// und größer als ein Drittel der mittleren Zeichenbreite, sowie zwischen Buchstabe und Ziffer.</summary>
    public static string InsertByGaps(string text, IReadOnlyList<Rect> boxes, double gapFactor)
    {
        var gaps = new int[boxes.Count - 1];
        for (var i = 0; i < gaps.Length; i++)
        {
            gaps[i] = boxes[i + 1].Left - boxes[i].Right;
        }

        var threshold = Math.Max(gapFactor * Median(gaps), Median(boxes.Select(b => b.Width).ToArray()) / 3.0);
        var result = new System.Text.StringBuilder(text.Length + 3);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && (gaps[i - 1] > threshold || IsLetterToDigit(text, i)))
            {
                result.Append(' ');
            }

            result.Append(text[i]);
        }

        return result.ToString();
    }

    public static string InsertAtLetterDigitBoundary(string text)
    {
        var result = new System.Text.StringBuilder(text.Length + 2);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && IsLetterToDigit(text, i))
            {
                result.Append(' ');
            }

            result.Append(text[i]);
        }

        return result.ToString();
    }

    private static bool IsLetterToDigit(string text, int i) => char.IsLetter(text[i - 1]) && char.IsDigit(text[i]);

    /// <summary>
    /// Dunkle Zeichen auf hellem Grund, von links nach rechts. Aussortiert werden Flächen, die zu klein oder zu breit sind,
    /// die über die volle Höhe gehen (Rand, EU-Feld), und alles deutlich Niedrigere als die typische Zeichenhöhe (Plaketten).
    /// </summary>
    public static List<Rect> FindCharacterBoxes(Mat plateBgr)
    {
        using var gray = new Mat();
        Cv2.CvtColor(plateBgr, gray, ColorConversionCodes.BGR2GRAY);
        var scale = (double)WorkHeight / gray.Rows;
        using var resized = new Mat();
        Cv2.Resize(gray, resized, new Size(Math.Max(1, (int)Math.Round(gray.Cols * scale)), WorkHeight), interpolation: InterpolationFlags.Cubic);
        using var binary = new Mat();
        Cv2.Threshold(resized, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var count = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids);

        var height = binary.Rows;
        var width = binary.Cols;
        var candidates = new List<Rect>();
        for (var i = 1; i < count; i++)
        {
            var box = new Rect(
                stats.At<int>(i, (int)ConnectedComponentsTypes.Left),
                stats.At<int>(i, (int)ConnectedComponentsTypes.Top),
                stats.At<int>(i, (int)ConnectedComponentsTypes.Width),
                stats.At<int>(i, (int)ConnectedComponentsTypes.Height));
            if (box.Height > 0.3 * height && box.Height < 0.95 * height && box.Width < 0.3 * width)
            {
                candidates.Add(box);
            }
        }

        // Zeichen eines Kennzeichens sind gleich hoch; Plaketten und Schmutz sind kleiner.
        var typicalHeight = Median(candidates.Select(c => c.Height).ToArray());
        var boxes = candidates.Where(c => c.Height >= 0.8 * typicalHeight).ToList();

        boxes.Sort((a, b) => a.X.CompareTo(b.X));
        return boxes;
    }

    private static double Median(int[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
