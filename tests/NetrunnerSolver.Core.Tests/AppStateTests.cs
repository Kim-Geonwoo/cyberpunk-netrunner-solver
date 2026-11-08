using NetrunnerSolver.Core.Input;
using NetrunnerSolver.Core.Session;
using NetrunnerSolver.Core.Settings;
using NetrunnerSolver.Core.Text;

namespace NetrunnerSolver.Core.Tests;

public class SolveSessionTests
{
    private static SolveSession WebSession() => new(
        new CodeMatrix(new byte[,]
        {
            { 0x55, 0x1C, 0xBD, 0x1C, 0x1C },
            { 0x55, 0x55, 0xBD, 0x1C, 0xE9 },
            { 0xE9, 0x1C, 0x55, 0x1C, 0xBD },
            { 0x55, 0x1C, 0x1C, 0xBD, 0x55 },
            { 0xE9, 0x55, 0x1C, 0xE9, 0xE9 },
        }),
        [[0xE9, 0x1C], [0xE9, 0x55], [0xE9, 0x55, 0x1C]],
        4);

    [Fact]
    public void StartsWithTopFirstPriorityAndASolution()
    {
        var session = WebSession();

        Assert.Equal([0, 1, 2], session.Priority);
        Assert.Equal([true, false, false], session.Result.Completed);
    }

    [Fact]
    public void MovingPriorityResolvesAndRaisesChangedOnce()
    {
        var session = WebSession();
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.MovePriority(0, +2);

        Assert.Equal([1, 2, 0], session.Priority);
        Assert.Equal([false, true, true], session.Result.Completed);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void BufferSizeIsClamped()
    {
        var session = WebSession();

        session.SetBufferSize(0);
        Assert.Equal(1, session.BufferSize);
        session.SetBufferSize(20);
        Assert.Equal(12, session.BufferSize);
    }

    [Fact]
    public void CyclingCellsChangesTheMatrixAndSequences()
    {
        var session = WebSession();

        session.CycleMatrixCell(new Cell(0, 0));
        session.CycleSequenceCode(0, 1);

        Assert.Equal(0x7A, session.Matrix[0, 0]);
        Assert.Equal(new byte[] { 0xE9, 0x55 }, session.Sequences[0]);
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
