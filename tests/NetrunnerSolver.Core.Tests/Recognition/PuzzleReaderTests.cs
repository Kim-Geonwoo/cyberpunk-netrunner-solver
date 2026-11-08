using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

public class PuzzleReaderTests
{
    private static BgraImage Blank(int width, int height) => new(width, height, new byte[width * height * 4]);

    private static (BgraImage Screen, IReadOnlyList<OcrWord> Ko, IReadOnlyList<OcrWord> En) SceneA()
    {
        var ko = OcrFixture.Load("game-ko-5x5-a.ko");
        var en = OcrFixture.Load("game-ko-5x5-a.en-US");
        return (Blank(ko.Width, ko.Height), ko.Words, en.Words);
    }

    private static void AssertPuzzleA(PuzzleReading reading)
    {
        Assert.NotNull(reading.Grid);
        Assert.NotNull(reading.Matrix);
        var cells = new byte[5, 5];
        for (var r = 0; r < 5; r++)
        {
            for (var c = 0; c < 5; c++)
            {
                cells[r, c] = reading.Matrix[r, c];
            }
        }

        OcrFixture.AssertCells(OcrFixture.MatrixA, cells);
        OcrFixture.AssertSequences(OcrFixture.SequencesA, reading.Sequences.Select(s => s.Codes));
    }

    [Fact]
    public async Task ReadsPuzzleWithOneKoreanEngine()
    {
        var (screen, ko, _) = SceneA();

        var reading = await new PuzzleReader([new FakeOcrEngine("ko", ko, screen.Width, screen.Height)]).ReadAsync(screen, CancellationToken.None);

        AssertPuzzleA(reading);
        Assert.Null(reading.Buffer);
        Assert.Contains(ReadWarning.BufferNotDetected, reading.Warnings);
        Assert.DoesNotContain(ReadWarning.UsedStripScan, reading.Warnings);
    }

    [Fact]
    public async Task VotingAcrossEnginesKeepsTheAnswer()
    {
        var (screen, ko, en) = SceneA();
        IOcrEngine[] engines = [new FakeOcrEngine("ko", ko, screen.Width, screen.Height), new FakeOcrEngine("en-US", en, screen.Width, screen.Height)];

        AssertPuzzleA(await new PuzzleReader(engines).ReadAsync(screen, CancellationToken.None));
    }

    [Fact]
    public async Task EngineBlindToTheFullScreenFallsBackToStrips()
    {
        var (screen, ko, _) = SceneA();
        var engine = new FakeOcrEngine("en-US", ko, screen.Width, screen.Height, blindAboveHeight: 200);

        var reading = await new PuzzleReader([engine]).ReadAsync(screen, CancellationToken.None);

        AssertPuzzleA(reading);
        Assert.Contains(ReadWarning.UsedStripScan, reading.Warnings);
    }

    [Fact]
    public async Task RowMissingFromFullScreenIsReadFromItsStrip()
    {
        var (screen, ko, _) = SceneA();
        var engine = new FakeOcrEngine("ko", ko, screen.Width, screen.Height, hideInFullImage: w => Math.Abs(w.Box.CenterY - 589) < 20 && w.Box.CenterX < 700);

        AssertPuzzleA(await new PuzzleReader([engine]).ReadAsync(screen, CancellationToken.None));
    }

    [Fact]
    public async Task ScreenWithoutPuzzleReturnsNoGrid()
    {
        var screen = Blank(800, 600);

        var reading = await new PuzzleReader([new FakeOcrEngine("ko", [], 800, 600)]).ReadAsync(screen, CancellationToken.None);

        Assert.Null(reading.Grid);
        Assert.Null(reading.Matrix);
        Assert.Empty(reading.Sequences);
    }

    [Fact]
    public async Task BrokenEngineIsReportedAndOthersStillWork()
    {
        var (screen, ko, _) = SceneA();
        IOcrEngine[] engines = [new ThrowingOcrEngine(), new FakeOcrEngine("ko", ko, screen.Width, screen.Height)];

        var reading = await new PuzzleReader(engines).ReadAsync(screen, CancellationToken.None);

        AssertPuzzleA(reading);
        Assert.Contains(ReadWarning.EngineError, reading.Warnings);
    }

    [Fact]
    public async Task CancellationStopsReading()
    {
        var (screen, ko, _) = SceneA();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PuzzleReader([new FakeOcrEngine("ko", ko, screen.Width, screen.Height)]).ReadAsync(screen, cts.Token));
    }
}
