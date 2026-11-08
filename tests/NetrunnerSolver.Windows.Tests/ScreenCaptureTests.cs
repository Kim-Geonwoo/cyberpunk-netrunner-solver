using NetrunnerSolver.Windows.Capture;
using Xunit.Abstractions;

namespace NetrunnerSolver.Windows.Tests;

public class ScreenCaptureTests(ITestOutputHelper output)
{
    [Fact]
    public void PrimaryMonitorHasPhysicalSizeAndScale()
    {
        var area = MonitorLocator.Primary();

        output.WriteLine($"primary: {area.Width}x{area.Height} at {area.Left},{area.Top} scale {area.DpiScale}");
        Assert.True(area.Width > 0 && area.Height > 0);
        Assert.True(area.DpiScale >= 1);
    }

    [Fact]
    public void CapturesTheWholePrimaryMonitor()
    {
        var area = MonitorLocator.Primary();

        var image = ScreenCapture.Capture(area, out var method);

        output.WriteLine($"method: {method}");
        Assert.Equal(area.Width, image.Width);
        Assert.Equal(area.Height, image.Height);
        Assert.False(image.IsMostlyBlack());
    }

    [Fact]
    public void GdiCaptureMatchesMonitorSize()
    {
        var area = MonitorLocator.Primary();

        var image = GdiCapture.Capture(area);

        Assert.Equal(area.Width, image.Width);
        Assert.Equal(area.Height, image.Height);
    }
}
