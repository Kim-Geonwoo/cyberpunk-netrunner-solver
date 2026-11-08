using System.Runtime.InteropServices;

namespace NetrunnerSolver.App.Interop;

internal static partial class AppNative
{
    public const int GwlExStyle = -20;
    public const long WsExTransparent = 0x20;
    public const long WsExToolWindow = 0x80;
    public const long WsExLayered = 0x80000;
    public const long WsExNoActivate = 0x08000000;
    public const uint SwpNoSize = 0x1;
    public const uint SwpNoActivate = 0x10;
    public static readonly IntPtr HwndTopmost = new(-1);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr hwnd);
}
