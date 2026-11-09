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

        // 칸 코드와 사용 표시를 1차원 배열(행×크기+열)로 두어 2차원 배열 접근을 피한다. 탐색 순서는 그대로다.
        var codes = new byte[size * size];
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                codes[(r * size) + c] = matrix[r, c];
            }
        }

        var used = new bool[size * size];
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
                    used[(path[depth].Row * size) + path[depth].Col] = false;
                }

                continue;
            }

            var index = next[depth]++;
            var cell = depth == 0
                ? new Cell(0, index)
                : depth % 2 == 1
                    ? new Cell(index, path[depth - 1].Col)
                    : new Cell(path[depth - 1].Row, index);

            var at = (cell.Row * size) + cell.Col;
            if (used[at] || !Matches(codes[at], pattern[depth]))
            {
                continue;
            }

            path[depth] = cell;
            if (depth == length - 1)
            {
                yield return (Cell[])path.Clone();
                continue;
            }

            used[at] = true;
            depth++;
            next[depth] = 0;
        }
    }

    private static bool Matches(byte code, int wanted) =>
        wanted == PatternBuilder.Any || (code != BreachCodes.Unknown && code == wanted);
}
