using NetrunnerSolver.Core.Input;
using NetrunnerSolver.Windows.Interop;

namespace NetrunnerSolver.Windows.Input;

public sealed class HotkeyPoller : IDisposable
{
    private const int IntervalMs = 30;
    private const int EscapeKey = 0x1B;

    private readonly Action _onFire;
    private readonly object _gate = new();
    private readonly Timer _timer;
    private ChordDetector _chord;
    private ChordDetector? _escape;
    private Action? _onEscape;
    private bool _paused;
    private int _ticking;

    public HotkeyPoller(HotkeyChord chord, Action onFire)
    {
        ArgumentNullException.ThrowIfNull(chord);
        _onFire = onFire ?? throw new ArgumentNullException(nameof(onFire));
        _chord = new ChordDetector(chord);
        _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start() => _timer.Change(0, IntervalMs);

    public void UpdateChord(HotkeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        lock (_gate)
        {
            _chord = new ChordDetector(chord);
        }
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            _paused = paused;
        }
    }

    public void WatchEscape(Action? onEscape)
    {
        lock (_gate)
        {
            _onEscape = onEscape;
            _escape = onEscape is null ? null : new ChordDetector(new HotkeyChord([EscapeKey]));
        }
    }

    public void Dispose() => _timer.Dispose();

    private void Tick()
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1)
        {
            return;
        }

        try
        {
            Action? fire = null;
            lock (_gate)
            {
                if (_paused)
                {
                    return;
                }

                var now = Environment.TickCount64;
                if (_chord.Update(IsDown, now))
                {
                    fire = _onFire;
                }
                else if (_escape is not null && _escape.Update(IsDown, now))
                {
                    fire = _onEscape;
                }
            }

            fire?.Invoke();
        }
        finally
        {
            Volatile.Write(ref _ticking, 0);
        }
    }

    private static bool IsDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
