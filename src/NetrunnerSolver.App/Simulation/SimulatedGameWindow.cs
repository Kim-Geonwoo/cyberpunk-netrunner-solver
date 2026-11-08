using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetrunnerSolver.App.Interop;
using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Windows.Capture;

namespace NetrunnerSolver.App.Simulation;

internal sealed class SimulatedGameWindow : Window
{
    private readonly BgraImage _image;
    private readonly MonitorArea _area;

    public SimulatedGameWindow(BgraImage image, MonitorArea area)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _area = area ?? throw new ArgumentNullException(nameof(area));
        Title = "Netrunner Solver Simulation";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Black;
        var dpi = 96 * area.DpiScale;
        var bitmap = BitmapSource.Create(image.Width, image.Height, dpi, dpi, PixelFormats.Bgra32, null, image.Pixels.ToArray(), image.Width * 4);
        bitmap.Freeze();
        Content = new Image { Source = bitmap, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        SourceInitialized += (_, _) => Place();
        Loaded += (_, _) =>
        {
            Place();
            _ = AppNative.SetForegroundWindow(new WindowInteropHelper(this).Handle);
        };
    }

    private void Place()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        _ = AppNative.SetWindowPos(hwnd, IntPtr.Zero, _area.Left, _area.Top, _image.Width, _image.Height, 0);
    }
}
