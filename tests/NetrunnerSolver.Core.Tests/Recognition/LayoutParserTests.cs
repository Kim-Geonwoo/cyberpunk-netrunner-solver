using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

public class LayoutParserTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    public void ReadsMatrixAndSequencesFromKoreanOcr(string screenshot)
    {
        var tokens = OcrFixture.Tokens($"game-ko-5x5-{screenshot}.ko");
        var (matrix, sequences) = screenshot == "a"
            ? (OcrFixture.MatrixA, OcrFixture.SequencesA)
            : (OcrFixture.MatrixB, OcrFixture.SequencesB);

        var grid = LayoutParser.FindMatrix(tokens);

        Assert.NotNull(grid);
        Assert.Equal(5, grid.Size);
        Assert.Equal(0, grid.AmbiguousRows);
        Assert.Equal(0, grid.AmbiguousCols);
        OcrFixture.AssertCells(matrix, LayoutParser.ReadCells(grid, tokens));
        OcrFixture.AssertSequences(sequences, LayoutParser.FindSequences(grid, tokens).Select(s => s.Codes));
    }

    [Fact]
    public void GridGeometryIsExactlyAsBefore()
    {
        // 묶음 평균·간격 지지를 할당 없이 다시 쓴 뒤에도 실수 값까지 같다(바뀌면 칸 위치와 오버레이가 미세하게 움직인다).
        var grid = LayoutParser.FindMatrix(OcrFixture.Tokens("game-ko-5x5-a.ko"));

        Assert.NotNull(grid);
        Assert.Equal(
            (5, 336.5799999999999, 385.86, 67.37000000000002, 67.51, 34.0, 24.0),
            (grid.Size, grid.OriginX, grid.OriginY, grid.PitchX, grid.PitchY, grid.CellWidth, grid.CellHeight));
    }

    [Fact]
    public void MissingInnerRowStaysInTheGridAsUnknown()
    {
        var full = OcrFixture.Tokens("game-ko-5x5-a.ko");
        var grid = LayoutParser.FindMatrix(full)!;
        var rowThreeY = grid.CellBox(3, 0).CenterY;
        var tokens = full.Where(t => Math.Abs(t.Box.CenterY - rowThreeY) > grid.PitchY / 3 || t.Box.CenterX > grid.Bounds.Right).ToList();

        var partial = LayoutParser.FindMatrix(tokens);

        Assert.NotNull(partial);
        Assert.Equal(5, partial.Size);
        var cells = LayoutParser.ReadCells(partial, tokens);
        for (var r = 0; r < 5; r++)
        {
            for (var c = 0; c < 5; c++)
            {
                Assert.Equal(r == 3 ? BreachCodes.Unknown : OcrFixture.MatrixA[r, c], cells[r, c]);
            }
        }

        Assert.Equal(0.8, LayoutParser.FillRatio(cells), 6);
    }

    [Fact]
    public void MissingEdgeRowIsReportedAsAmbiguous()
    {
        var full = OcrFixture.Tokens("game-ko-5x5-a.ko");
        var grid = LayoutParser.FindMatrix(full)!;
        var rowZeroY = grid.CellBox(0, 0).CenterY;
        var tokens = full.Where(t => Math.Abs(t.Box.CenterY - rowZeroY) > grid.PitchY / 3 || t.Box.CenterX > grid.Bounds.Right).ToList();

        var partial = LayoutParser.FindMatrix(tokens);

        Assert.NotNull(partial);
        Assert.Equal(5, partial.Size);
        Assert.Equal(1, partial.AmbiguousRows);
        Assert.Equal(0, partial.AmbiguousCols);
    }

    [Fact]
    public void ShiftMovesTheOriginByWholeCellsAndClearsAmbiguity()
    {
        var grid = new GridGeometry(5, 100, 200, 40, 50, 24, 20, 1, 0);

        var shifted = grid.Shift(-1, 0);

        Assert.Equal(150, shifted.OriginY);
        Assert.Equal(100, shifted.OriginX);
        Assert.Equal(0, shifted.AmbiguousRows);
    }

    [Fact]
    public void SequenceTokensAloneAreNotAMatrix()
    {
        var full = OcrFixture.Tokens("game-ko-5x5-a.ko");
        var grid = LayoutParser.FindMatrix(full)!;
        var sequenceOnly = full.Where(t => t.Box.CenterX > grid.Bounds.Right).ToList();

        Assert.Null(LayoutParser.FindMatrix(sequenceOnly));
        Assert.Null(LayoutParser.FindMatrix([]));
    }

    [Fact]
    public void SequenceColumnWhereEveryCodeTiesGivesNoSequences()
    {
        // 엔진 둘이 같은 자리를 서로 다르게 읽으면 그 자리는 '?'가 되고, 남는 줄이 없을 수 있다.
        var grid = new GridGeometry(5, 100, 200, 40, 50, 24, 20, 0, 0);
        var x = grid.Bounds.Right + 100;
        Token[] tokens = [new(0x1C, new Box(x, 200, 24, 20)), new(0x55, new Box(x, 200, 24, 20))];

        Assert.Empty(LayoutParser.FindSequences(grid, tokens));
    }

    [Fact]
    public void GeometryBoxesFollowTheLattice()
    {
        var grid = new GridGeometry(5, 100, 200, 40, 50, 24, 20, 0, 0);

        Assert.Equal(new Box(128, 240, 24, 20), grid.CellBox(1, 1));
        Assert.Equal(new Box(88, 190, 184, 220), grid.Bounds);
        Assert.Equal(new Box(68, 285, 224, 30), grid.RowStrip(2, 5));
        Assert.Equal(new Box(123, 165, 34, 270), grid.ColumnStrip(1, 5));
    }
}
