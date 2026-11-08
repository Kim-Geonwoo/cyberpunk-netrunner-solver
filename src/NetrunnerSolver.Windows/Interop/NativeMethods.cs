using System.Runtime.InteropServices;

namespace NetrunnerSolver.Windows.Interop;

internal static partial class NativeMethods
{
    public const uint MonitorDefaultToPrimary = 1;
    public const uint MonitorDefaultToNearest = 2;
    public const int MdtEffectiveDpi = 0;
    public const uint SrcCopy = 0x00CC0020;
    public const uint CaptureBlt = 0x40000000;
    public const uint DibRgbColors = 0;
    public static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromPoint(long point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    public static partial IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetDC(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateCompatibleDC(IntPtr dc);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint rop);

    [LibraryImport("gdi32.dll")]
    public static partial int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, [Out] byte[] bits, ref BitmapInfoHeader info, uint usage);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(IntPtr dc);

    public sealed class PerMonitorDpiScope : IDisposable
    {
        private readonly IntPtr _previous = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);

        public void Dispose()
        {
            if (_previous != IntPtr.Zero)
            {
                _ = SetThreadDpiAwarenessContext(_previous);
            }
        }
    }
}
