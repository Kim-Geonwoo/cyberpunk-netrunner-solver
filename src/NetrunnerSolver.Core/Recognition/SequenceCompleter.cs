using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

internal static class SequenceCompleter
{
    private const int MaxLines = 6;
    private const int MaxCodesPerLine = 8;

    public static IReadOnlyList<SequenceLine> Complete(BgraImage screen, GridGeometry grid, byte[,] cells, IReadOnlyList<SequenceLine> found, CancellationToken cancellationToken) =>
        Complete(new GlyphFeatures(screen), grid, cells, found, cancellationToken);

    public static IReadOnlyList<SequenceLine> Complete(GlyphFeatures glyphs, GridGeometry grid, byte[,] cells, IReadOnlyList<SequenceLine> found, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        var screen = glyphs.Image;
        cancellationToken.ThrowIfCancellationRequested();
        if (found.Count == 0)
        {
            return found;
        }

        var boxes = found.SelectMany(l => l.Boxes).ToList();
        var tokenWidth = Median(boxes.Select(b => b.Width));
        var tokenHeight = Median(boxes.Select(b => b.Height));
        var gaps = found.SelectMany(l => l.Boxes.Zip(l.Boxes.Skip(1), (a, b) => b.CenterX - a.CenterX)).ToList();
        var xPitch = gaps.Count > 0 ? Median(gaps) : tokenWidth * 1.5;
        var left = Median(found.Select(l => l.Boxes[0].CenterX));
        var samples = Samples(grid, cells, found);

        var lines = found
            .Select(l => (Y: l.Boxes[0].CenterY, Codes: l.Codes.ToList(), Boxes: l.Boxes.ToList()))
            .OrderBy(l => l.Y)
            .ToList();
        foreach (var line in lines)
        {
            Extend(glyphs, samples, line.Codes, line.Boxes, xPitch, cancellationToken);
        }

        var yPitch = LinePitch(lines.Select(l => l.Y).ToList(), grid.PitchY);
        var limit = grid.Bounds.Bottom + (3 * grid.PitchY);
        var y = lines[0].Y;
        while (lines.Count < MaxLines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guess = y + yPitch;
            if (guess > limit)
            {
                break;
            }

            var existing = lines.FirstOrDefault(l => Math.Abs(l.Y - guess) < yPitch / 2);
            if (existing.Codes is not null)
            {
                y = existing.Y;
                continue;
            }

            var snapped = SnapLine(screen, left, guess, tokenWidth, tokenHeight, yPitch);
            if (snapped is null)
            {
                if (guess > lines[^1].Y)
                {
                    break;
                }

                y = guess;
                continue;
            }

            var codes = new List<byte>();
            var lineBoxes = new List<Box>();
            var first = new Box(left - (tokenWidth / 2), snapped.Value - (tokenHeight / 2), tokenWidth, tokenHeight);
            var code = GlyphMatcher.Classify(glyphs, first, samples);
            if (code != BreachCodes.Unknown)
            {
                codes.Add(code);
                lineBoxes.Add(first);
                Extend(glyphs, samples, codes, lineBoxes, xPitch, cancellationToken);
            }

            if (codes.Count < 2)
            {
                if (guess > lines[^1].Y)
                {
                    break;
                }

                y = guess;
                continue;
            }

            lines.Add((snapped.Value, codes, lineBoxes));
            lines.Sort((a, b) => a.Y.CompareTo(b.Y));
            y = snapped.Value;
        }

        return [.. lines.Take(MaxLines).Select(l => new SequenceLine(l.Codes, l.Boxes))];
    }

    private static List<Token> Samples(GridGeometry grid, byte[,] cells, IReadOnlyList<SequenceLine> found)
    {
        var samples = new List<Token>();
        for (var r = 0; r < grid.Size; r++)
        {
            for (var c = 0; c < grid.Size; c++)
            {
                if (cells[r, c] != BreachCodes.Unknown)
                {
                    samples.Add(new Token(cells[r, c], grid.CellBox(r, c)));
                }
            }
        }

        foreach (var line in found)
        {
            samples.AddRange(line.Codes.Zip(line.Boxes, (code, box) => new Token(code, box)));
        }

        return samples;
    }

    private static void Extend(GlyphFeatures glyphs, List<Token> samples, List<byte> codes, List<Box> boxes, double xPitch, CancellationToken cancellationToken)
    {
        var screen = glyphs.Image;
        while (codes.Count < MaxCodesPerLine)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = boxes[^1].Offset(xPitch, 0);
            if (next.Right > screen.Width)
            {
                return;
            }

            var code = GlyphMatcher.Classify(glyphs, next, samples);
            if (code == BreachCodes.Unknown)
            {
                return;
            }

            codes.Add(code);
            boxes.Add(next);
        }
    }

    private static double LinePitch(List<double> ys, double gridPitch)
    {
        var gaps = ys.Zip(ys.Skip(1), (a, b) => b - a).Where(g => g > 0).ToList();
        if (gaps.Count == 0)
        {
            return gridPitch;
        }

        var smallest = gaps.Min();
        var multiple = Math.Max(1, Math.Round(smallest / gridPitch));
        return smallest / multiple;
    }

    private static double? SnapLine(BgraImage screen, double centerX, double guessY, double tokenWidth, double tokenHeight, double yPitch)
    {
        var x0 = Math.Clamp((int)Math.Round(centerX - (tokenWidth / 2)), 0, screen.Width - 1);
        var x1 = Math.Clamp((int)Math.Round(centerX + (tokenWidth / 2)), 0, screen.Width - 1);
        var y0 = Math.Clamp((int)Math.Round(guessY - (0.45 * yPitch)), 0, screen.Height - 1);
        var y1 = Math.Clamp((int)Math.Round(guessY + (0.45 * yPitch)), 0, screen.Height - 1);
        if (x1 <= x0 || y1 <= y0)
        {
            return null;
        }

        var rowMax = new int[y1 - y0 + 1];
        int min = 255, max = 0;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var v = screen.Brightness(x, y);
                rowMax[y - y0] = Math.Max(rowMax[y - y0], v);
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        }

        if (max - min < 30)
        {
            return null;
        }

        var threshold = min + ((max - min) / 2);
        double? best = null;
        var start = -1;
        for (var i = 0; i <= rowMax.Length; i++)
        {
            var ink = i < rowMax.Length && rowMax[i] >= threshold;
            if (ink && start < 0)
            {
                start = i;
            }
            else if (!ink && start >= 0)
            {
                var height = i - start;
                var center = y0 + start + (height / 2.0);
                if (height >= tokenHeight * 0.5 && height <= tokenHeight * 1.5
                    && (best is null || Math.Abs(center - guessY) < Math.Abs(best.Value - guessY)))
                {
                    best = center;
                }

                start = -1;
            }
        }

        return best;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
