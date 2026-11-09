namespace NetrunnerSolver.Core.Imaging;

public sealed class BgraImage
{
    // Direct3D 11 텍스처 한 변의 최대치. 모니터 한 대의 캡처는 이보다 클 수 없고,
    // 이 안에서는 가로×세로×4가 int를 넘지 않는다(16384²×4 = 2^30).
    public const int MaxDimension = 16384;

    private readonly byte[] _pixels;

    public BgraImage(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var expected = ByteCount(width, height);
        if (pixels.Length != expected)
        {
            throw new ArgumentException($"Expected {expected} bytes, got {pixels.Length}.", nameof(pixels));
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

    // 픽셀을 복사해 가는 API(WinRT·WPF)에 배열째 넘길 때 쓴다. 읽기 전용으로만 다룬다.
    public ReadOnlyMemory<byte> PixelMemory => _pixels;

    // 픽셀 버퍼 크기. 한 변이 1..MaxDimension을 벗어나면 할당 전에 거절한다.
    public static int ByteCount(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
        {
            throw new ArgumentException($"Image size must be 1..{MaxDimension} per side, got {width}x{height}.", nameof(width));
        }

        return checked(width * height * 4);
    }

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
        var pixels = new byte[ByteCount(width, height)];
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
