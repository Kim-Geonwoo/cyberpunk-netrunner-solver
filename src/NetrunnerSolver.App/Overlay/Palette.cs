using System.Windows.Media;

namespace NetrunnerSolver.App.Overlay;

internal static class Palette
{
    public static SolidColorBrush Path { get; } = Frozen(0x2E, 0xE6, 0xF0);

    public static SolidColorBrush BadgeText { get; } = Frozen(0x0B, 0x0F, 0x14);

    public static SolidColorBrush Unknown { get; } = Frozen(0xFF, 0x4D, 0x6D);

    public static SolidColorBrush Panel { get; } = Frozen(0x0B, 0x0F, 0x14, 0xEE);

    public static SolidColorBrush PanelBorder { get; } = Frozen(0x2E, 0xE6, 0xF0, 0x88);

    public static SolidColorBrush Text { get; } = Frozen(0xE6, 0xED, 0xF3);

    public static SolidColorBrush Muted { get; } = Frozen(0x8B, 0x98, 0xA5);

    public static SolidColorBrush Success { get; } = Frozen(0x3D, 0xDC, 0x84);

    public static SolidColorBrush Cell { get; } = Frozen(0x1A, 0x22, 0x2C);

    private static SolidColorBrush Frozen(byte r, byte g, byte b, byte a = 0xFF)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
