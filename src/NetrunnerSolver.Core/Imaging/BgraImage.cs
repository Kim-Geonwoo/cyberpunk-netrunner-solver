namespace NetrunnerSolver.Core.Imaging;

public sealed class BgraImage
{
    private readonly byte[] _pixels;

    public BgraImage(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException($"Image size must be positive, got {width}x{height}.", nameof(width));
        }

        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException($"Expected {width * height * 4} bytes, got {pixels.Length}.", nameof(pixels));
        }

        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int OffsetX { get; init; }

    public int OffsetY { get; init; }

    public ReadOnlySpan<byte> Pixels => _pixels;

    public byte Brightness(int x, int y)
    {
        var i = ((y * Width) + x) * 4;
        return Math.Max(_pixels[i + 1], _pixels[i + 2]);
    }

    public BgraImage Crop(Box area)
    {
        var left = Math.Clamp((int)Math.Round(area.X), 0, Width);
        var top = Math.Clamp((int)Math.Round(area.Y), 0, Height);
        var right = Math.Clamp((int)Math.Round(area.Right), 0, Width);
        var bottom = Math.Clamp((int)Math.Round(area.Bottom), 0, Height);
        if (right <= left || bottom <= top)
        {
            throw new ArgumentException($"Crop area {area} does not overlap the {Width}x{Height} image.", nameof(area));
        }

        var width = right - left;
        var height = bottom - top;
        var pixels = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            Array.Copy(_pixels, (((top + row) * Width) + left) * 4, pixels, row * width * 4, width * 4);
        }

        return new BgraImage(width, height, pixels) { OffsetX = OffsetX + left, OffsetY = OffsetY + top };
    }

    public bool IsMostlyBlack(double ratio = 0.98)
    {
        var total = 0;
        var dark = 0;
        for (var y = 0; y < Height; y += 16)
        {
            for (var x = 0; x < Width; x += 16)
            {
                total++;
                if (Brightness(x, y) <= 8)
                {
                    dark++;
                }
            }
        }

        return total > 0 && dark >= total * ratio;
    }
}
