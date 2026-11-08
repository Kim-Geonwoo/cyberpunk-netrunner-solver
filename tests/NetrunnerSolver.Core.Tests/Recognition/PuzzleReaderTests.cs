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
        Assert.DoesNotContain(ReadWarning.TokenLimitReached, reading.Warnings);
        Assert.InRange(reading.TokenCount, 25, TokenPool.MaxTokens / 10);
    }

    [Fact]
    public async Task StripScanOfAWholeScreenStaysFarBelowTheTokenCap()
    {
        // 전체 화면에서 못 읽어 띠 36개를 겹쳐 다시 읽어도, 겹친 글자는 표만 늘고 토큰 수는 그대로다.
        var (screen, ko, _) = SceneA();
        var engine = new FakeOcrEngine("en-US", ko, screen.Width, screen.Height, blindAboveHeight: 200);

        var reading = await new PuzzleReader([engine, new FakeOcrEngine("ko", ko, screen.Width, screen.Height, blindAboveHeight: 200)]).ReadAsync(screen, CancellationToken.None);

        AssertPuzzleA(reading);
        Assert.Contains(ReadWarning.UsedStripScan, reading.Warnings);
        Assert.InRange(reading.TokenCount, 25, TokenPool.MaxTokens / 10);
    }

    [Fact]
    public async Task ScreenFullOfCodesIsCappedAndReportedInsteadOfGrindingOn()
    {
        // 80×60 칸에 코드 글자가 빽빽한 화면. 토큰은 상한에서 멈추고, 격자(4~8칸)는 없다고 끝난다.
        var words = new List<OcrWord>();
        for (var row = 0; row < 60; row++)
        {
            for (var col = 0; col < 80; col++)
            {
                words.Add(new OcrWord("1C", new Box(10 + (col * 30), 10 + (row * 20), 24, 14)));
            }
        }

        var screen = Blank(2420, 1220);

        var reading = await new PuzzleReader([new FakeOcrEngine("ko", words, screen.Width, screen.Height)]).ReadAsync(screen, CancellationToken.None);

        Assert.Equal(ReadOutcome.NotFound, reading.Outcome);
        Assert.Equal(TokenPool.MaxTokens, reading.TokenCount);
        Assert.Contains(ReadWarning.TokenLimitReached, reading.Warnings);
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

    [Fact]
    public async Task CancellingAfterTheLastOcrCallStopsTheLayoutPhase()
    {
        // 장면 A는 전체 화면 한 번으로 다 읽힌다. 그 뒤 격자 맞추기·글리프 단계도 취소를 봐야 한다.
        var (screen, ko, _) = SceneA();
        using var cts = new CancellationTokenSource();
        var engine = new CancelOnCallEngine(new FakeOcrEngine("ko", ko, screen.Width, screen.Height), cts, cancelOnCall: 1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PuzzleReader([engine]).ReadAsync(screen, cts.Token));
    }

    [Fact]
    public async Task CancellingDuringTheRowRereadStopsTheGlyphFallback()
    {
        // 가운데 칸을 OCR이 끝내 못 읽게 하면 줄 재인식(두 번째이자 마지막 호출) 뒤 글리프 비교로 넘어간다.
        var (screen, ko, _) = SceneA();
        var grid = LayoutParser.FindMatrix(OcrFixture.Tokens("game-ko-5x5-a.ko"))!;
        var hidden = grid.CellBox(2, 2);
        var words = ko.Where(w => !w.Box.Contains(hidden.CenterX, hidden.CenterY)).ToList();
        using var cts = new CancellationTokenSource();
        var engine = new CancelOnCallEngine(new FakeOcrEngine("ko", words, screen.Width, screen.Height), cts, cancelOnCall: 2);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PuzzleReader([engine]).ReadAsync(screen, cts.Token));
    }
}
