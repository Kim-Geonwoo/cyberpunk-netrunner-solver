namespace NetrunnerSolver.Core.Solving;

public static class RouteMetrics
{
    public static int CountOverlaps(IReadOnlyList<Cell> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!IsAxisAligned(path))
        {
            return CountOverlapsByPoints(path);
        }

        // 풀이 경로의 선분은 모두 가로나 세로다. 그런 선분이 지나는 칸은 [행 구간]×[열 구간] 직사각형이므로
        // 두 선분이 칸을 공유하는 것은 행 구간과 열 구간이 함께 겹치는 것과 같다. 칸 집합을 만들지 않고 센다.
        var overlaps = 0;
        for (var i = 0; i + 1 < path.Count; i++)
        {
            for (var j = i + 2; j + 1 < path.Count; j++)
            {
                if (Overlap(path[i].Row, path[i + 1].Row, path[j].Row, path[j + 1].Row)
                    && Overlap(path[i].Col, path[i + 1].Col, path[j].Col, path[j + 1].Col))
                {
                    overlaps++;
                }
            }
        }

        return overlaps;
    }

    public static double Distance(IReadOnlyList<Cell> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var total = 0.0;
        for (var i = 0; i + 1 < path.Count; i++)
        {
            total += Math.Sqrt(Math.Pow(path[i + 1].Row - path[i].Row, 2) + Math.Pow(path[i + 1].Col - path[i].Col, 2));
        }

        return total;
    }

    private static bool IsAxisAligned(IReadOnlyList<Cell> path)
    {
        for (var i = 0; i + 1 < path.Count; i++)
        {
            if (path[i].Row != path[i + 1].Row && path[i].Col != path[i + 1].Col)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Overlap(int a0, int a1, int b0, int b1) =>
        Math.Max(Math.Min(a0, a1), Math.Min(b0, b1)) <= Math.Min(Math.Max(a0, a1), Math.Max(b0, b1));

    private static int CountOverlapsByPoints(IReadOnlyList<Cell> path)
    {
        var segments = new List<HashSet<Cell>>();
        for (var i = 0; i + 1 < path.Count; i++)
        {
            segments.Add(Points(path[i], path[i + 1]));
        }

        var overlaps = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            for (var j = i + 2; j < segments.Count; j++)
            {
                if (segments[i].Overlaps(segments[j]))
                {
                    overlaps++;
                }
            }
        }

        return overlaps;
    }

    private static HashSet<Cell> Points(Cell from, Cell to)
    {
        var points = new HashSet<Cell>();
        var steps = Math.Max(Math.Abs(to.Row - from.Row), Math.Abs(to.Col - from.Col));
        var dr = Math.Sign(to.Row - from.Row);
        var dc = Math.Sign(to.Col - from.Col);
        for (var s = 0; s <= steps; s++)
        {
            points.Add(new Cell(from.Row + (dr * s), from.Col + (dc * s)));
        }

        return points;
    }
}
