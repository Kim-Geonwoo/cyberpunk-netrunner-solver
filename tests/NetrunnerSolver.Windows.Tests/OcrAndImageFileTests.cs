using System.Buffers.Binary;
using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Windows.Imaging;
using NetrunnerSolver.Windows.Ocr;

namespace NetrunnerSolver.Windows.Tests;

public class OcrAndImageFileTests
{
    [Fact]
    public void CreateAvailablePutsNonEnglishFirst()
    {
        var engines = OcrEngines.CreateAvailable();

        Assert.NotEmpty(engines);
        if (engines.Any(e => !e.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.False(engines[0].Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void CreateAvailableFiltersToOneLanguage()
    {
        Assert.Empty(OcrEngines.CreateAvailable("xx-NOPE"));
    }

    [FixtureFact("game-ko-5x5-b.webp")]
    public async Task SaveThenLoadRoundTrips()
    {
        var image = await ImageFile.LoadAsync(TestFixtures.Path("game-ko-5x5-b.webp"));
        var path = Path.Combine(Path.GetTempPath(), $"nrs-{Guid.NewGuid():N}.png");
        try
        {
            await ImageFile.SavePngAsync(image, path);
            var again = await ImageFile.LoadAsync(path);

            Assert.Equal(1306, again.Width);
            Assert.Equal(756, again.Height);
            Assert.True(image.Pixels.SequenceEqual(again.Pixels));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PixelsAreHandedOverWithoutACopyAndStillRoundTrip()
    {
        var pixels = new byte[3 * 2 * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = i % 4 == 3 ? (byte)255 : (byte)(i * 9);
        }

        var image = new BgraImage(3, 2, pixels);
        Assert.Same(pixels, ImageFile.SharedPixels(image));

        var path = Path.Combine(Path.GetTempPath(), $"nrs-{Guid.NewGuid():N}.png");
        try
        {
            await ImageFile.SavePngAsync(image, path);
            var again = await ImageFile.LoadAsync(path);

            Assert.Equal(pixels, again.Pixels.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImageLargerThanTheLimitIsRejectedFromItsHeader()
    {
        // 1×1 PNG의 IHDR만 20000×20000으로 고친다. 픽셀을 풀기 전에 머리글 크기로 거절해야 한다.
        var path = Path.Combine(Path.GetTempPath(), $"nrs-huge-{Guid.NewGuid():N}.png");
        try
        {
            await ImageFile.SavePngAsync(new BgraImage(1, 1, new byte[4]), path);
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal("IHDR"u8.ToArray(), bytes[12..16]);
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), 20000);
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), 20000);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29), Crc32(bytes.AsSpan(12, 17)));
            await File.WriteAllBytesAsync(path, bytes);

            var error = await Assert.ThrowsAsync<ArgumentException>(() => ImageFile.LoadAsync(path));
            Assert.Contains("20000x20000", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [FixtureFact("game-ko-5x5-b.webp")]
    public async Task EnginesReadCodesFromGameScreenshot()
    {
        var image = await ImageFile.LoadAsync(TestFixtures.Path("game-ko-5x5-b.webp"));

        var words = await OcrEngines.CreateAvailable()[0].RecognizeAsync(image, CancellationToken.None);

        Assert.NotEmpty(words);
        Assert.All(words, w => Assert.True(w.Box.Width > 0 && w.Box.Height > 0));
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
