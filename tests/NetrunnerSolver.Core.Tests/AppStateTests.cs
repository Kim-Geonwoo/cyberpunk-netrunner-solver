using NetrunnerSolver.Core.Input;
using NetrunnerSolver.Core.Session;
using NetrunnerSolver.Core.Settings;
using NetrunnerSolver.Core.Solving;
using NetrunnerSolver.Core.Text;

namespace NetrunnerSolver.Core.Tests;

public class SolveSessionTests
{
    private static readonly byte[,] WebCells =
    {
        { 0x55, 0x1C, 0xBD, 0x1C, 0x1C },
        { 0x55, 0x55, 0xBD, 0x1C, 0xE9 },
        { 0xE9, 0x1C, 0x55, 0x1C, 0xBD },
        { 0x55, 0x1C, 0x1C, 0xBD, 0x55 },
        { 0xE9, 0x55, 0x1C, 0xE9, 0xE9 },
    };

    private static Task<SolveSession> WebSessionAsync() =>
        SolveSession.StartAsync(new CodeMatrix(WebCells), [[0xE9, 0x1C], [0xE9, 0x55], [0xE9, 0x55, 0x1C]], 4);

    // 1C로 채운 8×8의 맨 아랫줄에만 다른 코드 다섯을 두고 그 코드들을 잇는 시퀀스를 주면, 버퍼 12에서 1초 제한까지 푼다.
    // 버퍼 1에서는 쓸 수 있는 시퀀스가 없어 바로 끝난다.
    private static Task<SolveSession> HeavySessionAsync(int bufferSize)
    {
        var cells = new byte[8, 8];
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                cells[r, c] = 0x1C;
            }
        }

        byte[] rare = [0x55, 0x7A, 0xBD, 0xE9, 0xFF];
        for (var c = 0; c < rare.Length; c++)
        {
            cells[7, c] = rare[c];
        }

        IReadOnlyList<IReadOnlyList<byte>> chain = [[0x55, 0x7A], [0x7A, 0xBD], [0xBD, 0xE9], [0xE9, 0xFF], [0xFF, 0x55], [0x1C, 0x1C]];
        return SolveSession.StartAsync(new CodeMatrix(cells), chain, bufferSize);
    }

    [Fact]
    public async Task StartsWithTopFirstPriorityAndASolution()
    {
        using var session = await WebSessionAsync();

        Assert.Equal([0, 1, 2], session.Priority);
        Assert.Equal([true, false, false], session.Result.Completed);
    }

    [Fact]
    public async Task MovingPriorityResolvesAndRaisesChangedOnce()
    {
        using var session = await WebSessionAsync();
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.MovePriority(0, +2);
        await session.Solving;

        Assert.Equal([1, 2, 0], session.Priority);
        Assert.Equal([false, true, true], session.Result.Completed);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task BufferSizeIsClamped()
    {
        using var session = await WebSessionAsync();

        session.SetBufferSize(0);
        Assert.Equal(1, session.BufferSize);
        session.SetBufferSize(20);
        Assert.Equal(12, session.BufferSize);
        await session.Solving;
    }

    [Fact]
    public async Task CyclingCellsChangesTheMatrixAndSequences()
    {
        using var session = await WebSessionAsync();

        session.CycleMatrixCell(new Cell(0, 0));
        session.CycleSequenceCode(0, 1);
        await session.Solving;

        Assert.Equal(0x7A, session.Matrix[0, 0]);
        Assert.Equal(new byte[] { 0xE9, 0x55 }, session.Sequences[0]);
    }

    [Fact]
    public async Task ChangeReturnsBeforeASlowSolveFinishes()
    {
        // 예전에는 고친 자리(UI 스레드)에서 1초를 다 풀고서야 돌아왔다.
        using var session = await HeavySessionAsync(bufferSize: 1);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        session.SetBufferSize(12);

        Assert.True(clock.ElapsedMilliseconds < 200, $"took {clock.ElapsedMilliseconds} ms");
        Assert.False(session.Solving.IsCompleted);
        await session.Solving;
        Assert.True(session.Result.TimedOut);
    }

    [Fact]
    public async Task DisposeCancelsTheRunningSolve()
    {
        var session = await HeavySessionAsync(bufferSize: 1);
        var before = session.Result;
        var changes = 0;
        session.Changed += (_, _) => changes++;
        session.SetBufferSize(12);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        session.Dispose();
        await session.Solving;

        Assert.True(clock.ElapsedMilliseconds < 800, $"took {clock.ElapsedMilliseconds} ms");
        Assert.Equal(0, changes);
        Assert.Same(before, session.Result);
        session.SetBufferSize(5);
        Assert.True(session.Solving.IsCompleted);
    }

    [Fact]
    public async Task StartObservesTheCallersToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SolveSession.StartAsync(new CodeMatrix(WebCells), [[0xE9, 0x1C]], 4, cts.Token));
    }

    [Fact]
    public void ChangedArrivesOnTheChangingThreadAndOnlyForTheLatestState()
    {
        var context = new PumpingContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            using var session = context.Run(WebSessionAsync());
            var threads = new List<int>();
            session.Changed += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

            session.MovePriority(0, +2);
            Assert.Empty(threads);
            session.MovePriority(2, -1);
            context.Run(session.Solving);
            context.Drain();

            Assert.Equal([Environment.CurrentManagedThreadId], threads);
            Assert.Equal([2, 1, 0], session.Priority);
            var expected = Solver.Solve(new SolveRequest(session.Matrix, session.Sequences, 4, [2, 1, 0], TimeSpan.FromSeconds(1)));
            Assert.Equal(expected.Completed, session.Result.Completed);
            Assert.Equal(expected.Path, session.Result.Path);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    // UI 스레드처럼 Post된 일을 이 스레드에서 차례로 돌린다.
    private sealed class PumpingContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public T Run<T>(Task<T> task)
        {
            Run((Task)task);
            return task.GetAwaiter().GetResult();
        }

        public void Run(Task task)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (!Drain())
                {
                    Thread.Sleep(1);
                }

                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "timed out pumping");
            }

            task.GetAwaiter().GetResult();
        }

        public bool Drain()
        {
            var ran = false;
            while (_queue.TryDequeue(out var item))
            {
                item.Callback(item.State);
                ran = true;
            }

            return ran;
        }
    }
}

