using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public sealed record GridGeometry(
    int Size,
    double OriginX,
    double OriginY,
    double PitchX,
    double PitchY,
    double CellWidth,
    double CellHeight,
    int AmbiguousRows,
    int AmbiguousCols)
{
    public Box Bounds => new(
        OriginX - (CellWidth / 2),
        OriginY - (CellHeight / 2),
        ((Size - 1) * PitchX) + CellWidth,
        ((Size - 1) * PitchY) + CellHeight);

    public Box CellBox(int row, int col) => new(
        OriginX + (col * PitchX) - (CellWidth / 2),
        OriginY + (row * PitchY) - (CellHeight / 2),
        CellWidth,
        CellHeight);

    public Box RowStrip(int row, double padding)
    {
        var bounds = Bounds;
        var centerY = OriginY + (row * PitchY);
        return new Box(
            bounds.X - (PitchX / 2),
            centerY - (CellHeight / 2) - padding,
            bounds.Width + PitchX,
            CellHeight + (2 * padding));
    }

    public Box ColumnStrip(int col, double padding)
    {
        var bounds = Bounds;
        var centerX = OriginX + (col * PitchX);
        return new Box(
            centerX - (CellWidth / 2) - padding,
            bounds.Y - (PitchY / 2),
            CellWidth + (2 * padding),
            bounds.Height + PitchY);
    }

    public GridGeometry Shift(int rows, int cols) => this with
    {
        OriginX = OriginX + (cols * PitchX),
        OriginY = OriginY + (rows * PitchY),
        AmbiguousRows = 0,
        AmbiguousCols = 0,
    };

    public bool TryLocate(double x, double y, double tolerance, out Cell cell)
    {
        var col = (int)Math.Round((x - OriginX) / PitchX);
        var row = (int)Math.Round((y - OriginY) / PitchY);
        cell = new Cell(row, col);
        return row >= 0 && col >= 0 && row < Size && col < Size
            && Math.Abs(((x - OriginX) / PitchX) - col) <= tolerance
            && Math.Abs(((y - OriginY) / PitchY) - row) <= tolerance;
    }
}

public sealed record SequenceLine(IReadOnlyList<byte> Codes, IReadOnlyList<Box> Boxes);
