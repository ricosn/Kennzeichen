using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Kennzeichen.Recognizers.Onnx;

/// <summary>
/// Modellkonfiguration von fast-plate-ocr (die YAML-Datei, die neben jedem OCR-Modell veröffentlicht wird).
/// Felder und Standardwerte entsprechen <c>fast_plate_ocr/inference/config.py</c>.
/// </summary>
public sealed class PlateOcrConfig
{
    public int MaxPlateSlots { get; set; }

    public string Alphabet { get; set; } = "";

    public string PadChar { get; set; } = "_";

    public int ImgHeight { get; set; }

    public int ImgWidth { get; set; }

    public bool KeepAspectRatio { get; set; }

    public string Interpolation { get; set; } = "linear";

    /// <summary>"grayscale" (1 Kanal) oder "rgb" (3 Kanäle).</summary>
    public string ImageColorMode { get; set; } = "grayscale";

    /// <summary>Füllfarbe bei <see cref="KeepAspectRatio"/>; ein Wert (Graustufen) oder drei Werte (RGB).</summary>
    public List<int> PaddingColor { get; set; } = [114, 114, 114];

    public List<string>? PlateRegions { get; set; }

    public bool IsRgb => string.Equals(ImageColorMode, "rgb", StringComparison.OrdinalIgnoreCase);

    public int Channels => IsRgb ? 3 : 1;

    public static PlateOcrConfig Load(string path) => Parse(File.ReadAllText(path));

    public static PlateOcrConfig Parse(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new IntOrListConverter())
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<PlateOcrConfig>(yaml)
            ?? throw new InvalidDataException("Leere OCR-Konfiguration.");
        config.Validate();
        return config;
    }

    private void Validate()
    {
        if (MaxPlateSlots <= 0 || ImgHeight <= 0 || ImgWidth <= 0)
        {
            throw new InvalidDataException("max_plate_slots, img_height und img_width müssen größer als 0 sein.");
        }

        if (PadChar.Length != 1 || !Alphabet.Contains(PadChar[0]))
        {
            throw new InvalidDataException($"pad_char '{PadChar}' muss genau ein Zeichen aus dem Alphabet sein.");
        }

        if (ImageColorMode is not ("rgb" or "grayscale"))
        {
            throw new InvalidDataException($"Unbekannter image_color_mode '{ImageColorMode}'.");
        }
    }
}
