using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Tests.Solving;

public class PatternBuilderTests
{
    private const int Any = PatternBuilder.Any;

    private static IReadOnlyList<IReadOnlyList<byte>> Seqs(params byte[][] sequences) => sequences;

    [Fact]
    public void SingleSequenceGetsLeadingWildcardsUpToMaxLength()
    {
        var patterns = PatternBuilder.Build(Seqs([0x1C, 0x55]), 4);

        Assert.Equal(
            new[] { new[] { 0x1C, 0x55 }, new[] { Any, 0x1C, 0x55 }, new[] { Any, Any, 0x1C, 0x55 } },
            patterns);
    }

    [Fact]
    public void OverlappingSequencesMergeAndLongerPatternsAreDropped()
    {
        var patterns = PatternBuilder.Build(Seqs([0xE9, 0x55], [0x55, 0x1C]), 3);

        Assert.Contains(patterns, p => p.SequenceEqual(new[] { 0xE9, 0x55, 0x1C }));
        Assert.All(patterns, p => Assert.True(p.Length <= 3));
    }

    [Fact]
    public void ContainedSequenceUsesTheLongerOne()
    {
        var patterns = PatternBuilder.Build(Seqs([0xE9, 0x55, 0x1C], [0xE9, 0x55]), 3);

        Assert.Contains(patterns, p => p.SequenceEqual(new[] { 0xE9, 0x55, 0x1C }));
    }

    [Fact]
    public void IdenticalSequencesShareOnePattern()
    {
        var patterns = PatternBuilder.Build(Seqs([0x1C], [0x1C]), 1);

        Assert.Equal(new[] { new[] { 0x1C } }, patterns);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void EveryPatternContainsEverySequence(byte[][] sequences, int maxLength)
    {
        var patterns = PatternBuilder.Build(sequences, maxLength);

        Assert.NotEmpty(patterns);
        foreach (var pattern in patterns)
        {
            Assert.True(pattern.Length <= maxLength);
            foreach (var sequence in sequences)
            {
                Assert.True(ContainsExactly(pattern, sequence), $"{string.Join(",", pattern)} lacks {Convert.ToHexString(sequence)}");
            }
        }
    }

    public static TheoryData<byte[][], int> Inputs() => new()
    {
        { new[] { new byte[] { 0x1C, 0x55 } }, 4 },
        { new[] { new byte[] { 0xE9, 0x55 }, new byte[] { 0x55, 0x1C } }, 5 },
        { new[] { new byte[] { 0xE9, 0x55, 0x1C }, new byte[] { 0xE9, 0x55 }, new byte[] { 0x1C, 0xBD } }, 7 },
    };

    private static bool ContainsExactly(int[] pattern, byte[] sequence)
    {
        for (var start = 0; start + sequence.Length <= pattern.Length; start++)
        {
            var ok = true;
            for (var i = 0; i < sequence.Length && ok; i++)
            {
                ok = pattern[start + i] == sequence[i];
            }

            if (ok)
            {
                return true;
            }
        }

        return false;
    }
}
