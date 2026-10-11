using OpenCvSharp;

namespace Kennzeichen.Recognizers.Onnx;

/// <summary>
/// Setzt Leerzeichen in den gelesenen Text, denn die OCR-Modelle kennen kein Leerzeichen (Alphabet 0-9, A-Z).
/// Vorgehen: Zeichen im Ausschnitt als zusammenhängende Flächen finden; passt ihre Anzahl zur Textlänge,
/// kommt ein Leerzeichen dorthin, wo der Abstand zwischen zwei Zeichen deutlich größer ist als üblich.
/// Passt die Anzahl nicht (kleine oder unscharfe Kennzeichen, bei denen Zeichen zerfallen oder zusammenwachsen),
/// werden die Lücken in der Spaltenprojektion gesucht und über ihre Lage im Schriftzug einer Textstelle zugeordnet.
/// Beim Wechsel von Buchstabe zu Ziffer wird immer getrennt, auch wenn die Abstände nicht auswertbar sind.
/// </summary>
public static class PlateSpacing
{
    private const int WorkHeight = 96;
    private const double SeparatorGapFactor = 0.35;
    private const double FragmentBrightnessFactor = 0.4;

    public static string Insert(Mat plateBgr, string text, double gapFactor)
    {
        if (text.Length < 2)
        {
            return text;
        }

        var boxes = FindCharacterBoxes(plateBgr);
        return boxes.Count == text.Length
            ? InsertByGaps(text, boxes, gapFactor)
            : InsertByInkRuns(text, FindInkRuns(plateBgr, out var typicalHeight), typicalHeight);
    }

