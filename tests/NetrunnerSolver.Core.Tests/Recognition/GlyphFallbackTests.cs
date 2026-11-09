using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

public class GlyphFallbackTests
{
    private const double RowZeroY = 386;
    private const double ColumnFourX = 606;

    private static async Task<PuzzleReading> ReadWithout(Func<OcrWord, bool> hidden, Box? erase = null, bool cursor = true)
    {
        var (width, height, words) = OcrFixture.Load("game-ko-5x5-a.ko");
        var screen = erase is { } box
            ? GlyphScene.Draw(width, height, words.Where(w => !box.Contains(w.Box.CenterX, w.Box.CenterY)), cursor ? [box] : [])
            : GlyphScene.Draw(width, height, words);
        var engine = new FakeOcrEngine("en-US", [.. words.Where(w => !hidden(w))], width, height);
        return await new PuzzleReader([engine]).ReadAsync(screen, CancellationToken.None);
    }

    private static bool InMatrix(OcrWord w) => w.Box.CenterX < 700;

    private static void AssertMatrixA(PuzzleReading reading)
    {
        Assert.NotNull(reading.Matrix);
        var matrix = reading.Matrix;
        string Rows(Func<int, int, byte> at, int size) =>
            string.Join(" / ", Enumerable.Range(0, size).Select(r => string.Join(' ', Enumerable.Range(0, size).Select(c => BreachCodes.Format(at(r, c))))));
        Assert.Equal(Rows((r, c) => OcrFixture.MatrixA[r, c], 5), Rows((r, c) => matrix[r, c], matrix.Size));
    }

    [Fact]
    public async Task TopRowUnreadByOcrIsRecoveredWithoutShiftingTheGrid()
    {
        var reading = await ReadWithout(w => InMatrix(w) && Math.Abs(w.Box.CenterY - RowZeroY) < 20);

        AssertMatrixA(reading);
        Assert.DoesNotContain(ReadWarning.UnknownCells, reading.Warnings);
    }

    [Fact]
    public async Task TopRowAndRightColumnUnreadStillGiveTheFullMatrix()
    {
        var reading = await ReadWithout(w => InMatrix(w) && (Math.Abs(w.Box.CenterY - RowZeroY) < 20 || Math.Abs(w.Box.CenterX - ColumnFourX) < 20));

        AssertMatrixA(reading);
    }

    [Fact]
    public async Task MissingSequenceLineIsRecoveredFromGlyphs()
    {
        var reading = await ReadWithout(w => !InMatrix(w) && Math.Abs(w.Box.CenterY - 442) < 15 && w.Box.CenterX < 1100);

        OcrFixture.AssertSequences(OcrFixture.SequencesA, reading.Sequences.Select(s => s.Codes));
    }

    [Fact]
    public async Task TruncatedSequenceIsExtendedAndStopsAtBlankSpace()
    {
        var reading = await ReadWithout(w => !InMatrix(w) && w.Box.CenterX > 1040 && w.Box.CenterX < 1100);

        OcrFixture.AssertSequences(OcrFixture.SequencesA, reading.Sequences.Select(s => s.Codes));
    }

    [Fact]
    public async Task CursorCoveringACellIsReported()
    {
        var cursor = new Box(565, 345, 85, 85);

        var reading = await ReadWithout(w => cursor.Contains(w.Box.CenterX, w.Box.CenterY), cursor);

        Assert.Equal(BreachCodes.Unknown, reading.Matrix![0, 4]);
        Assert.Contains(ReadWarning.CursorCovering, reading.Warnings);
        Assert.Equal(ReadOutcome.CursorCovered, reading.Outcome);
    }

    [Fact]
    public async Task UnreadableCellWithoutCursorIsNotBlamedOnTheCursor()
    {
        var blank = new Box(565, 345, 85, 85);

        var reading = await ReadWithout(w => blank.Contains(w.Box.CenterX, w.Box.CenterY), blank, cursor: false);

        Assert.Equal(BreachCodes.Unknown, reading.Matrix![0, 4]);
        Assert.Contains(ReadWarning.UnknownCells, reading.Warnings);
        Assert.DoesNotContain(ReadWarning.CursorCovering, reading.Warnings);
        Assert.Equal(ReadOutcome.Solved, reading.Outcome);
    }

    [Fact]
    public async Task CursorHidingASequenceLineIsReported()
    {
        var hand = new Box(955, 425, 90, 34);

        var reading = await ReadWithout(w => hand.Contains(w.Box.CenterX, w.Box.CenterY), hand);

        Assert.Equal(2, reading.Sequences.Count);
        Assert.Contains(ReadWarning.CursorCovering, reading.Warnings);
        Assert.Equal(ReadOutcome.CursorCovered, reading.Outcome);
    }

    [Fact]
    public async Task CursorBelowTheLastSequenceIsReported()
    {
        var hand = new Box(960, 550, 30, 30);

        var reading = await ReadWithout(_ => false, hand);

        Assert.Equal(ReadOutcome.CursorCovered, reading.Outcome);
    }

    [Fact]
    public async Task CursorFarFromCodesIsIgnored()
    {
        var hand = new Box(1500, 900, 30, 30);

        var reading = await ReadWithout(_ => false, hand);

        Assert.Equal(ReadOutcome.Solved, reading.Outcome);
    }

    [Fact]
    public void OutcomeIsNotFoundWithoutAGrid()
    {
        var reading = new PuzzleReading(null, null, [], null, [], TimeSpan.Zero);

        Assert.Equal(ReadOutcome.NotFound, reading.Outcome);
    }

    [Fact]
    public void CompleterTerminatesWhenLinesAreCrowded()
    {
        var grid = new GridGeometry(5, 100, 100, 40, 40, 24, 24, 0, 0);
        var screen = new BgraImage(800, 600, new byte[800 * 600 * 4]);
        SequenceLine[] found =
        [
            new([0x1C, 0x55], [new Box(400, 100, 20, 16), new Box(430, 100, 20, 16)]),
            new([0x55, 0x55], [new Box(400, 101, 20, 16), new Box(430, 101, 20, 16)]),
        ];

        var result = SequenceCompleter.Complete(screen, grid, new byte[5, 5], found, CancellationToken.None);

        Assert.InRange(result.Count, 2, 6);
    }
}
