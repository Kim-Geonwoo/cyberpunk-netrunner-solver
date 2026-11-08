namespace NetrunnerSolver.Core.Solving;

public sealed record SolveResult(
    IReadOnlyList<Cell> Path,
    IReadOnlyList<byte> Buffer,
    IReadOnlyList<bool> Completed,
    bool AllCompleted,
    bool TimedOut)
{
    public int CompletedCount => Completed.Count(c => c);
}
