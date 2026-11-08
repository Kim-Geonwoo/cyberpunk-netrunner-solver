namespace NetrunnerSolver.Core;

public sealed class CodeMatrix
{
    private readonly byte[,] _codes;

    public CodeMatrix(byte[,] codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        var rows = codes.GetLength(0);
        var cols = codes.GetLength(1);
        if (rows == 0 || rows != cols)
        {
            throw new ArgumentException($"Matrix must be square and non-empty, got {rows}x{cols}.", nameof(codes));
        }

        _codes = (byte[,])codes.Clone();
        Size = rows;
    }

    public int Size { get; }

    public byte this[int row, int col] => _codes[row, col];

    public byte this[Cell cell] => _codes[cell.Row, cell.Col];

    public CodeMatrix With(Cell cell, byte code)
    {
        var copy = (byte[,])_codes.Clone();
        copy[cell.Row, cell.Col] = code;
        return new CodeMatrix(copy);
    }
}
