namespace NetrunnerSolver.Core.Solving;

internal static class PatternBuilder
{
    public const int Any = -1;

    // 한 길이에서 만드는 패턴 수의 상한. 시퀀스 6개(각 1~3코드)·버퍼 12의 실제 조합은 수만 개 안이고,
    // 이를 넘는 경우(서로 다른 한 코드 시퀀스 6개, 길이 12에서 33만 개)는 1초 제한 안에 이만큼도 다 풀지 못한다.
    // 상한이 없으면 그 경우 패턴과 중복 검사 문자열만으로 100MB를 넘게 잡는다.
    public const int MaxPatterns = 100_000;

    public static IReadOnlyList<int[]> Build(IReadOnlyList<IReadOnlyList<byte>> sequences, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        var patterns = new List<int[]>();
        for (var length = 1; length <= maxLength; length++)
        {
            patterns.AddRange(BuildExact(sequences, length, Deadline.None));
        }

        return patterns;
    }

    public static IReadOnlyList<int[]> BuildExact(IReadOnlyList<IReadOnlyList<byte>> sequences, int length, Deadline deadline)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        ArgumentNullException.ThrowIfNull(deadline);
        if (sequences.Count == 0 || length <= 0 || sequences.Any(s => s.Count > length))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var found = new List<int[]>();
        foreach (var order in Permutations(sequences.Count))
        {
            if (deadline.Expired || found.Count >= MaxPatterns)
            {
                break;
            }

            var buffer = new int[length];
            Array.Fill(buffer, Any);
            Place(sequences, order, 0, 0, buffer, 0, length, seen, found, deadline);
        }

        return found;
    }

    private static void Place(
        IReadOnlyList<IReadOnlyList<byte>> sequences,
        int[] order,
        int depth,
        int minStart,
        int[] buffer,
        int usedLength,
        int length,
        HashSet<string> seen,
        List<int[]> found,
        Deadline deadline)
    {
        if (found.Count >= MaxPatterns)
        {
            return;
        }

        if (depth == order.Length)
        {
            if (usedLength == length && seen.Add(string.Join(',', buffer)))
            {
                found.Add((int[])buffer.Clone());
            }

            return;
        }

        if (deadline.Expired)
        {
            return;
        }

        var sequence = sequences[order[depth]];
        if (sequence.Count == 0)
        {
            Place(sequences, order, depth + 1, minStart, buffer, usedLength, length, seen, found, deadline);
            return;
        }

        for (var start = minStart; start + sequence.Count <= length; start++)
        {
            if (!Fits(buffer, sequence, start))
            {
                continue;
            }

            var next = (int[])buffer.Clone();
            for (var i = 0; i < sequence.Count; i++)
            {
                next[start + i] = sequence[i];
            }

            Place(sequences, order, depth + 1, start, next, Math.Max(usedLength, start + sequence.Count), length, seen, found, deadline);
        }
    }

    private static bool Fits(int[] buffer, IReadOnlyList<byte> sequence, int start)
    {
        for (var i = 0; i < sequence.Count; i++)
        {
            var current = buffer[start + i];
            if (current != Any && current != sequence[i])
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<int[]> Permutations(int count)
    {
        var items = Enumerable.Range(0, count).ToArray();
        return Permute(items, 0);
    }

    private static IEnumerable<int[]> Permute(int[] items, int k)
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
}
