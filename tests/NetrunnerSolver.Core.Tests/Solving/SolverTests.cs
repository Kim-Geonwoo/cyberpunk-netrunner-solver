using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Tests.Solving;

public class SolverTests
{
    private static readonly CodeMatrix Web = new(new byte[,]
    {
        { 0x55, 0x1C, 0xBD, 0x1C, 0x1C },
        { 0x55, 0x55, 0xBD, 0x1C, 0xE9 },
        { 0xE9, 0x1C, 0x55, 0x1C, 0xBD },
        { 0x55, 0x1C, 0x1C, 0xBD, 0x55 },
        { 0xE9, 0x55, 0x1C, 0xE9, 0xE9 },
    });

    private static readonly byte[][] WebSequences = [[0xE9, 0x1C], [0xE9, 0x55], [0xE9, 0x55, 0x1C]];

    private static readonly CodeMatrix Small = new(new byte[,]
    {
        { 0x1C, 0x55, 0xBD },
        { 0xE9, 0x1C, 0x55 },
        { 0x55, 0xBD, 0x1C },
    });

    private static SolveRequest Request(CodeMatrix matrix, byte[][] sequences, int buffer, int[]? priority = null, TimeSpan? limit = null) =>
        new(matrix, sequences, buffer, priority ?? [.. Enumerable.Range(0, sequences.Length)], limit ?? TimeSpan.FromSeconds(5));

    [Fact]
    public void DefaultPriorityPrefersTopSequence()
    {
        var result = Solver.Solve(Request(Web, WebSequences, 4));

        Assert.Equal([true, false, false], result.Completed);
        Assert.False(result.AllCompleted);
        Assert.Equal(3, result.Path.Count);
    }

    [Fact]
    public void CustomPriorityCompletesLowerTwo()
    {
        var result = Solver.Solve(Request(Web, WebSequences, 4, [1, 2, 0]));

        Assert.Equal([false, true, true], result.Completed);
        Assert.Equal(4, result.Path.Count);
        Assert.Equal(new byte[] { 0xE9, 0x55, 0x1C }, result.Buffer.Skip(1));
    }

    [Fact]
    public void AllSolvableWhenEverySequenceFits()
    {
        var result = Solver.Solve(Request(Small, [[0x55, 0x1C], [0x1C, 0x55]], 3));

        Assert.True(result.AllCompleted);
        Assert.Equal(3, result.Path.Count);
    }

    [Fact]
    public void SequenceLongerThanBufferFailsOnlyItself()
    {
        var result = Solver.Solve(Request(Small, [[0x55, 0x1C], [0x55, 0x1C, 0x55, 0x1C]], 3));

        Assert.Equal([true, false], result.Completed);
    }

    [Fact]
    public void SequenceWithUnknownCodeNeverCompletes()
    {
        var matrix = Small.With(new Cell(2, 0), BreachCodes.Unknown);

        var result = Solver.Solve(Request(matrix, [[0x55, 0x1C], [BreachCodes.Unknown, 0x1C]], 4));

        Assert.Equal([true, false], result.Completed);
    }

    [Fact]
    public void NoSequencesGivesEmptyResult()
    {
        var result = Solver.Solve(Request(Small, [], 4));

        Assert.Empty(result.Path);
        Assert.Empty(result.Completed);
        Assert.False(result.AllCompleted);
    }

    [Fact]
    public void AllUnknownMatrixGivesEmptyPath()
    {
        var result = Solver.Solve(Request(new CodeMatrix(new byte[3, 3]), [[0x55, 0x1C]], 4));

        Assert.Empty(result.Path);
        Assert.Equal([false], result.Completed);
    }

    [Fact]
    public void DuplicateSequencesCompleteTogether()
    {
        var result = Solver.Solve(Request(Small, [[0x55, 0x1C], [0x55, 0x1C]], 4));

        Assert.Equal([true, true], result.Completed);
        Assert.Equal(2, result.Path.Count);
    }

    [Fact]
    public void ZeroTimeLimitReturnsTimedOut()
    {
        var result = Solver.Solve(Request(Web, WebSequences, 4, limit: TimeSpan.Zero));

        Assert.True(result.TimedOut);
    }

    [Fact]
    public void CancelledTokenThrows()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => Solver.Solve(Request(Web, WebSequences, 4), cts.Token));
    }

    [Fact]
    public void InvalidRequestsAreRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => Request(Small, [[0x55]], 4, [1]));
        Assert.ThrowsAny<ArgumentException>(() => Request(Small, [[0x55]], 0));
        Assert.ThrowsAny<ArgumentException>(() => Request(Small, [[0x55]], 13));
        Assert.ThrowsAny<ArgumentException>(() => Request(Small, [.. Enumerable.Repeat(new byte[] { 0x55 }, 7)], 4));
    }
}
