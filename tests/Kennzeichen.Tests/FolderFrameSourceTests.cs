using Kennzeichen.Sources.OpenCv;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kennzeichen.Tests;

public class FolderFrameSourceTests
{
    [Fact]
    public async Task Delivers_existing_images_with_file_name_in_source_id()
    {
        var folder = Directory.CreateTempSubdirectory("kennzeichen-bilder-");
        try
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "fast-alpr-test_image.png"), Path.Combine(folder.FullName, "auto.png"));
            File.WriteAllText(Path.Combine(folder.FullName, "notiz.txt"), "kein Bild");

            var source = new FolderFrameSource(
                new CameraOptions { Type = CameraSourceType.Folder, Name = "test", Url = folder.FullName },
                NullLogger<FolderFrameSource>.Instance);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await using var frames = source.ReadFramesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

            Assert.True(await frames.MoveNextAsync());
            Assert.Equal("test/auto.png", frames.Current.SourceId);
            Assert.Equal((518, 331), (frames.Current.Width, frames.Current.Height));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
