using System.ComponentModel;
using System.Runtime.InteropServices;
using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Windows.Interop;

namespace NetrunnerSolver.Windows.Capture;

public static class GdiCapture
{
    public static BgraImage Capture(MonitorArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        using var scope = new NativeMethods.PerMonitorDpiScope();
        var screen = NativeMethods.GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetDC failed.");
        }

        var memory = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        try
        {
            memory = NativeMethods.CreateCompatibleDC(screen);
            bitmap = NativeMethods.CreateCompatibleBitmap(screen, area.Width, area.Height);
            if (memory == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not create a GDI capture bitmap.");
            }

            var old = NativeMethods.SelectObject(memory, bitmap);
            var copied = NativeMethods.BitBlt(memory, 0, 0, area.Width, area.Height, screen, area.Left, area.Top, NativeMethods.SrcCopy | NativeMethods.CaptureBlt);
            _ = NativeMethods.SelectObject(memory, old);
            if (!copied)
            {
                throw new InvalidOperationException("BitBlt failed.");
            }

            var header = new NativeMethods.BitmapInfoHeader
            {
                Size = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                Width = area.Width,
                Height = -area.Height,
                Planes = 1,
                BitCount = 32,
            };
            var pixels = new byte[area.Width * area.Height * 4];
            if (NativeMethods.GetDIBits(memory, bitmap, 0, (uint)area.Height, pixels, ref header, NativeMethods.DibRgbColors) != area.Height)
            {
                throw new InvalidOperationException("GetDIBits failed.");
            }

            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            return new BgraImage(area.Width, area.Height, pixels);
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                _ = NativeMethods.DeleteObject(bitmap);
            }

            if (memory != IntPtr.Zero)
            {
                _ = NativeMethods.DeleteDC(memory);
            }

            _ = NativeMethods.ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
