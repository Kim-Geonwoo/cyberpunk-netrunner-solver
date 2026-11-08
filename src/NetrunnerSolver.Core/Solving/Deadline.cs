using System.Diagnostics;

namespace NetrunnerSolver.Core.Solving;

internal sealed class Deadline(TimeSpan limit, CancellationToken cancellationToken)
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public static Deadline None => new(TimeSpan.MaxValue, CancellationToken.None);

    public bool Expired
    {
        get
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _clock.Elapsed >= limit;
        }
    }
}
