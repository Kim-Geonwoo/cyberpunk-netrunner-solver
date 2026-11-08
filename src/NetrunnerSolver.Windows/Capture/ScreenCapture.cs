using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Windows.Capture;

public enum CaptureMethod
{
    DesktopDuplication,
    Gdi,
}

public static class ScreenCapture
{
    public static BgraImage Capture(MonitorArea area) => Capture(area, out _);

    public static BgraImage Capture(MonitorArea area, out CaptureMethod method)
    {
        ArgumentNullException.ThrowIfNull(area);
        try
        {
            var image = DxgiCapture.Capture(area);
            if (!image.IsMostlyBlack() && image.Width == area.Width && image.Height == area.Height)
            {
                method = CaptureMethod.DesktopDuplication;
                return image;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or TimeoutException or SharpGen.Runtime.SharpGenException or System.Runtime.InteropServices.COMException)
        {
        }

        method = CaptureMethod.Gdi;
        return GdiCapture.Capture(area);
    }
}
