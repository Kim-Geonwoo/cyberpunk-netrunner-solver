namespace NetrunnerSolver.Core.Solving;

public static class RouteMetrics
{
    public static int CountOverlaps(IReadOnlyList<Cell> path)
    {
        ArgumentNullException.ThrowIfNull(path);
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
