using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Tests.Recognition;

internal sealed class SyntheticImage(int width, int height)
{
    public static readonly (byte B, byte G, byte R) Yellow = (87, 237, 208);
    public static readonly (byte B, byte G, byte R) White = (255, 255, 255);

    private readonly byte[] _pixels = new byte[width * height * 4];

    public SyntheticImage Fill(int x, int y, int w, int h, (byte B, byte G, byte R) color)
    {
        for (var row = y; row < y + h; row++)
        {
            for (var col = x; col < x + w; col++)
            {
                Set(col, row, color);
            }
        }

        return this;
    }

    public SyntheticImage Outline(int x, int y, int w, int h, int thickness, (byte B, byte G, byte R) color) =>
        Fill(x, y, w, thickness, color)
            .Fill(x, y + h - thickness, w, thickness, color)
            .Fill(x, y, thickness, h, color)
            .Fill(x + w - thickness, y, thickness, h, color);

    public SyntheticImage DashedSquare(int x, int y, int size, int thickness, int on, int off, (byte B, byte G, byte R) color)
    {
        for (var i = 0; i < size; i++)
        {
            if (i % (on + off) >= on)
            {
                continue;
            }

            Fill(x + i, y, 1, thickness, color);
            Fill(x + i, y + size - thickness, 1, thickness, color);
            Fill(x, y + i, thickness, 1, color);
            Fill(x + size - thickness, y + i, thickness, 1, color);
        }

        return this;
    }

    public SyntheticImage Pattern(int x, int y, bool[,] bits, byte on, byte background, int noise, Random random)
    {
        for (var r = 0; r < bits.GetLength(0); r++)
        {
            for (var c = 0; c < bits.GetLength(1); c++)
            {
                var value = Math.Clamp((bits[r, c] ? on : background) + random.Next(-noise, noise + 1), 0, 255);
                Set(x + c, y + r, ((byte)value, (byte)value, (byte)value));
            }
        }

        return this;
    }

    public static bool[,] RandomBits(int rows, int cols, int seed)
    {
        var random = new Random(seed);
        var bits = new bool[rows, cols];
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                bits[r, c] = random.Next(2) == 1;
            }
        }

        return bits;
    }

    public BgraImage ToImage() => new(width, height, (byte[])_pixels.Clone());

    private void Set(int x, int y, (byte B, byte G, byte R) color)
    {
        var i = ((y * width) + x) * 4;
        _pixels[i] = color.B;
        _pixels[i + 1] = color.G;
        _pixels[i + 2] = color.R;
        _pixels[i + 3] = 255;
    }
}