public class HotkeyTests
{
    [Fact]
    public void FiresOncePerPressWithDebounce()
    {
        var detector = new ChordDetector(HotkeyChord.Default);
        var down = new HashSet<int>();
        bool Step(long t) => detector.Update(down.Contains, t);

        Assert.False(Step(0));
        down.Add(0xC0);
        Assert.False(Step(10));
        down.Add(0x20);
        Assert.True(Step(20));
        Assert.False(Step(70));
        down.Remove(0x20);
        Assert.False(Step(120));
        down.Add(0x20);
        Assert.False(Step(170));
        down.Remove(0x20);
        Assert.False(Step(250));
        down.Add(0x20);
        Assert.True(Step(320));
    }

    [Fact]
    public void KeyOrderDoesNotMatter()
    {
        var detector = new ChordDetector(HotkeyChord.Default);
        var down = new HashSet<int> { 0x20 };

        Assert.False(detector.Update(down.Contains, 0));
        down.Add(0xC0);
        Assert.True(detector.Update(down.Contains, 30));
    }

    [Fact]
    public void RecorderCollectsUpToThreeKeysAndNormalisesSides()
    {
        var recorder = new ChordRecorder();

        recorder.KeyDown(0xA2);
        recorder.KeyDown(0xA0);
        recorder.KeyDown(0xA0);
        recorder.KeyDown(0x41);
        recorder.KeyDown(0x42);
        Assert.Null(recorder.Result);
        recorder.KeyUp(0x41);
        recorder.KeyUp(0x42);
        recorder.KeyUp(0xA0);
        recorder.KeyUp(0xA2);

        Assert.Equal([0x11, 0x10, 0x41], recorder.Result!.VirtualKeys);
    }

    [Fact]
    public void RecorderStartsOverAfterAllKeysAreReleased()
    {
        var recorder = new ChordRecorder();
        recorder.KeyDown(0x74);
        recorder.KeyUp(0x74);

        recorder.KeyDown(0xC0);
        recorder.KeyDown(0x20);
        recorder.KeyUp(0x20);
        recorder.KeyUp(0xC0);

        Assert.Equal("`+Space", recorder.Result!.ToDisplayString());
    }