    /// <summary>
    /// Für den Fall, dass die Zeichen nicht einzeln gefunden werden. <paramref name="runs"/> sind die waagrechten Abschnitte
    /// mit Schrift (von links nach rechts). Große Lücken zwischen ihnen werden einer Textstelle zugeordnet, indem man annimmt,
    /// dass alle Zeichen ungefähr gleich breit sind: Liegen links der Lücke 30 % der Schriftbreite, gehört sie hinter
    /// 30 % der Zeichen. Der Wechsel von Buchstabe zu Ziffer bekommt die nächstliegende Lücke; weitere Lücken werden nur
    /// innerhalb einer Buchstabengruppe gesetzt (bei deutschen Kennzeichen zwischen Unterscheidungszeichen und Buchstaben),
    /// nie zwischen Ziffern. Als groß gilt eine Lücke ab 0,35 Zeichenhöhen; die Zeichenhöhe lässt sich auch bei
    /// zerfallenen Zeichen zuverlässig messen, die Abstände untereinander nicht.
    /// </summary>
    public static string InsertByInkRuns(string text, IReadOnlyList<(int Start, int End)> runs, double typicalHeight)
    {
        var spaces = new SortedSet<int>();
        for (var i = 1; i < text.Length; i++)
        {
            if (IsLetterToDigit(text, i))
            {
                spaces.Add(i);
            }
        }

        if (runs.Count >= 2)
        {
            var gaps = new int[runs.Count - 1];
            for (var i = 0; i < gaps.Length; i++)
            {
                gaps[i] = runs[i + 1].Start - runs[i].End;
            }

            var widths = runs.Select(r => r.End - r.Start).ToArray();
            var totalInk = (double)widths.Sum();
            var threshold = SeparatorGapFactor * typicalHeight;

            // Lage jeder großen Lücke, gemessen in Zeichen ab dem linken Rand der Schrift.
            var separators = new List<double>();
            var inkLeft = 0;
            for (var i = 0; i < gaps.Length; i++)
            {
                inkLeft += widths[i];
                if (gaps[i] > threshold)
                {
                    separators.Add(inkLeft / totalInk * text.Length);
                }
            }

            foreach (var boundary in spaces.ToArray())
            {
                if (separators.Count == 0)
                {
                    break;
                }

                var nearest = separators.MinBy(p => Math.Abs(p - boundary));
                if (Math.Abs(nearest - boundary) < 1.0)
                {
                    separators.Remove(nearest);
                }
            }

            foreach (var position in separators)
            {
                var index = (int)Math.Round(position, MidpointRounding.AwayFromZero);
                if (index > 0 && index < text.Length && Math.Abs(position - index) < 0.4
                    && char.IsLetter(text[index - 1]) && char.IsLetter(text[index]))
                {
                    spaces.Add(index);
                }
            }
        }

        var result = new System.Text.StringBuilder(text.Length + spaces.Count);
        for (var i = 0; i < text.Length; i++)
        {
            if (spaces.Contains(i))
            {
                result.Append(' ');
            }

            result.Append(text[i]);
        }

        return result.ToString();
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
    public static List<Rect> FindCharacterBoxes(Mat plateBgr) => Segment(plateBgr).Characters;

    /// <summary>
    /// Waagrechte Abschnitte mit Schrift, von links nach rechts. Zusätzlich zu den ganzen Zeichen zählen Bruchstücke
    /// (mindestens 40 % der Zeichenhöhe, ähnlich dunkel wie die Schrift, innerhalb der Schriftzeile), wenn sie an einem
    /// Zeichen anliegen oder übereinander ein ganzes Zeichen ergeben. So entstehen auch bei kleinen Kennzeichen, deren
    /// Zeichen in Teile zerfallen, die richtigen Lücken.
    /// </summary>
    public static List<(int Start, int End)> FindInkRuns(Mat plateBgr, out double typicalHeight)
    {
        var (characters, fragments, height) = Segment(plateBgr);
        typicalHeight = height;
        if (characters.Count == 0)
        {
            return [];
        }

        var top = Median(characters.Select(c => c.Top).ToArray());
        var bottom = Median(characters.Select(c => c.Bottom).ToArray());
        var tolerance = 0.15 * height;
        var inLine = fragments.Where(f => f.Top >= top - tolerance && f.Bottom <= bottom + tolerance).ToList();

        // Bruchstücke direkt neben oder über einem Zeichen gehören zu diesem Zeichen.
        var adjacent = 0.1 * height;
        var parts = new List<Rect>(characters);
        var loose = new List<Rect>();
        foreach (var fragment in inLine)
        {
            var touchesCharacter = characters.Any(c => fragment.Left <= c.Right + adjacent && fragment.Right >= c.Left - adjacent);
            (touchesCharacter ? parts : loose).Add(fragment);
        }

        // Übrige Bruchstücke zählen nur, wenn sie übereinander zusammen fast die volle Zeichenhöhe ergeben
        // (ein zerfallenes Zeichen). Eine einzelne Plakette oder ein Fleck erfüllt das nicht.
        foreach (var group in GroupByOverlap(loose))
        {
            if (group.Max(f => f.Bottom) - group.Min(f => f.Top) >= 0.8 * height)
            {
                parts.AddRange(group);
            }
        }

        var runs = new List<(int Start, int End)>();
        foreach (var part in parts.OrderBy(r => r.Left))
        {
            if (runs.Count > 0 && part.Left <= runs[^1].End)
            {
                runs[^1] = (runs[^1].Start, Math.Max(runs[^1].End, part.Right));
            }
            else
            {
                runs.Add((part.Left, part.Right));
            }
        }

        return runs;
    }

    private static List<List<Rect>> GroupByOverlap(IEnumerable<Rect> boxes)
    {
        var groups = new List<List<Rect>>();
        foreach (var box in boxes.OrderBy(b => b.Left))
        {
            if (groups.Count > 0 && box.Left < groups[^1].Max(b => b.Right))
            {
                groups[^1].Add(box);
            }
            else
            {
                groups.Add([box]);
            }
        }

        return groups;
    }

    private static (List<Rect> Characters, List<Rect> Fragments, double TypicalHeight) Segment(Mat plateBgr)
    {
        using var gray = new Mat();
        Cv2.CvtColor(plateBgr, gray, ColorConversionCodes.BGR2GRAY);
        var scale = (double)WorkHeight / gray.Rows;
        using var resized = new Mat();
        Cv2.Resize(gray, resized, new Size(Math.Max(1, (int)Math.Round(gray.Cols * scale)), WorkHeight), interpolation: InterpolationFlags.Cubic);
        using var binary = new Mat();
        var otsu = Cv2.Threshold(resized, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var count = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids);

        var height = binary.Rows;
        var width = binary.Cols;
        var components = new List<(Rect Box, int Label)>();
        for (var i = 1; i < count; i++)
        {
            var box = new Rect(
                stats.At<int>(i, (int)ConnectedComponentsTypes.Left),
                stats.At<int>(i, (int)ConnectedComponentsTypes.Top),
                stats.At<int>(i, (int)ConnectedComponentsTypes.Width),
                stats.At<int>(i, (int)ConnectedComponentsTypes.Height));
            if (box.Height < 0.95 * height && box.Width < 0.3 * width)
            {
                components.Add((box, i));
            }
        }

        var candidates = components.Where(c => c.Box.Height > 0.3 * height).ToList();

        // Zeichen eines Kennzeichens sind gleich hoch; Plaketten und Schmutz sind kleiner.
        var typicalHeight = Median(candidates.Select(c => c.Box.Height).ToArray());
        var characters = candidates.Where(c => c.Box.Height >= 0.8 * typicalHeight).ToList();

        // Bruchstücke müssen so dunkel sein wie die Schrift; hellere Flächen (z. B. eine graue Plakette) zählen nicht.
        double Brightness(int label)
        {
            using var mask = new Mat();
            Cv2.InRange(labels, label, label, mask);
            return Cv2.Mean(resized, mask).Val0;
        }

        var fragments = new List<Rect>();
        if (characters.Count > 0)
        {
            var textBrightness = Median(characters.Select(c => (int)Math.Round(Brightness(c.Label))).ToArray());
            var limit = textBrightness + (FragmentBrightnessFactor * (otsu - textBrightness));
            fragments = components
                .Where(c => c.Box.Height >= 0.4 * typicalHeight && !characters.Contains(c) && Brightness(c.Label) <= limit)
                .Select(c => c.Box)
                .ToList();
        }

        var boxes = characters.Select(c => c.Box).ToList();
        boxes.Sort((a, b) => a.X.CompareTo(b.X));
        return (boxes, fragments, typicalHeight);
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
