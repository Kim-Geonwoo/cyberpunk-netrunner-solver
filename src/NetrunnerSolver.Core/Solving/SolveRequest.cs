namespace NetrunnerSolver.Core.Solving;

public sealed record SolveRequest(
    CodeMatrix Matrix,
    IReadOnlyList<IReadOnlyList<byte>> Sequences,
    int BufferSize,
    IReadOnlyList<int> Priority,
    TimeSpan TimeLimit)
{
    public const int MinBufferSize = 1;
    public const int MaxBufferSize = 12;
    public const int MaxSequences = 6;

    public CodeMatrix Matrix { get; } = Matrix ?? throw new ArgumentNullException(nameof(Matrix));

    public IReadOnlyList<IReadOnlyList<byte>> Sequences { get; } = ValidateSequences(Sequences);

    public int BufferSize { get; } = BufferSize is >= MinBufferSize and <= MaxBufferSize
        ? BufferSize
        : throw new ArgumentOutOfRangeException(nameof(BufferSize), BufferSize, $"Buffer size must be {MinBufferSize}..{MaxBufferSize}.");

    public IReadOnlyList<int> Priority { get; } = ValidatePriority(Priority, Sequences?.Count ?? 0);

    private static IReadOnlyList<IReadOnlyList<byte>> ValidateSequences(IReadOnlyList<IReadOnlyList<byte>> sequences)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        if (sequences.Count > MaxSequences)
        {
            throw new ArgumentException($"At most {MaxSequences} sequences are supported, got {sequences.Count}.", nameof(sequences));
        }

        if (sequences.Any(s => s is null))
        {
            throw new ArgumentException("Sequences must not contain null.", nameof(sequences));
        }

        return sequences;
    }

    private static IReadOnlyList<int> ValidatePriority(IReadOnlyList<int> priority, int count)
    {
        ArgumentNullException.ThrowIfNull(priority);
        if (priority.Count != count || priority.Any(i => i < 0 || i >= count) || priority.Distinct().Count() != count)
        {
            throw new ArgumentException("Priority must be a permutation of the sequence indices.", nameof(priority));
        }

        return priority;
    }
}
