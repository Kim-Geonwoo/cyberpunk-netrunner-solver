using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

internal static class GlyphScene
{
    public static BgraImage Draw(int width, int height, IEnumerable<OcrWord> words, params Box[] cursorBoxes)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
        }

        foreach (var token in words.SelectMany(TokenNormalizer.Normalize))
        {
            var bits = SyntheticImage.RandomBits(16, 24, token.Code);
            var box = token.Box;
            for (var y = 0; y < (int)box.Height; y++)
            {
                for (var x = 0; x < (int)box.Width; x++)
                {
                    if (!bits[y * 16 / (int)box.Height, x * 24 / (int)box.Width])
                    {
                        continue;
                    }

                    var px = (int)box.X + x;
                    var py = (int)box.Y + y;
                    if (px < 0 || py < 0 || px >= width || py >= height)
                    {
                        continue;
                    }

                    var i = ((py * width) + px) * 4;
                    pixels[i] = 87;
                    pixels[i + 1] = 237;
                    pixels[i + 2] = 208;
                }
            }
        }

        foreach (var box in cursorBoxes)
        {
            for (var y = (int)box.Y; y < (int)box.Bottom; y++)
            {
                for (var x = (int)box.X; x < (int)box.Right; x++)
                {
                    var edge = y < box.Y + 4 || y >= box.Bottom - 4 || x < box.X + 4 || x >= box.Right - 4;
                    var i = ((y * width) + x) * 4;
                    (pixels[i], pixels[i + 1], pixels[i + 2]) = edge ? ((byte)255, (byte)220, (byte)40) : ((byte)110, (byte)100, (byte)30);
                }
            }
        }

        return new BgraImage(width, height, pixels);
    }
}
