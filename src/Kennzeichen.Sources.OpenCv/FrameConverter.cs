using System.Runtime.InteropServices;
using Kennzeichen.Abstractions;
using OpenCvSharp;

namespace Kennzeichen.Sources.OpenCv;

internal static class FrameConverter
{
    /// <summary>Kopiert ein BGR-Bild (CV_8UC3) in einen <see cref="Frame"/>.</summary>
    public static Frame ToFrame(Mat bgr, string sourceId)
    {
        if (bgr.Type() != MatType.CV_8UC3)
        {
            throw new ArgumentException($"Erwartet CV_8UC3, erhalten {bgr.Type()}.", nameof(bgr));
        }

        var width = bgr.Cols;
        var height = bgr.Rows;
        var rowBytes = width * 3;
        var data = new byte[rowBytes * height];
        if (bgr.IsContinuous())
        {
            Marshal.Copy(bgr.Data, data, 0, data.Length);
        }
        else
        {
            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(bgr.Ptr(row), data, row * rowBytes, rowBytes);
            }
        }

        return new Frame(data, width, height, DateTimeOffset.Now, sourceId);
    }
}
