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
    public void SimpleTurnHasNoOverlap()
    {
        Assert.Equal(0, RouteMetrics.CountOverlaps([new(0, 0), new(0, 3), new(2, 3)]));
    }
}
