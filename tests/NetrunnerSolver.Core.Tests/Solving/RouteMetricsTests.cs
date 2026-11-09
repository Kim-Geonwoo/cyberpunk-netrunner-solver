using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Tests.Solving;

public class RouteMetricsTests
{
    [Fact]
    public void DistanceSumsStraightSegments()
    {
        Assert.Equal(5, RouteMetrics.Distance([new(0, 0), new(0, 3), new(2, 3)]), 6);
    }

    [Fact]
    public void CountsNonAdjacentSegmentsThatTouch()
    {
        Assert.Equal(1, RouteMetrics.CountOverlaps([new(0, 0), new(0, 3), new(2, 3), new(2, 1), new(0, 1)]));
    }

    [Fact]
    public void CountingWithoutCellSetsMatchesCellSets()
    {
        // 가로·세로 경로(풀이가 내는 모양)와 대각선이 섞인 임의 경로 모두에서 칸 집합으로 센 값과 같아야 한다.
        var random = new Random(8);
        for (var trial = 0; trial < 3000; trial++)
        {
            var path = new List<Cell> { new(random.Next(8), random.Next(8)) };
            var straight = trial % 3 != 0;
            for (var step = random.Next(0, 12); step > 0; step--)
            {
                var last = path[^1];
                path.Add(!straight ? new Cell(random.Next(8), random.Next(8))
                    : path.Count % 2 == 1 ? new Cell(random.Next(8), last.Col)
                    : new Cell(last.Row, random.Next(8)));
            }

            Assert.Equal(ReferenceOverlaps(path), RouteMetrics.CountOverlaps(path));
        }
    }

    private static int ReferenceOverlaps(List<Cell> path)
    {
        var segments = new List<HashSet<Cell>>();
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var (from, to) = (path[i], path[i + 1]);
            var steps = Math.Max(Math.Abs(to.Row - from.Row), Math.Abs(to.Col - from.Col));
            var points = new HashSet<Cell>();
            for (var s = 0; s <= steps; s++)
            {
                points.Add(new Cell(from.Row + (Math.Sign(to.Row - from.Row) * s), from.Col + (Math.Sign(to.Col - from.Col) * s)));
            }

            segments.Add(points);
        }

        var overlaps = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            for (var j = i + 2; j < segments.Count; j++)
            {
                overlaps += segments[i].Overlaps(segments[j]) ? 1 : 0;
            }
        }

        return overlaps;
    }

    [Fact]
    public void SimpleTurnHasNoOverlap()
    {
        Assert.Equal(0, RouteMetrics.CountOverlaps([new(0, 0), new(0, 3), new(2, 3)]));
    }
}
