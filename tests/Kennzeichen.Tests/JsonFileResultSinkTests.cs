using System.Text.Json;
using Kennzeichen.Abstractions;
using Kennzeichen.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kennzeichen.Tests;

public class JsonFileResultSinkTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 11, 18, 0, 0, TimeSpan.Zero);

    private static RecognitionResult Result(string text) =>
        new("kamera", Day, "Onnx", new PlateReading(text, 0.9f, 0.8f, new PlateBox(1, 2, 3, 4), "Germany"));

    [Fact]
    public async Task Writes_valid_json_array_with_spaced_text_and_appends_across_restarts()
    {
        var folder = Directory.CreateTempSubdirectory("kennzeichen-results-");
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await new JsonFileResultSink(folder.FullName, NullLogger<JsonFileResultSink>.Instance).PublishAsync(Result("M AB 1234"), ct);

            // Neue Instanz wie nach einem Neustart des Dienstes.
            await new JsonFileResultSink(folder.FullName, NullLogger<JsonFileResultSink>.Instance).PublishAsync(Result("B XY 99"), ct);

            var path = Path.Combine(folder.FullName, "kennzeichen-2026-10-11.json");
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
            var entries = json.RootElement.EnumerateArray().ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal("M AB 1234", entries[0].GetProperty("reading").GetProperty("text").GetString());
            Assert.Equal("MAB1234", entries[0].GetProperty("reading").GetProperty("compactText").GetString());
            Assert.Equal("B XY 99", entries[1].GetProperty("reading").GetProperty("text").GetString());
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Appends_to_empty_array_and_moves_broken_file_aside()
    {
        var folder = Directory.CreateTempSubdirectory("kennzeichen-results-");
        try
        {
            var ct = TestContext.Current.CancellationToken;
            var path = Path.Combine(folder.FullName, "kennzeichen-2026-10-11.json");
            var sink = new JsonFileResultSink(folder.FullName, NullLogger<JsonFileResultSink>.Instance);

            await File.WriteAllTextAsync(path, "[ ]\n", ct);
            await sink.PublishAsync(Result("M AB 1"), ct);
            Assert.Single(JsonDocument.Parse(await File.ReadAllTextAsync(path, ct)).RootElement.EnumerateArray());

            await File.WriteAllTextAsync(path, "{ kaputt", ct);
            await sink.PublishAsync(Result("M AB 2"), ct);
            Assert.Single(JsonDocument.Parse(await File.ReadAllTextAsync(path, ct)).RootElement.EnumerateArray());
            Assert.Single(Directory.GetFiles(folder.FullName, "*.defekt-*"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
