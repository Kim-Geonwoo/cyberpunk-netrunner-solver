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

    [Fact]
    public void BacktrackingGivesTheSamePatternsInTheSameOrderAsCopying()
    {
        // 단계마다 버퍼를 복제하고 문자열로 중복을 가리던 예전 방식과 패턴·순서가 같아야 풀이가 바뀌지 않는다.
        var random = new Random(31);
        byte[] alphabet = [0x1C, 0x55, 0xBD, 0xE9];
        for (var trial = 0; trial < 300; trial++)
        {
            var sequences = Enumerable.Range(0, random.Next(1, 6))
                .Select(_ => Enumerable.Range(0, random.Next(0, 4)).Select(_ => alphabet[random.Next(3)]).ToArray())
                .ToArray();
            var length = random.Next(1, 10);

            var actual = PatternBuilder.BuildExact(sequences, length, Deadline.None);

            Assert.Equal(ReferenceBuildExact(sequences, length), actual);
        }
    }

    private static List<int[]> ReferenceBuildExact(byte[][] sequences, int length)
    {
        var found = new List<int[]>();
        if (sequences.Any(s => s.Length > length))
        {
            return found;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Place(int[] order, int depth, int minStart, int[] buffer, int used)
        {
            if (depth == order.Length)
            {
                if (used == length && seen.Add(string.Join(',', buffer)))
                {
                    found.Add((int[])buffer.Clone());
                }

                return;
            }

            var sequence = sequences[order[depth]];
            if (sequence.Length == 0)
            {
                Place(order, depth + 1, minStart, buffer, used);
                return;
            }

            for (var start = minStart; start + sequence.Length <= length; start++)
            {
                if (Enumerable.Range(0, sequence.Length).Any(i => buffer[start + i] != Any && buffer[start + i] != sequence[i]))
                {
                    continue;
                }

                var next = (int[])buffer.Clone();
                for (var i = 0; i < sequence.Length; i++)
                {
                    next[start + i] = sequence[i];
                }

                Place(order, depth + 1, start, next, Math.Max(used, start + sequence.Length));
            }
        }

        IEnumerable<int[]> Permute(int[] items, int k)
        {
            if (k == items.Length)
            {
                yield return (int[])items.Clone();
                yield break;
            }

            for (var i = k; i < items.Length; i++)
            {
                (items[k], items[i]) = (items[i], items[k]);
                foreach (var permutation in Permute(items, k + 1))
                {
                    yield return permutation;
                }

                (items[k], items[i]) = (items[i], items[k]);
            }
        }

        foreach (var order in Permute([.. Enumerable.Range(0, sequences.Length)], 0))
        {
            Place(order, 0, 0, Enumerable.Repeat(Any, length).ToArray(), 0);
        }

        return found;
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
