using System.Windows;
using System.Windows.Interop;
using NetrunnerSolver.App.Interop;
using NetrunnerSolver.Windows.Capture;

namespace NetrunnerSolver.App.Overlay;

internal static class WindowStyles
{
    public static void MakeOverlay(Window window, bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = AppNative.GetWindowLongPtr(hwnd, AppNative.GwlExStyle).ToInt64();
        style |= AppNative.WsExLayered | AppNative.WsExNoActivate | AppNative.WsExToolWindow;
        if (clickThrough)
        {
            style |= AppNative.WsExTransparent;
        }

        _ = AppNative.SetWindowLongPtr(hwnd, AppNative.GwlExStyle, new IntPtr(style));
    }

    public static void PlaceOnMonitor(Window window, MonitorArea area)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();

        // 배율이 다른 모니터로 옮기면 첫 호출 뒤 WM_DPICHANGED가 크기를 줄여 놓으므로 한 번 더 맞춘다.
        _ = AppNative.SetWindowPos(hwnd, AppNative.HwndTopmost, area.Left, area.Top, area.Width, area.Height, AppNative.SwpNoActivate);
        _ = AppNative.SetWindowPos(hwnd, AppNative.HwndTopmost, area.Left, area.Top, area.Width, area.Height, AppNative.SwpNoActivate);
    }

    public static void MoveTo(Window window, int x, int y)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        _ = AppNative.SetWindowPos(hwnd, AppNative.HwndTopmost, x, y, 0, 0, AppNative.SwpNoActivate | AppNative.SwpNoSize);
    }
}
