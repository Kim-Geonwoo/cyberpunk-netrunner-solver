using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Session;

// 패널에서 고친 행렬·시퀀스·우선순위·버퍼로 다시 푼다.
// 한 번 푸는 데 최대 1초가 걸리므로 고친 스레드(UI) 밖에서 풀고, 결과는 고친 스레드의 컨텍스트로 넘겨
// 그곳에서 Result를 바꾸고 Changed를 알린다. 풀이가 끝나기 전에 또 고치면 앞 풀이는 취소하고 마지막 상태만 반영한다.
// 상태를 고치는 메서드는 한 스레드에서만 부른다.
public sealed class SolveSession : IDisposable
{
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private readonly List<byte[]> _sequences;
    private readonly List<int> _priority;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _pending;
    private CodeMatrix _matrix;
    private SolveResult _result;
    private int _version;
    private bool _disposed;

    private SolveSession(CodeMatrix matrix, List<byte[]> sequences, int bufferSize, SolveResult result)
    {
        _matrix = matrix;
        _sequences = sequences;
        _priority = [.. Enumerable.Range(0, sequences.Count)];
        BufferSize = bufferSize;
        _result = result;
    }

    public event EventHandler? Changed;

    public CodeMatrix Matrix => _matrix;

    public IReadOnlyList<IReadOnlyList<byte>> Sequences => _sequences;

    public int BufferSize { get; private set; }

    public IReadOnlyList<int> Priority => _priority;

    public SolveResult Result
    {
        get
        {
            lock (_gate)
            {
                return _result;
            }
        }
    }

    // 마지막으로 고친 상태의 풀이. 끝났으면 Result와 Changed가 이미 반영됐거나, 더 새 상태·Dispose로 취소된 것이다.
    public Task Solving { get; private set; } = Task.CompletedTask;

    // 첫 풀이도 부른 스레드 밖에서 하고, 다 풀린 세션을 돌려준다.
    public static async Task<SolveSession> StartAsync(CodeMatrix matrix, IReadOnlyList<IReadOnlyList<byte>> sequences, int bufferSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(sequences);
        var taken = sequences.Take(SolveRequest.MaxSequences).ToList();
        if (taken.Any(s => s is null))
        {
            throw new ArgumentException("Sequences must not contain null.", nameof(sequences));
        }

        List<byte[]> copies = [.. taken.Select(s => s.ToArray())];
        var clamped = Math.Clamp(bufferSize, SolveRequest.MinBufferSize, SolveRequest.MaxBufferSize);
        var request = Request(matrix, copies, clamped, [.. Enumerable.Range(0, copies.Count)]);
        var result = await SolveOffThreadAsync(request, cancellationToken).ConfigureAwait(false);
        return new SolveSession(matrix, copies, clamped, result);
    }

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

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        // _disposed 뒤로는 Update가 _pending을 바꾸지 않는다.
        _lifetime.Cancel();
        _pending?.Dispose();
        _lifetime.Dispose();
    }

    private void Update()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pending?.Cancel();
            _pending?.Dispose();
            _pending = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            var request = Request(_matrix, _sequences, BufferSize, _priority);
            Solving = ResolveAsync(request, ++_version, SynchronizationContext.Current, _pending.Token);
        }
    }

    private async Task ResolveAsync(SolveRequest request, int version, SynchronizationContext? context, CancellationToken cancellationToken)
    {
        SolveResult result;
        try
        {
            result = await SolveOffThreadAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (context is null)
        {
            Deliver(result, version);
            return;
        }

        // 고친 스레드(UI)에서 Result를 바꾸고 Changed를 알린다. 처리기에서 난 예외도 그 스레드의 처리기로 간다.
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(
            _ =>
            {
                try
                {
                    Deliver(result, version);
                }
                finally
                {
                    delivered.SetResult();
                }
            },
            null);
        await delivered.Task.ConfigureAwait(false);
    }

    private void Deliver(SolveResult result, int version)
    {
        lock (_gate)
        {
            if (_disposed || version != _version)
            {
                return;
            }

            _result = result;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static Task<SolveResult> SolveOffThreadAsync(SolveRequest request, CancellationToken cancellationToken) =>
        Task.Run(() => Solver.Solve(request, cancellationToken), cancellationToken);

    private static SolveRequest Request(CodeMatrix matrix, List<byte[]> sequences, int bufferSize, IReadOnlyList<int> priority) =>
        new(matrix, [.. sequences.Select(s => (IReadOnlyList<byte>)s.ToArray())], bufferSize, [.. priority], TimeLimit);
}
