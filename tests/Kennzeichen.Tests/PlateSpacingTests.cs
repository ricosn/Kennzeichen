using Kennzeichen.Recognizers.Onnx;
using OpenCvSharp;

namespace Kennzeichen.Tests;

public class PlateSpacingTests
{
    [Fact]
    public void Inserts_space_where_gap_is_clearly_larger()
    {
        // Abstände wie im ausgeschnittenen Testbild "5AU 5341": 8, 1, 39, 4, 7, 7 Pixel.
        int[] lefts = [47, 84, 108, 176, 208, 241, 271];
        int[] widths = [29, 23, 29, 28, 26, 23, 21];
        var boxes = lefts.Select((x, i) => new Rect(x, 25, widths[i], 46)).ToList();

        Assert.Equal("5AU 5341", PlateSpacing.InsertByGaps("5AU5341", boxes, 2.5));
    }

    [Fact]
    public void Always_splits_letters_from_digits_even_when_gaps_are_even()
    {
        var boxes = Enumerable.Range(0, 7).Select(i => new Rect(i * 30, 0, 20, 40)).ToList();

        Assert.Equal("MAB 1234", PlateSpacing.InsertByGaps("MAB1234", boxes, 2.5));
    }

    [Theory]
    [InlineData("MAB1234", "MAB 1234")]
    [InlineData("B1234", "B 1234")]
    [InlineData("1234AB", "1234AB")]
    public void Fallback_splits_only_at_letter_to_digit(string text, string expected)
    {
        Assert.Equal(expected, PlateSpacing.InsertAtLetterDigitBoundary(text));
    }

    [Fact]
    public void Finds_gaps_on_synthetic_german_style_plate_with_eu_band_and_seals()
    {
        using var plate = DrawPlate();

        var boxes = PlateSpacing.FindCharacterBoxes(plate);

        Assert.Equal(7, boxes.Count);
        Assert.Equal("M AB 1234", PlateSpacing.Insert(plate, "MAB1234", 2.5));
    }

    [Fact]
    public void Falls_back_to_letter_digit_split_when_character_count_does_not_match()
    {
        using var plate = DrawPlate();

        // Das Modell hat ein Zeichen zu wenig gelesen; die 7 gefundenen Zeichen passen nicht zu 6 Buchstaben/Ziffern.
        Assert.Equal("MA 1234", PlateSpacing.Insert(plate, "MA1234", 2.5));
    }

    /// <summary>Schwarze Schrift auf Weiß, links ein blaues Feld, zwischen Bezirk und Buchstaben zwei runde Plaketten.</summary>
    private static Mat DrawPlate()
    {
        var plate = new Mat(new Size(600, 110), MatType.CV_8UC3, Scalar.White);
        Cv2.Rectangle(plate, new Rect(0, 0, 40, 110), new Scalar(153, 51, 0), -1);
        Cv2.Rectangle(plate, new Rect(0, 0, 600, 110), Scalar.Black, 4);

        void Text(string s, int x) =>
            Cv2.PutText(plate, s, new Point(x, 88), HersheyFonts.HersheyDuplex, 2.6, Scalar.Black, 7);

        Text("M", 55);
        Cv2.Circle(plate, new Point(150, 35), 17, new Scalar(0, 140, 255), -1);
        Cv2.Circle(plate, new Point(150, 75), 17, new Scalar(80, 80, 80), -1);
        Text("AB", 180);
        Text("1234", 310);
        return plate;
    }
}
