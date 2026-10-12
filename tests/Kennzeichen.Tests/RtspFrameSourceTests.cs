using Kennzeichen.Sources.OpenCv;
using OpenCvSharp;

namespace Kennzeichen.Tests;

public class RtspFrameSourceTests
{
    [Theory]
    [InlineData(true, VideoAccelerationType.Any)]
    [InlineData(false, VideoAccelerationType.None)]
    public void Requests_hardware_decoding_only_when_enabled(bool enabled, VideoAccelerationType expected)
    {
        var parameters = RtspFrameSource.OpenParameters(new CameraOptions { HardwareDecoding = enabled });

        Assert.Equal([(int)VideoCaptureProperties.HwAcceleration, (int)expected], parameters);
    }
}
