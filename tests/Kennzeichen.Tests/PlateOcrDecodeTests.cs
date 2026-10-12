using Kennzeichen.Recognizers.Onnx;

namespace Kennzeichen.Tests;

public class PlateOcrDecodeTests
{
    private static readonly PlateOcrConfig Config = PlateOcrConfig.Parse("""
        max_plate_slots: 4
        alphabet: 'AB1_'
        pad_char: '_'
        img_height: 10
        img_width: 20
        plate_regions: ['X', 'Y']
        """);

    [Fact]
    public void Picks_best_char_per_slot_and_strips_trailing_padding()
    {
        float[] scores =
        [
            0.9f, 0.05f, 0.03f, 0.02f, // A
            0.1f, 0.1f, 0.7f, 0.1f,    // 1
            0.0f, 0.0f, 0.2f, 0.8f,    // _
            0.0f, 0.0f, 0.0f, 1.0f,    // _
        ];

        var result = PlateOcr.Decode(scores, [0.3f, 0.7f], Config);

        Assert.Equal("A1", result.Text);
        Assert.Equal("Y", result.Region);
        Assert.Equal([0.9f, 0.7f, 0.8f, 1.0f], result.SlotConfidences);
        Assert.Equal(0.85f, result.Confidence, 3);
    }

    [Fact]
    public void Keeps_padding_char_in_the_middle()
    {
        float[] scores =
        [
            1, 0, 0, 0, // A
            0, 0, 0, 1, // _
            0, 1, 0, 0, // B
            0, 0, 0, 1, // _
        ];

        Assert.Equal("A_B", PlateOcr.Decode(scores, null, Config).Text);
    }

    [Fact]
    public void Rejects_output_of_wrong_size()
    {
        Assert.Throws<InvalidDataException>(() => PlateOcr.Decode(new float[15], null, Config));
    }
}
