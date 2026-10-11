using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Kennzeichen.Recognizers.Onnx;

/// <summary>Ein gefundenes Kennzeichen in Pixelkoordinaten des Originalbildes (bereits auf das Bild begrenzt).</summary>
public readonly record struct PlateDetection(int X1, int Y1, int X2, int Y2, float Confidence);

/// <summary>
/// Kennzeichendetektor für die YOLOv9-"end2end"-Modelle aus open-image-models.
/// Vor- und Nachverarbeitung entsprechen <c>open_image_models/detection/core/yolo_v9/preprocess.py</c>
/// und <c>postprocess.py</c>: Letterbox auf ein Quadrat (Füllwert 114), BGR nach RGB, 0..1, NCHW float32.
/// Das Modell enthält NMS bereits und liefert Zeilen der Form [batch, x1, y1, x2, y2, klasse, score].
/// </summary>
public sealed class YoloV9PlateDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;

    public YoloV9PlateDetector(string modelPath, SessionOptions options)
    {
        _session = new InferenceSession(modelPath, options);
        var input = _session.InputMetadata.Single();
        _inputName = input.Key;
        var dims = input.Value.Dimensions;
        if (dims.Length != 4 || dims[1] != 3 || dims[2] <= 0 || dims[2] != dims[3])
        {
            throw new InvalidDataException($"Unerwartete Detektor-Eingabe {string.Join('x', dims)}; erwartet 1x3xNxN.");
        }

        InputSize = dims[2];
    }

    public int InputSize { get; }

    public IReadOnlyList<PlateDetection> Detect(Mat bgr, float confidenceThreshold)
    {
        var letterbox = Letterbox.Compute(bgr.Width, bgr.Height, InputSize);
        var input = ToInputTensor(bgr, letterbox);

        using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_inputName, input)]);
        var output = results[0].AsTensor<float>();
        if (output.Dimensions.Length != 2 || output.Dimensions[1] < 7)
        {
            throw new InvalidDataException($"Unerwartete Detektor-Ausgabe {string.Join('x', output.Dimensions.ToArray())}; erwartet Nx7.");
        }

        var detections = new List<PlateDetection>();
        for (var i = 0; i < output.Dimensions[0]; i++)
        {
            var score = output[i, 6];
            if (score < confidenceThreshold)
            {
                continue;
            }

            var (x1, y1) = letterbox.ToOriginal(output[i, 1], output[i, 2]);
            var (x2, y2) = letterbox.ToOriginal(output[i, 3], output[i, 4]);

            // Wie fast-alpr: auf ganze Pixel kürzen und auf das Bild begrenzen.
            var box = new PlateDetection(
                Math.Clamp((int)x1, 0, bgr.Width),
                Math.Clamp((int)y1, 0, bgr.Height),
                Math.Clamp((int)x2, 0, bgr.Width),
                Math.Clamp((int)y2, 0, bgr.Height),
                score);
            if (box.X2 > box.X1 && box.Y2 > box.Y1)
            {
                detections.Add(box);
            }
        }

        return detections;
    }

    private static DenseTensor<float> ToInputTensor(Mat bgr, Letterbox letterbox)
    {
        using var resized = new Mat();
        if (letterbox.ScaledWidth != bgr.Width || letterbox.ScaledHeight != bgr.Height)
        {
            Cv2.Resize(bgr, resized, new Size(letterbox.ScaledWidth, letterbox.ScaledHeight), interpolation: InterpolationFlags.Linear);
        }
        else
        {
            bgr.CopyTo(resized);
        }

        using var padded = new Mat();
        Cv2.CopyMakeBorder(
            resized, padded, letterbox.Top, letterbox.Bottom, letterbox.Left, letterbox.Right,
            BorderTypes.Constant, new Scalar(114, 114, 114));

        var size = letterbox.TargetSize;
        var pixels = MatBytes.ToArray(padded);
        var tensor = new DenseTensor<float>([1, 3, size, size]);
        var span = tensor.Buffer.Span;
        var plane = size * size;
        for (var p = 0; p < plane; p++)
        {
            // BGR im Bild, RGB im Modell.
            span[p] = pixels[(p * 3) + 2] / 255f;
            span[plane + p] = pixels[(p * 3) + 1] / 255f;
            span[(2 * plane) + p] = pixels[p * 3] / 255f;
        }

        return tensor;
    }

    public void Dispose() => _session.Dispose();
}
