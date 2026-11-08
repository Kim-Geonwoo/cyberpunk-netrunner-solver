using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

public class BufferAndGlyphTests
{
    private static readonly GridGeometry Grid = new(5, 112, 212, 40, 40, 24, 24, 0, 0);

    private static SyntheticImage BufferScene()
    {
        var scene = new SyntheticImage(600, 400).Outline(300, 40, 220, 80, 2, SyntheticImage.Yellow);
        for (var k = 0; k < 4; k++)
        {
            scene.DashedSquare(312 + (50 * k), 62, 36, 2, 6, 3, SyntheticImage.Yellow);
        }

        return scene;
    }

    [Fact]
    public void CountsEmptyDashedSlotsAboveTheMatrix()
    {
        var reading = BufferDetector.Detect(BufferScene().ToImage(), Grid);

        Assert.NotNull(reading);
        Assert.Equal(4, reading.SlotCount);
        Assert.False(reading.HasContent);
    }

    [Fact]
    public void CountsASlotWhoseDashesFallApart()
    {
        var scene = new SyntheticImage(600, 400).Outline(300, 40, 220, 80, 2, SyntheticImage.Yellow);
        for (var k = 0; k < 4; k++)
        {
            scene.DashedSquare(312 + (50 * k), 62, 36, 2, k == 1 ? 4 : 6, k == 1 ? 9 : 3, SyntheticImage.Yellow);
        }

        var reading = BufferDetector.Detect(scene.ToImage(), Grid);

        Assert.NotNull(reading);
        Assert.Equal(4, reading.SlotCount);
    }

    [Fact]
    public void FilledBarNextToTheSlotsIsNotCountedAsSlots()
    {
        var scene = BufferScene().Fill(300, 130, 200, 40, SyntheticImage.Yellow).DashedSquare(520, 132, 36, 2, 6, 3, SyntheticImage.Yellow);

        var reading = BufferDetector.Detect(scene.ToImage(), Grid);

        Assert.NotNull(reading);
        Assert.Equal(4, reading.SlotCount);
        Assert.False(reading.HasContent);
    }

    [Fact]
    public void FlagsSlotsThatAlreadyHoldACode()
    {
        var reading = BufferDetector.Detect(BufferScene().Fill(372, 72, 16, 16, SyntheticImage.White).ToImage(), Grid);

        Assert.NotNull(reading);
        Assert.True(reading.HasContent);
    }

    [Fact]
    public void ReturnsNullWithoutSlots()
    {
        Assert.Null(BufferDetector.Detect(new SyntheticImage(600, 400).ToImage(), Grid));
    }

    [Fact]
    public void GlyphMatcherRecognisesASampledCodeUnderNoise()
    {
        var random = new Random(7);
        var a = SyntheticImage.RandomBits(16, 24, 1);
        var b = SyntheticImage.RandomBits(16, 24, 2);
        var image = new SyntheticImage(200, 40)
            .Pattern(10, 10, a, 237, 0, 0, random)
            .Pattern(50, 10, b, 237, 0, 0, random)
            .Pattern(100, 10, a, 237, 40, 10, random)
            .Pattern(140, 10, SyntheticImage.RandomBits(16, 24, 3), 237, 0, 0, random)
            .ToImage();
        Token[] samples = [new(0x1C, new Box(10, 10, 24, 16)), new(0x55, new Box(50, 10, 24, 16))];

        Assert.Equal(0x1C, GlyphMatcher.Classify(image, new Box(100, 10, 24, 16), samples));
        Assert.Equal(BreachCodes.Unknown, GlyphMatcher.Classify(image, new Box(140, 10, 24, 16), samples));
        Assert.Equal(BreachCodes.Unknown, GlyphMatcher.Classify(image, new Box(100, 10, 24, 16), []));
    }

    [Fact]
    public void GlyphMatcherToleratesAMisalignedBox()
    {
        var random = new Random(9);
        var a = SyntheticImage.RandomBits(16, 24, 1);
        var b = SyntheticImage.RandomBits(16, 24, 2);
        var image = new SyntheticImage(200, 40)
            .Pattern(10, 10, a, 237, 0, 0, random)
            .Pattern(50, 10, b, 237, 0, 0, random)
            .Pattern(100, 12, a, 237, 0, 0, random)
            .ToImage();
        Token[] samples = [new(0x1C, new Box(10, 10, 24, 16)), new(0x55, new Box(50, 10, 24, 16))];

        Assert.Equal(0x1C, GlyphMatcher.Classify(image, new Box(97, 9, 24, 16), samples));
    }

    [Fact]
    public void GlyphMatcherTreatsFlatCropsAsUnknown()
    {
        var image = new SyntheticImage(100, 40).ToImage();
        Token[] samples = [new(0x1C, new Box(10, 10, 24, 16))];

        Assert.Equal(BreachCodes.Unknown, GlyphMatcher.Classify(image, new Box(50, 10, 24, 16), samples));
    }
}
