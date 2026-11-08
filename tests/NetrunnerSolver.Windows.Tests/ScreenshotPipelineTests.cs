using NetrunnerSolver.Core.Recognition;
using NetrunnerSolver.Windows.Imaging;
using NetrunnerSolver.Windows.Ocr;
using Xunit.Abstractions;

namespace NetrunnerSolver.Windows.Tests;

public class ScreenshotPipelineTests(ITestOutputHelper output)
{
    private static readonly byte[,] MatrixA =
    {
        { 0xE9, 0xE9, 0x1C, 0x55, 0x1C },
        { 0xBD, 0x1C, 0xBD, 0x55, 0xBD },
        { 0x1C, 0x55, 0xE9, 0xBD, 0xE9 },
        { 0xBD, 0xBD, 0x1C, 0x1C, 0xBD },
        { 0xBD, 0x55, 0x1C, 0x55, 0x1C },
    };

    private static readonly string[] SequencesA = ["1C55", "1C1C", "BDBD1C"];

    private static readonly byte[,] MatrixB =
    {
        { 0x1C, 0xBD, 0x1C, 0x55, 0xE9 },
        { 0x1C, 0xBD, 0xBD, 0x55, 0xE9 },
        { 0xE9, 0x55, 0xE9, 0xBD, 0x55 },
        { 0xBD, 0xBD, 0x1C, 0x1C, 0x1C },
        { 0x55, 0x1C, 0x1C, 0x55, 0x1C },
    };

    private static readonly string[] SequencesB = ["BD55", "1CE9", "E9BD1C"];

    [FixtureFact("game-ko-5x5-cursor.webp")]
    public async Task ReportsTheMouseCursorCoveringACell()
    {
        var screen = await ImageFile.LoadAsync(TestFixtures.Path("game-ko-5x5-cursor.webp"));

        var reading = await new PuzzleReader(OcrEngines.CreateAvailable()).ReadAsync(screen, CancellationToken.None);

        output.WriteLine($"warnings: {string.Join(", ", reading.Warnings)}");
        Assert.NotNull(reading.Matrix);
        byte[,] expected =
        {
            { 0x1C, 0xBD, 0xBD, 0x55, 0x00 },
            { 0xBD, 0x1C, 0x1C, 0x55, 0x1C },
            { 0xBD, 0xE9, 0x1C, 0x1C, 0x55 },
            { 0x55, 0xE9, 0x1C, 0x55, 0xBD },
            { 0xE9, 0x55, 0x1C, 0x55, 0xE9 },
        };
        for (var r = 0; r < 5; r++)
        {
            for (var c = 0; c < 5; c++)
            {
                Assert.Equal(expected[r, c], reading.Matrix[r, c]);
            }
        }

        Assert.Contains(ReadWarning.CursorCovering, reading.Warnings);
        Assert.Equal(ReadOutcome.CursorCovered, reading.Outcome);
    }

    [FixtureFact("game-ko-5x5-cursor-seq.webp")]
    public async Task ReportsTheMouseCursorOverTheSequenceList()
    {
        var screen = await ImageFile.LoadAsync(TestFixtures.Path("game-ko-5x5-cursor-seq.webp"));

        var reading = await new PuzzleReader(OcrEngines.CreateAvailable()).ReadAsync(screen, CancellationToken.None);

        output.WriteLine($"warnings: {string.Join(", ", reading.Warnings)}; sequences: {string.Join(" | ", reading.Sequences.Select(s => Convert.ToHexString([.. s.Codes])))}");
        Assert.Equal(ReadOutcome.CursorCovered, reading.Outcome);
    }

    [FixtureFact("game-ko-5x5-a.webp")]
    public Task ReadsGameAWithAllEngines() => Check("game-ko-5x5-a.webp", null, MatrixA, SequencesA);

    [FixtureFact("game-ko-5x5-b.webp")]
    public Task ReadsGameBWithAllEngines() => Check("game-ko-5x5-b.webp", null, MatrixB, SequencesB);

    [FixtureFact("game-ko-5x5-a.webp", OcrLanguage = "en-US")]
    public Task ReadsGameAWithEnglishOnly() => Check("game-ko-5x5-a.webp", "en-US", MatrixA, SequencesA);

    [FixtureFact("game-ko-5x5-b.webp", OcrLanguage = "en-US")]
    public Task ReadsGameBWithEnglishOnly() => Check("game-ko-5x5-b.webp", "en-US", MatrixB, SequencesB);

    private async Task Check(string fixture, string? language, byte[,] matrix, string[] sequences)
    {
        var screen = await ImageFile.LoadAsync(TestFixtures.Path(fixture));
        var reader = new PuzzleReader(OcrEngines.CreateAvailable(language));

        var reading = await reader.ReadAsync(screen, CancellationToken.None);

        output.WriteLine($"{fixture} [{language ?? "all"}]: {reading.Elapsed.TotalMilliseconds:F0} ms, warnings: {string.Join(", ", reading.Warnings)}");
        Assert.NotNull(reading.Matrix);
        for (var r = 0; r < 5; r++)
        {
            output.WriteLine(string.Join(' ', Enumerable.Range(0, 5).Select(c => Core.BreachCodes.Format(reading.Matrix[r, c]))));
        }

        output.WriteLine("sequences: " + string.Join(" | ", reading.Sequences.Select(s => Convert.ToHexString([.. s.Codes]))));
        output.WriteLine($"buffer: {reading.Buffer?.SlotCount.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");
        for (var r = 0; r < 5; r++)
        {
            for (var c = 0; c < 5; c++)
            {
                Assert.Equal(matrix[r, c], reading.Matrix[r, c]);
            }
        }

        Assert.Equal(sequences, reading.Sequences.Select(s => Convert.ToHexString([.. s.Codes])));
        Assert.NotNull(reading.Buffer);
        Assert.Equal(4, reading.Buffer.SlotCount);
        Assert.False(reading.Buffer.HasContent);
        Assert.Equal(ReadOutcome.Solved, reading.Outcome);
    }
}
