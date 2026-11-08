using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

public class TokenNormalizerTests
{
    private static readonly Box Area = new(100, 50, 40, 20);

    [Theory]
    [InlineData("IC", 0x1C)]
    [InlineData("lC", 0x1C)]
    [InlineData("B0", 0xBD)]
    [InlineData("8D", 0xBD)]
    [InlineData("S5", 0x55)]
    [InlineData("Eg", 0xE9)]
    [InlineData("TA", 0x7A)]
    [InlineData("74", 0x7A)]
    [InlineData("ff", 0xFF)]
    [InlineData("55.", 0x55)]
    public void ConfusableReadingsBecomeTheGameCode(string text, int expected)
    {
        var tokens = TokenNormalizer.Normalize(new OcrWord(text, Area));

        var token = Assert.Single(tokens);
        Assert.Equal((byte)expected, token.Code);
        Assert.Equal(Area, token.Box);
    }

    [Fact]
    public void MergedTokensAreSplitWithEqualBoxes()
    {
        var tokens = TokenNormalizer.Normalize(new OcrWord("1C55", Area));

        Assert.Equal(
            [new Token(0x1C, new Box(100, 50, 20, 20)), new Token(0x55, new Box(120, 50, 20, 20))],
            tokens);
    }

    [Theory]
    [InlineData("88")]
    [InlineData("5")]
    [InlineData("데이터")]
    [InlineData("ABC")]
    [InlineData("")]
    public void NonCodesAreDropped(string text)
    {
        Assert.Empty(TokenNormalizer.Normalize(new OcrWord(text, Area)));
    }
}
