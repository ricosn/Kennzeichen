using Kennzeichen.Recognizers.Onnx;

namespace Kennzeichen.Tests;

public class PlateOcrConfigTests
{
    // Auszug aus cct_xs_v2_global_plate_config.yaml (fast-plate-ocr, Release "arg-plates").
    private const string GlobalConfig = """
        max_plate_slots: 10
        alphabet: '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_'
        pad_char: '_'
        img_height: 64
        img_width: 128
        keep_aspect_ratio: false
        interpolation: linear
        image_color_mode: rgb
        plate_regions: [ 'Albania', 'Germany',
                         'Unknown' ]
        """;

    // european_mobile_vit_v2_ocr_config.yaml ohne Kommentare; image_color_mode fehlt, Standard ist grayscale.
    private const string EuropeanConfig = """
        max_plate_slots: 9
        alphabet: '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_'
        pad_char: '_'
        img_height: 70
        img_width: 140
        """;

    [Fact]
    public void Parses_global_config_with_regions()
    {
        var config = PlateOcrConfig.Parse(GlobalConfig);

        Assert.Equal(10, config.MaxPlateSlots);
        Assert.Equal(37, config.Alphabet.Length);
        Assert.True(config.IsRgb);
        Assert.Equal(3, config.Channels);
        Assert.Equal(["Albania", "Germany", "Unknown"], config.PlateRegions);
    }

    [Fact]
    public void Missing_color_mode_defaults_to_grayscale()
    {
        var config = PlateOcrConfig.Parse(EuropeanConfig);

        Assert.False(config.IsRgb);
        Assert.Equal(1, config.Channels);
        Assert.Equal((140, 70), (config.ImgWidth, config.ImgHeight));
        Assert.Null(config.PlateRegions);
    }

    [Theory]
    [InlineData("padding_color: 0", new[] { 0 })]
    [InlineData("padding_color: [1, 2, 3]", new[] { 1, 2, 3 })]
    public void Padding_color_accepts_scalar_and_list(string line, int[] expected)
    {
        var config = PlateOcrConfig.Parse(EuropeanConfig + "\n" + line);

        Assert.Equal(expected, config.PaddingColor);
    }

    [Fact]
    public void Rejects_pad_char_outside_alphabet()
    {
        Assert.Throws<InvalidDataException>(() => PlateOcrConfig.Parse(EuropeanConfig.Replace("pad_char: '_'", "pad_char: '#'")));
    }
}
