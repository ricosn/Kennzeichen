using Kennzeichen.Service;

namespace Kennzeichen.Tests;

public class RepeatFilterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Suppresses_same_plate_within_window()
    {
        var filter = new RepeatFilter(TimeSpan.FromSeconds(30));

        Assert.True(filter.ShouldReport("cam", "AB123", T0));
        Assert.False(filter.ShouldReport("cam", "AB123", T0.AddSeconds(10)));
        Assert.True(filter.ShouldReport("cam", "XY999", T0.AddSeconds(10)));
        Assert.True(filter.ShouldReport("other", "AB123", T0.AddSeconds(10)));
    }

    [Fact]
    public void Standing_vehicle_is_reported_once_while_it_stays_visible()
    {
        var filter = new RepeatFilter(TimeSpan.FromSeconds(30));

        Assert.True(filter.ShouldReport("cam", "AB123", T0));
        Assert.False(filter.ShouldReport("cam", "AB123", T0.AddSeconds(25)));
        Assert.False(filter.ShouldReport("cam", "AB123", T0.AddSeconds(50)));
        Assert.True(filter.ShouldReport("cam", "AB123", T0.AddSeconds(90)));
    }

    [Fact]
    public void Zero_window_reports_everything()
    {
        var filter = new RepeatFilter(TimeSpan.Zero);

        Assert.True(filter.ShouldReport("cam", "AB123", T0));
        Assert.True(filter.ShouldReport("cam", "AB123", T0));
    }
}
