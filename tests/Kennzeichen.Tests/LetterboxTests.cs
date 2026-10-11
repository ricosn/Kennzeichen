using Kennzeichen.Recognizers.Onnx;

namespace Kennzeichen.Tests;

public class LetterboxTests
{
    [Fact]
    public void Matches_python_letterbox_for_landscape_image()
    {
        // 518x331 ist die Größe des Testbildes; Werte nachgerechnet mit open_image_models letterbox().
        var box = Letterbox.Compute(518, 331, 384);

        Assert.Equal(384, box.ScaledWidth);
        Assert.Equal(245, box.ScaledHeight);
        Assert.Equal(0, box.PadX);
        Assert.Equal(69.5, box.PadY);
        Assert.Equal((69, 70), (box.Top, box.Bottom));
        Assert.Equal((0, 0), (box.Left, box.Right));
        Assert.Equal(384, box.ScaledHeight + box.Top + box.Bottom);
    }

    [Fact]
    public void Maps_model_coordinates_back_to_original()
    {
        var box = Letterbox.Compute(1000, 500, 100);

        Assert.Equal((500.0, 250.0), box.ToOriginal(50, 50));
        Assert.Equal((0.0, 0.0), box.ToOriginal(0, 25));
    }
}
