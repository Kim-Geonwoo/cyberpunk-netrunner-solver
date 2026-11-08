namespace NetrunnerSolver.Core.Solving;

internal static class PathSearch
{
    private const int CheckEvery = 2048;

    public static IEnumerable<Cell[]> Find(CodeMatrix matrix, int[] pattern) => Find(matrix, pattern, Deadline.None);

    public static IEnumerable<Cell[]> Find(CodeMatrix matrix, int[] pattern, Deadline deadline)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(pattern);
        var size = matrix.Size;
        var length = pattern.Length;
        if (length == 0 || length > size * size)
        {
            yield break;
        }

        var used = new bool[size, size];
        var path = new Cell[length];
        var next = new int[length];
        var depth = 0;
        var steps = 0;
        while (depth >= 0)
        {
            if (++steps % CheckEvery == 0 && deadline.Expired)
            {
                yield break;
            }

            if (next[depth] >= size)
            {
                depth--;
                if (depth >= 0)
                {
                    used[path[depth].Row, path[depth].Col] = false;
                }

                continue;
            }

            var index = next[depth]++;
            var cell = depth == 0
                ? new Cell(0, index)
                : depth % 2 == 1
                    ? new Cell(index, path[depth - 1].Col)
                    : new Cell(path[depth - 1].Row, index);

            if (used[cell.Row, cell.Col] || !Matches(matrix[cell], pattern[depth]))
            {
                continue;
            }

            path[depth] = cell;
            if (depth == length - 1)
            {
                yield return (Cell[])path.Clone();
                continue;
            }

            used[cell.Row, cell.Col] = true;
            depth++;
            next[depth] = 0;
        }
    }

    private static bool Matches(byte code, int wanted) =>
        wanted == PatternBuilder.Any || (code != BreachCodes.Unknown && code == wanted);
}
