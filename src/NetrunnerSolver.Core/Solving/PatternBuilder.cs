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

    // 시퀀스를 놓는 순서(순열)마다, 앞 시퀀스보다 뒤에서 시작하도록 놓아 보며 길이가 꼭 맞는 버퍼를 모은다.
    // 처음 나온 순서대로, 같은 패턴은 한 번만 낸다.
    public static IReadOnlyList<int[]> BuildExact(IReadOnlyList<IReadOnlyList<byte>> sequences, int length, Deadline deadline)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        ArgumentNullException.ThrowIfNull(deadline);
        if (sequences.Count == 0 || length <= 0 || sequences.Any(s => s.Count > length))
        {
            return [];
        }

        var builder = new Builder([.. sequences.Select(s => s.Select(code => (int)code).ToArray())], length, deadline);
        builder.Permute(0);
        return builder.Found;
    }

    // 순열과 배치를 같은 버퍼·배열 위에서 바꿨다 되돌리며(백트래킹) 찾는다. 단계마다 복제하지 않고 새 패턴일 때만 복사한다.
    private sealed class Builder(int[][] sequences, int length, Deadline deadline)
    {
        private readonly int[] _order = [.. Enumerable.Range(0, sequences.Length)];
        private readonly int[] _buffer = new int[length];
        private readonly int[] _saved = new int[sequences.Length * length];
        private readonly HashSet<int[]> _seen = new(IntSequenceComparer.Instance);
        private bool _stopped;

        public List<int[]> Found { get; } = [];

        // 첫 자리부터 차례로 맞바꾸는 순열 순서. 순열마다 시작 전에 제한과 상한을 본다.
        public void Permute(int k)
        {
            if (k == _order.Length)
            {
                if (deadline.Expired || Found.Count >= MaxPatterns)
                {
                    _stopped = true;
                    return;
                }

                Array.Fill(_buffer, Any);
                Place(0, 0, 0);
                return;
            }

            for (var i = k; i < _order.Length && !_stopped; i++)
            {
                (_order[k], _order[i]) = (_order[i], _order[k]);
                Permute(k + 1);
                (_order[k], _order[i]) = (_order[i], _order[k]);
            }
        }

        private void Place(int depth, int minStart, int usedLength)
        {
            if (Found.Count >= MaxPatterns)
            {
                return;
            }

            if (depth == _order.Length)
            {
                if (usedLength == length && !_seen.Contains(_buffer))
                {
                    var pattern = (int[])_buffer.Clone();
                    _seen.Add(pattern);
                    Found.Add(pattern);
                }

                return;
            }

            if (deadline.Expired)
            {
                return;
            }

            var sequence = sequences[_order[depth]];
            if (sequence.Length == 0)
            {
                Place(depth + 1, minStart, usedLength);
                return;
            }

            var save = _saved.AsSpan(depth * length, sequence.Length);
            for (var start = minStart; start + sequence.Length <= length; start++)
            {
                var slot = _buffer.AsSpan(start, sequence.Length);
                if (!Fits(slot, sequence))
                {
                    continue;
                }

                slot.CopyTo(save);
                sequence.CopyTo(slot);
                Place(depth + 1, start, Math.Max(usedLength, start + sequence.Length));
                save.CopyTo(slot);
            }
        }

        private static bool Fits(Span<int> slot, int[] sequence)
        {
            for (var i = 0; i < sequence.Length; i++)
            {
                if (slot[i] != Any && slot[i] != sequence[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
