using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Tests.Solving;

public class PathSearchTests
{
    private const int Any = PatternBuilder.Any;

    private static CodeMatrix Sample() => new(new byte[,]
    {
        { 0x1C, 0x55, 0xBD },
        { 0xE9, 0x1C, 0x55 },
        { 0x55, 0xBD, 0x1C },
    });

    private static string Key(IEnumerable<Cell> path) => string.Join(" ", path.Select(c => $"{c.Row},{c.Col}"));

    [Fact]
    public void FindsTheOnlyPathForTwoCodes()
    {
        var paths = PathSearch.Find(Sample(), [0x55, 0x1C]).Select(Key).ToList();

        Assert.Equal(["0,1 1,1"], paths);
    }

    [Fact]
    public void WildcardStartExploresEveryTopRowCell()
    {
        var paths = PathSearch.Find(Sample(), [Any, 0x1C, 0x55]).Select(Key).ToHashSet();

        Assert.Equal(new HashSet<string> { "0,1 1,1 1,2", "0,2 2,2 2,0" }, paths);
    }

    [Fact]
    public void UnknownCellMatchesOnlyWildcards()
    {
        var matrix = Sample().With(new Cell(1, 1), BreachCodes.Unknown);

        Assert.Empty(PathSearch.Find(matrix, [0x55, 0x1C]));
        Assert.Contains("0,1 1,1", PathSearch.Find(matrix, [0x55, Any]).Select(Key));
    }

    [Fact]
    public void CodeMatrixRejectsNonSquareInput()
    {
        Assert.Throws<ArgumentException>(() => new CodeMatrix(new byte[2, 3]));
        Assert.Throws<ArgumentException>(() => new CodeMatrix(new byte[0, 0]));
    }
}
