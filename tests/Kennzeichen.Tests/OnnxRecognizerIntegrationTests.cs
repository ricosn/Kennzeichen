using Kennzeichen.Recognizers.Onnx;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace Kennzeichen.Tests;

/// <summary>
/// Läuft mit den echten Modellen aus <c>models/</c> im Repository-Wurzelverzeichnis
/// (herunterladen mit <c>tools/download-models.ps1</c>) und vergleicht mit der Python-Referenz.
/// </summary>
public class OnnxRecognizerIntegrationTests
{
    private static readonly string ModelDirectory = FindModelDirectory();

    private static string FindModelDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Kennzeichen.slnx")))
            {
                return Path.Combine(dir.FullName, "models");
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "models");
    }

    private static OnnxPlateRecognizer CreateRecognizer(string ocrModel, string ocrConfig)
    {
        var options = new OnnxRecognizerOptions
        {
            DetectorModel = "yolo-v9-t-384-license-plates-end2end.onnx",
            OcrModel = ocrModel,
            OcrConfig = ocrConfig,
        };
        if (!File.Exists(Path.Combine(ModelDirectory, options.DetectorModel)) || !File.Exists(Path.Combine(ModelDirectory, ocrModel)))
        {
            Assert.Skip($"Modelle fehlen in {ModelDirectory}; mit tools/download-models.ps1 herunterladen.");
        }

        return new OnnxPlateRecognizer(options, ModelDirectory, NullLogger.Instance);
    }

    // Referenz (Python, fast-alpr 0.4.0): Box (307,187)-(387,210), Detektor 0.8428, Text 5AU5341,
    // Region "Czech Republic" beim globalen Modell.
    [Theory]
    [InlineData("cct_xs_v2_global.onnx", "cct_xs_v2_global_plate_config.yaml", "Czech Republic")]
    [InlineData("european_mobile_vit_v2_ocr.onnx", "european_mobile_vit_v2_ocr_config.yaml", null)]
    public void Reads_test_image_like_python_reference(string ocrModel, string ocrConfig, string? region)
    {
        using var recognizer = CreateRecognizer(ocrModel, ocrConfig);
        using var image = Cv2.ImRead(Path.Combine(AppContext.BaseDirectory, "TestData", "fast-alpr-test_image.png"), ImreadModes.Color);

        var reading = Assert.Single(recognizer.Recognize(image, TestContext.Current.CancellationToken));

        Assert.Equal("5AU5341", reading.Text);
        Assert.Equal(region, reading.Region);
        Assert.InRange(reading.DetectionConfidence, 0.83f, 0.86f);
        Assert.InRange(reading.Box.X, 306, 308);
        Assert.InRange(reading.Box.Y, 186, 188);
        Assert.InRange(reading.Box.X + reading.Box.Width, 386, 388);
        Assert.InRange(reading.Box.Y + reading.Box.Height, 209, 211);
    }
}
