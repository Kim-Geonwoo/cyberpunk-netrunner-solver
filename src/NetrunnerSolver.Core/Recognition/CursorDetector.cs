using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public static class CursorDetector
{
    private const double MinCyanRatio = 0.03;
    private const double Reach = 0.75;
    private const double SequenceSpan = 10;
    private const double MinCursorArea = 0.25;

    public static bool Covers(BgraImage image, GridGeometry grid, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(grid);
        var center = grid.CellBox(cell.Row, cell.Col);
        var left = Math.Max(0, (int)Math.Floor(center.CenterX - (Reach * grid.PitchX)));
        var right = Math.Min(image.Width, (int)Math.Ceiling(center.CenterX + (Reach * grid.PitchX)));
        var top = Math.Max(0, (int)Math.Floor(center.CenterY - (Reach * grid.PitchY)));
        var bottom = Math.Min(image.Height, (int)Math.Ceiling(center.CenterY + (Reach * grid.PitchY)));
        if (right <= left || bottom <= top)
        {
            return false;
        }

        return CountCyan(image, left, top, right, bottom) >= (right - left) * (bottom - top) * MinCyanRatio;
    }

    public static bool CoversSequences(BgraImage image, GridGeometry grid, IReadOnlyList<SequenceLine> sequences)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(sequences);
        var bounds = grid.Bounds;
        var boxes = sequences.SelectMany(s => s.Boxes).ToList();
        double tokenWidth, tokenHeight, left, top, bottom;
        if (boxes.Count == 0)
        {
            tokenWidth = grid.CellWidth;
            tokenHeight = grid.CellHeight;
            left = bounds.Right + (grid.PitchX / 2);
            top = bounds.Y - grid.PitchY;
            bottom = bounds.Bottom;
        }
        else
        {
            tokenWidth = boxes.Average(b => b.Width);
            tokenHeight = boxes.Average(b => b.Height);
            var starts = sequences.Select(s => s.Boxes[0].CenterY).OrderBy(y => y).ToList();
            var gap = starts.Count > 1 ? starts.Zip(starts.Skip(1), (a, b) => b - a).Min() : grid.PitchY;
            left = boxes.Min(b => b.X) - tokenWidth;
            top = Math.Min(boxes.Min(b => b.Y), bounds.Y) - (tokenHeight / 2);
            bottom = Math.Max(boxes.Max(b => b.Bottom) + gap, bounds.Y + gap);
        }

        var right = left + (SequenceSpan * tokenWidth);
        var count = CountCyan(
            image,
            Math.Max(0, (int)Math.Floor(left)),
            Math.Max(0, (int)Math.Floor(top)),
            Math.Min(image.Width, (int)Math.Ceiling(right)),
            Math.Min(image.Height, (int)Math.Ceiling(bottom)));
        return count >= MinCursorArea * tokenHeight * tokenHeight;
    }

    private static int CountCyan(BgraImage image, int left, int top, int right, int bottom)
    {
        var pixels = image.Pixels;
        var cyan = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var i = ((y * image.Width) + x) * 4;
                byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                if (b >= 150 && g >= 120 && r <= 110 && b - r >= 80)
                {
                    cyan++;
                }
            }
        }

        return cyan;
    }
}
