using System.Diagnostics;
using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Tests.Solving;

public class SolverLimitsTests
{
    private static CodeMatrix RandomMatrix(int size, int seed, Action<byte[,]>? edit = null)
    {
        var random = new Random(seed);
        byte[] alphabet = [0x1C, 0x55, 0xBD, 0xE9];
        var cells = new byte[size, size];
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                cells[r, c] = alphabet[random.Next(alphabet.Length)];
            }
        }

        edit?.Invoke(cells);
        return new CodeMatrix(cells);
    }

    [Fact]
    public void ImpossibleTopSequenceWithLargeBufferStillSolvesTheRest()
    {
        var matrix = RandomMatrix(7, 11, cells => cells[3, 3] = 0xFF);
        var clock = Stopwatch.StartNew();

        var result = Solver.Solve(new SolveRequest(matrix, [[0xFF, 0xFF], [0x1C, 0x55]], 12, [0, 1], TimeSpan.FromSeconds(1)));

        Assert.False(result.TimedOut);
        Assert.Equal([false, true], result.Completed);
        Assert.True(clock.ElapsedMilliseconds < 500, $"took {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void ManyIdenticalSequencesWithLargeBufferFinishFast()
    {
        var matrix = RandomMatrix(7, 12);
        byte[][] sequences = [.. Enumerable.Repeat(new byte[] { 0x1C, 0x1C }, 6)];
        var clock = Stopwatch.StartNew();

        var result = Solver.Solve(new SolveRequest(matrix, sequences, 12, [0, 1, 2, 3, 4, 5], TimeSpan.FromSeconds(1)));

        Assert.False(result.TimedOut);
        Assert.True(result.AllCompleted);
        Assert.True(clock.ElapsedMilliseconds < 500, $"took {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void HugeSearchRespectsTheTimeLimitAndKeepsABestAnswer()
    {
        var matrix = RandomMatrix(8, 13);
        var random = new Random(14);
        byte[] alphabet = [0x1C, 0x55, 0xBD, 0xE9];
        byte[][] sequences = [.. Enumerable.Range(0, 6).Select(_ => Enumerable.Range(0, 4).Select(_ => alphabet[random.Next(4)]).ToArray())];
        var clock = Stopwatch.StartNew();

        var result = Solver.Solve(new SolveRequest(matrix, sequences, 12, [0, 1, 2, 3, 4, 5], TimeSpan.FromMilliseconds(300)));

        Assert.True(clock.ElapsedMilliseconds < 800, $"took {clock.ElapsedMilliseconds} ms");
        Assert.NotEmpty(result.Path);
        Assert.Contains(true, result.Completed);
    }
}
