using Kennzeichen.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace Kennzeichen.Recognizers.Onnx;

/// <summary>Zweistufige Erkennung: erst Kennzeichen finden, dann jeden Ausschnitt lesen. Läuft auf der CPU.</summary>
public sealed class OnnxPlateRecognizer : IPlateRecognizer
{
    private readonly YoloV9PlateDetector _detector;
    private readonly PlateOcr _ocr;
    private readonly float _threshold;
    private readonly OnnxRecognizerOptions _options;
    private readonly ILogger _logger;

    public OnnxPlateRecognizer(OnnxRecognizerOptions options, string baseDirectory, ILogger logger)
    {
        _logger = logger;
        _threshold = options.DetectionThreshold;
        _options = options;

        using var sessionOptions = new SessionOptions { IntraOpNumThreads = options.IntraOpNumThreads };
        var detectorPath = Resolve(baseDirectory, options.DetectorModel);
        var ocrPath = Resolve(baseDirectory, options.OcrModel);
        var configPath = Resolve(baseDirectory, options.OcrConfig);

        _detector = new YoloV9PlateDetector(detectorPath, sessionOptions);
        try
        {
            _ocr = new PlateOcr(ocrPath, PlateOcrConfig.Load(configPath), sessionOptions);
        }
        catch
        {
            _detector.Dispose();
            throw;
        }

        _logger.LogInformation(
            "ONNX-Erkenner geladen: Detektor {Detector} ({Size}px), OCR {Ocr} ({Width}x{Height}, {Mode})",
            Path.GetFileName(detectorPath), _detector.InputSize, Path.GetFileName(ocrPath),
            _ocr.Config.ImgWidth, _ocr.Config.ImgHeight, _ocr.Config.ImageColorMode);
    }

    public Task<IReadOnlyList<PlateReading>> RecognizeAsync(Frame frame, CancellationToken cancellationToken)
    {
        using var image = Mat.FromPixelData(frame.Height, frame.Width, MatType.CV_8UC3, frame.Bgr);
        return Task.FromResult(Recognize(image, cancellationToken));
    }

    /// <summary>Erkennung auf einem bereits geladenen BGR-Bild, z. B. aus einer Datei.</summary>
    public IReadOnlyList<PlateReading> Recognize(Mat bgr, CancellationToken cancellationToken = default)
    {
        var readings = new List<PlateReading>();
        foreach (var detection in _detector.Detect(bgr, _threshold))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rect = new Rect(detection.X1, detection.Y1, detection.X2 - detection.X1, detection.Y2 - detection.Y1);
            using var crop = new Mat(bgr, rect);
            var ocr = _ocr.Read(crop);
            if (ocr.Text.Length == 0)
            {
                continue;
            }

            var text = _options.InsertSpaces ? PlateSpacing.Insert(crop, ocr.Text, _options.SpaceGapFactor) : ocr.Text;
            readings.Add(new PlateReading(
                text, ocr.Confidence, detection.Confidence,
                new PlateBox(rect.X, rect.Y, rect.Width, rect.Height), ocr.Region));
        }

        return readings;
    }

    private static string Resolve(string baseDirectory, string path)
    {
        var full = Path.GetFullPath(path, baseDirectory);
        return File.Exists(full)
            ? full
            : throw new FileNotFoundException(
                $"Modelldatei nicht gefunden: {full}. Modelle mit tools/download-models.ps1 herunterladen.", full);
    }

    public void Dispose()
    {
        _detector.Dispose();
        _ocr.Dispose();
    }
}

/// <summary>Plugin-Einstiegspunkt; in der Konfiguration als <c>"Recognizer": { "Name": "Onnx" }</c> auswählbar.</summary>
public sealed class OnnxPlateRecognizerFactory : IPlateRecognizerFactory
{
    public string Name => "Onnx";

    public IPlateRecognizer Create(
        Microsoft.Extensions.Configuration.IConfiguration settings, string baseDirectory, ILoggerFactory loggerFactory)
    {
        var options = new OnnxRecognizerOptions();
        Bind(settings, options);
        return new OnnxPlateRecognizer(options, baseDirectory, loggerFactory.CreateLogger<OnnxPlateRecognizer>());
    }

    // Bewusst ohne Microsoft.Extensions.Configuration.Binder: das Plugin bringt so keine eigene Kopie
    // von Microsoft.Extensions.* mit, die mit den Typen des Dienstes kollidieren könnte.
    private static void Bind(Microsoft.Extensions.Configuration.IConfiguration settings, OnnxRecognizerOptions options)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        options.DetectorModel = settings[nameof(options.DetectorModel)] ?? options.DetectorModel;
        options.OcrModel = settings[nameof(options.OcrModel)] ?? options.OcrModel;
        options.OcrConfig = settings[nameof(options.OcrConfig)] ?? options.OcrConfig;
        if (settings[nameof(options.DetectionThreshold)] is { } threshold)
        {
            options.DetectionThreshold = float.Parse(threshold, inv);
        }

        if (settings[nameof(options.InsertSpaces)] is { } insertSpaces)
        {
            options.InsertSpaces = bool.Parse(insertSpaces);
        }

        if (settings[nameof(options.SpaceGapFactor)] is { } gapFactor)
        {
            options.SpaceGapFactor = double.Parse(gapFactor, inv);
        }

        if (settings[nameof(options.IntraOpNumThreads)] is { } threads)
        {
            options.IntraOpNumThreads = int.Parse(threads, inv);
        }
    }
}
