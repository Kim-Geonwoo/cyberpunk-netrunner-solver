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

    [FixtureFact("game-ko-5x5-b.webp")]
    public async Task EnginesReadCodesFromGameScreenshot()
    {
        var image = await ImageFile.LoadAsync(TestFixtures.Path("game-ko-5x5-b.webp"));

        var words = await OcrEngines.CreateAvailable()[0].RecognizeAsync(image, CancellationToken.None);

        Assert.NotEmpty(words);
        Assert.All(words, w => Assert.True(w.Box.Width > 0 && w.Box.Height > 0));
    }
}
