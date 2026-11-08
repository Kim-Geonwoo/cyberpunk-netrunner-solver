namespace NetrunnerSolver.Core.Tests;

public class BreachCodesTests
{
    [Fact]
    public void AllContainsTheSixGameCodesInOrder()
    {
        Assert.Equal(new byte[] { 0x1C, 0x55, 0x7A, 0xBD, 0xE9, 0xFF }, BreachCodes.All);
    }

    [Fact]
    public void FormatWritesTwoHexDigitsAndQuestionMarkForUnknown()
    {
        Assert.Equal("1C", BreachCodes.Format(0x1C));
        Assert.Equal("?", BreachCodes.Format(BreachCodes.Unknown));
    }

    [Fact]
    public void TryParseIgnoresCaseAndRejectsOtherValues()
    {
        Assert.True(BreachCodes.TryParse("bd", out var code));
        Assert.Equal(0xBD, code);
        Assert.False(BreachCodes.TryParse("12", out _));
        Assert.False(BreachCodes.TryParse("1C5", out _));
    }

    [Fact]
    public void NextCyclesThroughCodesThenUnknown()
    {
        var seen = new List<byte>();
        byte code = 0x1C;
        for (var i = 0; i < 7; i++)
        {
            code = BreachCodes.Next(code);
            seen.Add(code);
        }

        Assert.Equal(new byte[] { 0x55, 0x7A, 0xBD, 0xE9, 0xFF, BreachCodes.Unknown, 0x1C }, seen);
    }
}
