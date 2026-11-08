namespace NetrunnerSolver.Core.Solving;

public static class Solver
{
    private const int MaxPathsPerPattern = 256;

    public static SolveResult Solve(SolveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var sequences = request.Sequences;
        var count = sequences.Count;
        if (count == 0)
        {
            return Empty(0, timedOut: false);
        }

        var deadline = new Deadline(request.TimeLimit, cancellationToken);
        var available = CountCodes(request.Matrix);
        var singles = new Cell[]?[count];
        var timedOut = false;
        foreach (var s in request.Priority)
        {
            if (!IsUsable(sequences[s], request.BufferSize) || !FitsCounts(sequences[s], available))
            {
                continue;
            }

            singles[s] = Search(request, [sequences[s]], deadline);
            if (deadline.Expired)
            {
                timedOut = true;
                break;
            }
        }

        if (!timedOut)
        {
            var bitOf = new int[count];
            for (var rank = 0; rank < count; rank++)
            {
                bitOf[request.Priority[rank]] = count - 1 - rank;
            }

            for (var mask = (1 << count) - 1; mask > 0; mask--)
            {
                var subset = Enumerable.Range(0, count).Where(s => (mask & (1 << bitOf[s])) != 0).ToArray();
                if (subset.Any(s => singles[s] is null))
                {
                    continue;
                }

                if (subset.Length == 1)
                {
                    return Build(request, singles[subset[0]]!, timedOut: false);
                }

                var path = Search(request, [.. subset.Select(s => sequences[s])], deadline);
                var expired = deadline.Expired;
                if (path is not null)
                {
                    return Build(request, path, expired);
                }

                if (expired)
                {
                    timedOut = true;
                    break;
                }
            }
        }

        foreach (var s in request.Priority)
        {
            if (singles[s] is { } best)
            {
                return Build(request, best, timedOut);
            }
        }

        return Empty(count, timedOut);
    }

    private static Cell[]? Search(SolveRequest request, IReadOnlyList<IReadOnlyList<byte>> subset, Deadline deadline)
    {
        var reduced = Reduce(subset);
        var shortest = reduced.Max(s => s.Count);
        for (var length = shortest; length <= request.BufferSize; length++)
        {
            Cell[]? best = null;
            (int Overlaps, double Distance) bestKey = default;
            foreach (var pattern in PatternBuilder.BuildExact(reduced, length, deadline))
            {
                var found = 0;
                foreach (var path in PathSearch.Find(request.Matrix, pattern, deadline))
                {
                    var key = (RouteMetrics.CountOverlaps(path), RouteMetrics.Distance(path));
                    if (best is null || key.CompareTo(bestKey) < 0)
                    {
                        best = path;
                        bestKey = key;
                    }

                    if (++found >= MaxPathsPerPattern)
                    {
                        break;
                    }
                }

                if (deadline.Expired)
                {
                    return best;
                }
            }

            if (best is not null)
            {
                return best;
            }
        }

        return null;
    }

    private static List<IReadOnlyList<byte>> Reduce(IReadOnlyList<IReadOnlyList<byte>> subset)
    {
        var distinct = subset
            .GroupBy(s => Convert.ToHexString([.. s]), StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
        return [.. distinct.Where(s => !distinct.Any(o => !ReferenceEquals(o, s) && o.Count > s.Count && Contains([.. o], s)))];
    }

    private static Dictionary<byte, int> CountCodes(CodeMatrix matrix)
    {
        var counts = new Dictionary<byte, int>();
        for (var r = 0; r < matrix.Size; r++)
        {
            for (var c = 0; c < matrix.Size; c++)
            {
                counts[matrix[r, c]] = counts.GetValueOrDefault(matrix[r, c]) + 1;
            }
        }

        return counts;
    }

    private static bool FitsCounts(IReadOnlyList<byte> sequence, Dictionary<byte, int> available) =>
        sequence.GroupBy(c => c).All(g => g.Count() <= available.GetValueOrDefault(g.Key));

    private static bool IsUsable(IReadOnlyList<byte> sequence, int bufferSize) =>
        sequence.Count > 0 && sequence.Count <= bufferSize && !sequence.Contains(BreachCodes.Unknown);

    private static SolveResult Build(SolveRequest request, Cell[] path, bool timedOut)
    {
        var buffer = path.Select(c => request.Matrix[c]).ToArray();
        var completed = request.Sequences.Select(s => IsUsable(s, request.BufferSize) && Contains(buffer, s)).ToArray();
        return new SolveResult(path, buffer, completed, completed.All(c => c), timedOut);
    }

    private static SolveResult Empty(int count, bool timedOut) =>
        new([], [], new bool[count], false, timedOut);

    private static bool Contains(byte[] buffer, IReadOnlyList<byte> sequence)
    {
        for (var start = 0; start + sequence.Count <= buffer.Length; start++)
        {
            var match = true;
            for (var i = 0; i < sequence.Count && match; i++)
            {
                match = buffer[start + i] == sequence[i];
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}
