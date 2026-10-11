using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Kennzeichen.Recognizers.Onnx;

public sealed record OcrResult(string Text, float Confidence, string? Region, IReadOnlyList<float> SlotConfidences);

/// <summary>
/// Liest den Text eines ausgeschnittenen Kennzeichens mit einem fast-plate-ocr-Modell.
/// Vor- und Nachverarbeitung entsprechen <c>fast_plate_ocr/core/process.py</c> und <c>inference/plate_recognizer.py</c>:
/// Farbraum laut Konfiguration, Größe auf img_width x img_height, uint8 NHWC; die Normalisierung steckt im Modell.
/// </summary>
public sealed class PlateOcr : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _plateOutput;
    private readonly string? _regionOutput;

    public PlateOcr(string modelPath, PlateOcrConfig config, SessionOptions options)
    {
        Config = config;
        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.Single();
        var outputs = _session.OutputMetadata.Keys.ToList();
        _plateOutput = outputs.Contains("plate") ? "plate" : outputs[0];
        _regionOutput = outputs.Contains("region") && config.PlateRegions is { Count: > 0 } ? "region" : null;
    }

    public PlateOcrConfig Config { get; }

    /// <param name="plateBgr">Ausschnitt des Kennzeichens im BGR-Format.</param>
    public OcrResult Read(Mat plateBgr)
    {
        using var prepared = Prepare(plateBgr);
        var input = new DenseTensor<byte>(MatBytes.ToArray(prepared), [1, Config.ImgHeight, Config.ImgWidth, Config.Channels]);

        var fetch = _regionOutput is null ? new[] { _plateOutput } : [_plateOutput, _regionOutput];
        using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_inputName, input)], fetch);
        var plate = results.Single(r => r.Name == _plateOutput).AsEnumerable<float>().ToArray();
        float[]? region = _regionOutput is null ? null : results.Single(r => r.Name == _regionOutput).AsEnumerable<float>().ToArray();

        return Decode(plate, region, Config);
    }

    /// <summary>Wandelt die Modellausgabe in Text um: pro Zeichenplatz das wahrscheinlichste Zeichen, Füllzeichen am Ende entfernen.</summary>
    public static OcrResult Decode(ReadOnlySpan<float> plateScores, float[]? regionScores, PlateOcrConfig config)
    {
        var alphabetSize = config.Alphabet.Length;
        if (plateScores.Length != config.MaxPlateSlots * alphabetSize)
        {
            throw new InvalidDataException(
                $"OCR-Ausgabe hat {plateScores.Length} Werte, erwartet {config.MaxPlateSlots} x {alphabetSize}.");
        }

        var chars = new char[config.MaxPlateSlots];
        var confidences = new float[config.MaxPlateSlots];
        for (var slot = 0; slot < config.MaxPlateSlots; slot++)
        {
            var scores = plateScores.Slice(slot * alphabetSize, alphabetSize);
            var best = ArgMax(scores);
            chars[slot] = config.Alphabet[best];
            confidences[slot] = scores[best];
        }

        var text = new string(chars).TrimEnd(config.PadChar[0]);
        string? region = null;
        if (regionScores is { Length: > 0 } && config.PlateRegions is { Count: > 0 } labels)
        {
            var best = ArgMax(regionScores);
            region = best < labels.Count ? labels[best] : null;
        }

        return new OcrResult(text, confidences.Average(), region, confidences);
    }

    private Mat Prepare(Mat plateBgr)
    {
        using var converted = new Mat();
        Cv2.CvtColor(plateBgr, converted, Config.IsRgb ? ColorConversionCodes.BGR2RGB : ColorConversionCodes.BGR2GRAY);

        var interpolation = Config.Interpolation switch
        {
            "nearest" => InterpolationFlags.Nearest,
            "linear" => InterpolationFlags.Linear,
            "cubic" => InterpolationFlags.Cubic,
            "area" => InterpolationFlags.Area,
            "lanczos4" => InterpolationFlags.Lanczos4,
            _ => throw new InvalidDataException($"Unbekannte Interpolation '{Config.Interpolation}'."),
        };

        var result = new Mat();
        if (!Config.KeepAspectRatio)
        {
            Cv2.Resize(converted, result, new Size(Config.ImgWidth, Config.ImgHeight), interpolation: interpolation);
            return result;
        }

        var ratio = Math.Min((double)Config.ImgHeight / converted.Height, (double)Config.ImgWidth / converted.Width);
        var width = (int)Math.Round(converted.Width * ratio);
        var height = (int)Math.Round(converted.Height * ratio);
        using var resized = new Mat();
        Cv2.Resize(converted, resized, new Size(width, height), interpolation: interpolation);
        var padX = (Config.ImgWidth - width) / 2.0;
        var padY = (Config.ImgHeight - height) / 2.0;
        var color = Config.PaddingColor;
        var border = Config.IsRgb && color.Count == 3 ? new Scalar(color[0], color[1], color[2]) : new Scalar(color[0]);
        Cv2.CopyMakeBorder(
            resized, result,
            (int)Math.Round(padY - 0.1), (int)Math.Round(padY + 0.1),
            (int)Math.Round(padX - 0.1), (int)Math.Round(padX + 0.1),
            BorderTypes.Constant, border);
        return result;
    }

    private static int ArgMax(ReadOnlySpan<float> values)
    {
        var best = 0;
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > values[best])
            {
                best = i;
            }
        }

        return best;
    }

    public void Dispose() => _session.Dispose();
}
