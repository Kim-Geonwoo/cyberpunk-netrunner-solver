using System.ComponentModel;
using System.Runtime.InteropServices;
using NetrunnerSolver.Windows.Interop;

namespace NetrunnerSolver.Windows.Capture;

public sealed record MonitorArea(IntPtr Handle, int Left, int Top, int Width, int Height, double DpiScale);

public static class MonitorLocator
{
    public static MonitorArea FromForegroundWindow()
    {
        using var scope = new NativeMethods.PerMonitorDpiScope();
        var window = NativeMethods.GetForegroundWindow();
        var monitor = window == IntPtr.Zero
            ? NativeMethods.MonitorFromPoint(0, NativeMethods.MonitorDefaultToPrimary)
            : NativeMethods.MonitorFromWindow(window, NativeMethods.MonitorDefaultToNearest);
        return Describe(monitor);
    }

    public static MonitorArea Primary()
    {
        using var scope = new NativeMethods.PerMonitorDpiScope();
        return Describe(NativeMethods.MonitorFromPoint(0, NativeMethods.MonitorDefaultToPrimary));
    }

    private static MonitorArea Describe(IntPtr monitor)
    {
        if (monitor == IntPtr.Zero)
        {
            throw new InvalidOperationException("No monitor was found.");
        }

        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetMonitorInfo failed.");
        }

        var scale = NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdtEffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0
            ? dpiX / 96.0
            : 1.0;
        var rect = info.Monitor;
        return new MonitorArea(monitor, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, scale);
    }
}
