using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Session;

public sealed class SolveSession
{
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(1);

    private readonly List<byte[]> _sequences;
    private readonly List<int> _priority;
    private CodeMatrix _matrix;

    public SolveSession(CodeMatrix matrix, IReadOnlyList<IReadOnlyList<byte>> sequences, int bufferSize)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        _sequences = [.. sequences.Take(SolveRequest.MaxSequences).Select(s => s.ToArray())];
        _priority = [.. Enumerable.Range(0, _sequences.Count)];
        BufferSize = Math.Clamp(bufferSize, SolveRequest.MinBufferSize, SolveRequest.MaxBufferSize);
        Result = Solve();
    }

    public event EventHandler? Changed;

    public CodeMatrix Matrix => _matrix;

    public IReadOnlyList<IReadOnlyList<byte>> Sequences => _sequences;

    public int BufferSize { get; private set; }

    public IReadOnlyList<int> Priority => _priority;

    public SolveResult Result { get; private set; }

    public void CycleMatrixCell(Cell cell)
    {
        if (cell.Row < 0 || cell.Col < 0 || cell.Row >= _matrix.Size || cell.Col >= _matrix.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the matrix.");
        }

        _matrix = _matrix.With(cell, BreachCodes.Next(_matrix[cell]));
        Update();
    }

    public void CycleSequenceCode(int sequence, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sequence, _sequences.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _sequences[sequence].Length);
        _sequences[sequence][index] = BreachCodes.Next(_sequences[sequence][index]);
        Update();
    }

    public void MovePriority(int sequence, int delta)
    {
        var position = _priority.IndexOf(sequence);
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Unknown sequence index.");
        }

        var target = Math.Clamp(position + delta, 0, _priority.Count - 1);
        if (target == position)
        {
            return;
        }

        _priority.RemoveAt(position);
        _priority.Insert(target, sequence);
        Update();
    }

    public void SetBufferSize(int size)
    {
        var clamped = Math.Clamp(size, SolveRequest.MinBufferSize, SolveRequest.MaxBufferSize);
        if (clamped == BufferSize)
        {
            return;
        }

        BufferSize = clamped;
        Update();
    }

    private void Update()
    {
        Result = Solve();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private SolveResult Solve() =>
        Solver.Solve(new SolveRequest(_matrix, [.. _sequences.Select(s => (IReadOnlyList<byte>)s.ToArray())], BufferSize, [.. _priority], TimeLimit));
}
