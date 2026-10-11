using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Kennzeichen.Recognizers.Onnx;

internal static class MatBytes
{
    /// <summary>Kopiert die Pixel einer 8-Bit-Mat zeilenweise ohne Padding in ein Array.</summary>
    public static byte[] ToArray(Mat mat)
    {
        var rows = mat.Rows;
        var rowBytes = mat.Cols * mat.ElemSize();
        var result = new byte[rowBytes * rows];
        if (mat.IsContinuous())
        {
            Marshal.Copy(mat.Data, result, 0, result.Length);
            return result;
        }

        for (var row = 0; row < rows; row++)
        {
            Marshal.Copy(mat.Ptr(row), result, row * rowBytes, rowBytes);
        }

        return result;
    }
}
