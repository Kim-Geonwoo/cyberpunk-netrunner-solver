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

    [Fact]
    public void PatternCountStopsAtTheCap()
    {
        // 서로 다른 한 코드 시퀀스 6개는 길이 12에서 패턴이 33만 개를 넘는다.
        var singles = Seqs([0x1C], [0x55], [0x7A], [0xBD], [0xE9], [0xFF]);

        var patterns = PatternBuilder.BuildExact(singles, 12, Deadline.None);

        Assert.Equal(PatternBuilder.MaxPatterns, patterns.Count);
        Assert.Equal(patterns.Count, patterns.Select(p => string.Join(',', p)).Distinct().Count());
    }

    [Fact]
    public void RealisticSixSequencesStayWellUnderTheCap()
    {
        // 두 코드 시퀀스 6개, 같은 코드가 겹치는 최악에 가까운 조합도 모든 패턴을 다 만든다.
        var pairs = Seqs([0x1C, 0x1C], [0x1C, 0x55], [0x55, 0x1C], [0x55, 0x55], [0x1C, 0x7A], [0x7A, 0x1C]);

        for (var length = 1; length <= 12; length++)
        {
            Assert.InRange(PatternBuilder.BuildExact(pairs, length, Deadline.None).Count, 0, PatternBuilder.MaxPatterns / 4);
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
