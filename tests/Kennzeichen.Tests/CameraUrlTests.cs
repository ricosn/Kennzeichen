using Kennzeichen.Sources.OpenCv;

namespace Kennzeichen.Tests;

public class CameraUrlTests
{
    [Fact]
    public void Inserts_credentials_like_axis_documents_for_rtsp()
    {
        var url = CameraUrl.WithCredentials("rtsp://192.168.0.90/axis-media/media.amp?camera=2", "root", "geheim");

        Assert.Equal("rtsp://root:geheim@192.168.0.90/axis-media/media.amp?camera=2", url);
    }

    [Fact]
    public void Escapes_special_characters_in_password()
    {
        var url = CameraUrl.WithCredentials("rtsp://cam/axis-media/media.amp", "user", "p@ss:wort");

        Assert.Equal("rtsp://user:p%40ss%3Awort@cam/axis-media/media.amp", url);
    }

    [Fact]
    public void Leaves_url_unchanged_without_username()
    {
        Assert.Equal("rtsp://cam/x", CameraUrl.WithCredentials("rtsp://cam/x", null, "egal"));
    }
}
