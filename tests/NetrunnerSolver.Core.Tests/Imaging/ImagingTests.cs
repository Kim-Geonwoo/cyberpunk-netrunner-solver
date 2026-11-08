using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Tests.Imaging;

public class ImagingTests
{
    [Fact]
    public void ToLocalDipMovesOriginAndDividesByScale()
    {
        var dip = new Box(150, 300, 40, 20).ToLocalDip(100, 200, 1.5);

        Assert.Equal(33.333333, dip.X, 5);
        Assert.Equal(66.666667, dip.Y, 5);
        Assert.Equal(26.666667, dip.Width, 5);
        Assert.Equal(13.333333, dip.Height, 5);
    }

    [Fact]
    public void BoxHelpersComputeEdgesAndCenter()
    {
        var box = new Box(10, 20, 30, 40);

        Assert.Equal(40, box.Right);
        Assert.Equal(60, box.Bottom);
        Assert.Equal(25, box.CenterX);
        Assert.Equal(40, box.CenterY);
        Assert.True(box.Contains(25, 40));
        Assert.False(box.Contains(41, 40));
        Assert.Equal(new Box(5, 18, 40, 44), box.Inflate(5, 2));
        Assert.Equal(new Box(11, 22, 30, 40), box.Offset(1, 2));
    }

    [Fact]
    public void CropClampsToImage()
    {
        var image = Solid(4, 4, 0);

        var crop = image.Crop(new Box(2, 2, 10, 10));

        Assert.Equal(2, crop.Width);
        Assert.Equal(2, crop.Height);
    }

    [Fact]
    public void CropRemembersWhereItCameFrom()
    {
        var crop = Solid(20, 20, 0).Crop(new Box(5, 6, 10, 10)).Crop(new Box(2, 3, 4, 4));

        Assert.Equal(7, crop.OffsetX);
        Assert.Equal(9, crop.OffsetY);
    }

    [Fact]
    public void CropOutsideImageThrows()
    {
        Assert.Throws<ArgumentException>(() => Solid(4, 4, 0).Crop(new Box(10, 10, 5, 5)));
    }

    [Fact]
    public void ConstructorRejectsWrongPixelCount()
    {
        Assert.Throws<ArgumentException>(() => new BgraImage(2, 2, new byte[15]));
    }

    [Fact]
    public void BrightnessIsMaxOfRedAndGreen()
    {
        var image = new BgraImage(1, 1, [10, 200, 120, 255]);

        Assert.Equal(200, image.Brightness(0, 0));
    }

    [Fact]
    public void IsMostlyBlackDetectsBlackFrames()
    {
        Assert.True(Solid(64, 64, 0).IsMostlyBlack());

        var half = Solid(64, 64, 0);
        var pixels = half.Pixels.ToArray();
        for (var i = 0; i < pixels.Length / 2; i++)
        {
            pixels[i] = 255;
        }

        Assert.False(new BgraImage(64, 64, pixels).IsMostlyBlack());
    }

    private static BgraImage Solid(int width, int height, byte value)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, value);
        return new BgraImage(width, height, pixels);
    }
}
