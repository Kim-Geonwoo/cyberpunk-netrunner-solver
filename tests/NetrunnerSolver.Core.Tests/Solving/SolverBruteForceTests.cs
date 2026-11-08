using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Tests.Solving;

public class SolverBruteForceTests
{
    [Fact]
    public void MatchesExhaustiveSearchOn500RandomPuzzles()
    {
        var random = new Random(20261107);
        var alphabet = BreachCodes.All.Take(4).ToArray();
        for (var round = 0; round < 500; round++)
        {
            var size = random.Next(4, 6);
            var buffer = random.Next(3, 7);
            var count = random.Next(1, 4);
            var cells = new byte[size, size];
            for (var r = 0; r < size; r++)
            {
                for (var c = 0; c < size; c++)
                {
                    cells[r, c] = alphabet[random.Next(alphabet.Length)];
                }
            }

            var matrix = new CodeMatrix(cells);
            var sequences = Enumerable.Range(0, count)
                .Select(_ => Enumerable.Range(0, random.Next(2, 5)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray())
                .ToArray();
            var priority = Enumerable.Range(0, count).OrderBy(_ => random.Next()).ToArray();

            var result = Solver.Solve(new SolveRequest(matrix, sequences, buffer, priority, TimeSpan.FromSeconds(10)));

            var label = $"round {round}";
            Assert.False(result.TimedOut, label);
            Assert.True(result.Path.Count <= buffer, label);
            Assert.True(BruteForce.IsLegal(result.Path, size), label);
            Assert.Equal(result.Path.Select(c => matrix[c]), result.Buffer);
            Assert.Equal(BruteForce.Completed(result.Buffer, sequences), result.Completed);
            Assert.Equal(BruteForce.BestScore(matrix, sequences, buffer, priority), BruteForce.Score(result.Completed, priority));
        }
    }
}

internal static class BruteForce
{
    public static int Score(IReadOnlyList<bool> completed, IReadOnlyList<int> priority)
    {
        var score = 0;
        for (var rank = 0; rank < priority.Count; rank++)
        {
            if (completed[priority[rank]])
            {
                score += 1 << (priority.Count - 1 - rank);
            }
        }

        return score;
    }

    public static bool[] Completed(IReadOnlyList<byte> buffer, IReadOnlyList<byte[]> sequences) =>
        [.. sequences.Select(s => Contains(buffer, s))];

    public static bool IsLegal(IReadOnlyList<Cell> path, int size)
    {
        var seen = new HashSet<Cell>();
        for (var k = 0; k < path.Count; k++)
        {
            var cell = path[k];
            if (cell.Row < 0 || cell.Col < 0 || cell.Row >= size || cell.Col >= size || !seen.Add(cell))
            {
                return false;
            }

            var ok = k == 0 ? cell.Row == 0 : k % 2 == 1 ? cell.Col == path[k - 1].Col : cell.Row == path[k - 1].Row;
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    public static int BestScore(CodeMatrix matrix, IReadOnlyList<byte[]> sequences, int buffer, IReadOnlyList<int> priority)
    {
        var best = 0;
        var path = new List<Cell>();

        void Walk()
        {
            if (path.Count > 0)
            {
                best = Math.Max(best, Score(Completed([.. path.Select(c => matrix[c])], sequences), priority));
            }

            if (path.Count == buffer)
            {
                return;
            }

            for (var i = 0; i < matrix.Size; i++)
            {
                var cell = path.Count == 0
                    ? new Cell(0, i)
                    : path.Count % 2 == 1 ? new Cell(i, path[^1].Col) : new Cell(path[^1].Row, i);
                if (path.Contains(cell))
                {
                    continue;
                }

                path.Add(cell);
                Walk();
                path.RemoveAt(path.Count - 1);
            }
        }

        Walk();
        return best;
    }

    private static bool Contains(IReadOnlyList<byte> buffer, byte[] sequence)
    {
        for (var start = 0; start + sequence.Length <= buffer.Count; start++)
        {
            var match = true;
            for (var i = 0; i < sequence.Length && match; i++)
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