    [Fact]
    public void DisplayNamesAreReadable()
    {
        Assert.Equal("`+Space", HotkeyChord.Default.ToDisplayString());
        Assert.Equal("Ctrl+Shift+A", new HotkeyChord([0x11, 0x10, 0x41]).ToDisplayString());
        Assert.Equal("F5", new HotkeyChord([0x74]).ToDisplayString());
    }
}

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"nrs-settings-{Guid.NewGuid():N}");

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void MissingFileGivesDefaults()
    {
        var settings = SettingsStore.Load(SettingsPath);

        Assert.Equal(HotkeyChord.Default.VirtualKeys, settings.Hotkey.VirtualKeys);
        Assert.Equal(4, settings.LastBufferSize);
        Assert.Equal("auto", settings.Language);
    }

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        var saved = new AppSettings(new HotkeyChord([0x11, 0x41]), 7, "en");

        SettingsStore.Save(SettingsPath, saved);
        var loaded = SettingsStore.Load(SettingsPath);

        Assert.Equal(saved.Hotkey.VirtualKeys, loaded.Hotkey.VirtualKeys);
        Assert.Equal(7, loaded.LastBufferSize);
        Assert.Equal("en", loaded.Language);
    }

    [Fact]
    public void CorruptFileIsBackedUpAndDefaultsReturned()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ not json");

        var settings = SettingsStore.Load(SettingsPath);

        Assert.Equal(4, settings.LastBufferSize);
        Assert.True(File.Exists(SettingsPath + ".bak"));
    }

    [Fact]
    public void LockedFileGivesDefaultsInsteadOfThrowing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ not json");
        using var locked = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var settings = SettingsStore.Load(SettingsPath);

        Assert.Equal(AppSettings.Default.LastBufferSize, settings.LastBufferSize);
    }

    [Fact]
    public void OutOfRangeValuesAreRepaired()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{"hotkey":[],"lastBufferSize":99,"language":"de"}""");

        var settings = SettingsStore.Load(SettingsPath);

        Assert.Equal(12, settings.LastBufferSize);
        Assert.Equal("auto", settings.Language);
        Assert.Equal(HotkeyChord.Default.VirtualKeys, settings.Hotkey.VirtualKeys);
    }
}

public class UiTextTests
{
    [Fact]
    public void KoreanAndEnglishHaveTheSameNonEmptyKeys()
    {
        Assert.Equal(UiText.Keys("ko").OrderBy(k => k), UiText.Keys("en").OrderBy(k => k));
        Assert.All(UiText.Keys("ko"), k => Assert.False(string.IsNullOrWhiteSpace(UiText.Get(k, "ko"))));
        Assert.All(UiText.Keys("en"), k => Assert.False(string.IsNullOrWhiteSpace(UiText.Get(k, "en"))));
    }

    [Fact]
    public void LooksUpKnownKeysAndRejectsUnknown()
    {
        Assert.Equal("다시 인식", UiText.Get("action.reread", "ko"));
        Assert.Equal("Re-scan", UiText.Get("action.reread", "en"));
        Assert.Throws<KeyNotFoundException>(() => UiText.Get("nope", "ko"));
    }

    [Theory]
    [InlineData("auto", "ko-KR", "ko")]
    [InlineData("auto", "en-US", "en")]
    [InlineData("auto", "ja-JP", "en")]
    [InlineData("ko", "en-US", "ko")]
    [InlineData("en", "ko-KR", "en")]
    public void ResolvesLanguage(string setting, string culture, string expected)
    {
        Assert.Equal(expected, UiText.ResolveLanguage(setting, culture));
    }
}

public class LogTextTests
{
    private static readonly DateTime Time = new(2026, 11, 8, 13, 5, 7, 89);

    [Fact]
    public void PlainMessageIsOneLine()
    {
        Assert.Equal($"13:05:07.089 INFO read 1920x1080 in 35 ms, 로그{Environment.NewLine}", LogText.Line(Time, "INFO", "read 1920x1080 in 35 ms, 로그"));
    }

    [Fact]
    public void MessageCannotForgeANewRecord()
    {
        var line = LogText.Line(Time, "INFO", "first\r\n13:00:00.000 ERROR forged\rsecond\nthird\u2028fourth");

        var lines = line.Split(Environment.NewLine);

        Assert.Equal("13:05:07.089 INFO first", lines[0]);
        Assert.Equal(
            ["    13:00:00.000 ERROR forged", "    second", "    third", "    fourth", string.Empty],
            lines[1..]);
    }

    [Fact]
    public void OtherControlAndFormatCharactersAreEscaped()
    {
        var line = LogText.Line(Time, "ERR\tOR", "a\u001b[31mb\u0000c\u202Ed\u00ADe");

        Assert.Equal($@"13:05:07.089 ERR\u0009OR a\u001B[31mb\u0000c\u202Ed\u00ADe{Environment.NewLine}", line);
    }
}
