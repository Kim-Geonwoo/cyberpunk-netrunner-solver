using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public static class LayoutParser
{
    private const double LatticeTolerance = 0.25;
    private const double CellTolerance = 0.3;
    private const double MinFill = 0.4;
    private const int MinSize = 4;
    private const double MaxPitchRatio = 1.33;
    private const int MaxSize = 8;
    private const int MaxSequenceLines = 6;

    // 기준 토큰에서 이만큼 넘게 떨어진 칸 번호는 격자일 수 없다. int로 바꾸기 전에 거른다.
    private const double MaxLatticeOffset = 1 << 16;

    // 격자 맞추기는 토큰 수의 제곱~세제곱이다. 판독 시간 제한이 실제로 계산을 끊도록 반복마다 취소를 본다.
    public static GridGeometry? FindMatrix(IReadOnlyList<Token> tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Count < 4)
        {
            return null;
        }

        GridGeometry? best = null;
        var bestScore = 0;
        var triedHeights = new HashSet<long>();
        foreach (var seed in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!triedHeights.Add((long)Math.Round(seed.Box.Height)))
            {
                continue;
            }

            var group = tokens.Where(t => Math.Abs(t.Box.Height - seed.Box.Height) <= seed.Box.Height * 0.2).ToList();
            var (grid, score) = FitGrid(group, cancellationToken);
            if (grid is not null && score > bestScore)
            {
                best = grid;
                bestScore = score;
            }
        }

        return best;
    }

    public static byte[,] ReadCells(GridGeometry grid, IEnumerable<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(tokens);
        var votes = new Dictionary<byte, int>[grid.Size, grid.Size];
        foreach (var token in tokens)
        {
            if (!grid.TryLocate(token.Box.CenterX, token.Box.CenterY, CellTolerance, out var cell)
                || !IsNearCellSize(grid, token))
            {
                continue;
            }

            var tally = votes[cell.Row, cell.Col] ??= [];
            tally[token.Code] = tally.GetValueOrDefault(token.Code) + token.Votes;
        }

        var cells = new byte[grid.Size, grid.Size];
        for (var r = 0; r < grid.Size; r++)
        {
            for (var c = 0; c < grid.Size; c++)
            {
                cells[r, c] = Winner(votes[r, c]);
            }
        }

        return cells;
    }

    public static IReadOnlyList<SequenceLine> FindSequences(GridGeometry grid, IReadOnlyList<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(tokens);
        var bounds = grid.Bounds;
        var candidates = tokens
            .Where(t => t.Box.CenterX > bounds.Right + (grid.PitchX / 2)
                && t.Box.CenterY >= bounds.Y - grid.PitchY
                && t.Box.CenterY <= bounds.Bottom + (3 * grid.PitchY)
                && t.Box.Height <= grid.CellHeight * 1.4)
            .ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        var height = Median(candidates.Select(t => t.Box.Height));
        var width = Median(candidates.Select(t => t.Box.Width));
        var lines = GroupLines(candidates, height * 0.5)
            .Select(line => MergeSamePosition(line, width * 0.5))
            .Where(line => line.Count > 0)
            .ToList();
        if (lines.Count == 0)
        {
            return [];
        }

        var edges = lines.Select(l => l[0].CenterX).ToList();
        var edge = edges
            .Select(x => (X: x, Count: edges.Count(o => Math.Abs(o - x) <= width)))
            .OrderByDescending(e => e.Count)
            .ThenBy(e => e.X)
            .First().X;

        var result = new List<SequenceLine>();
        foreach (var line in lines.Where(l => Math.Abs(l[0].CenterX - edge) <= width).Take(MaxSequenceLines))
        {
            var codes = new List<byte> { line[0].Code };
            var boxes = new List<Box> { line[0].Box };
            for (var i = 1; i < line.Count && line[i].CenterX - line[i - 1].CenterX <= width * 2.2; i++)
            {
                codes.Add(line[i].Code);
                boxes.Add(line[i].Box);
            }

            result.Add(new SequenceLine(codes, boxes));
        }

        return result;
    }

    public static double FillRatio(byte[,] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        return cells.Length == 0 ? 0 : (double)cells.Cast<byte>().Count(c => c != BreachCodes.Unknown) / cells.Length;
    }

    private static (GridGeometry? Grid, int Score) FitGrid(List<Token> group, CancellationToken cancellationToken)
    {
        if (group.Count < 4)
        {
            return (null, 0);
        }

        var height = Median(group.Select(t => t.Box.Height));
        var width = Median(group.Select(t => t.Box.Width));
        var pitchX = DominantGap(group, t => t.Box.CenterX, t => t.Box.CenterY, height * 0.5);
        var pitchY = DominantGap(group, t => t.Box.CenterY, t => t.Box.CenterX, width * 0.5);
        if (pitchX is null && pitchY is null)
        {
            return (null, 0);
        }

        var px = pitchX ?? pitchY!.Value;
        var py = pitchY ?? px;
        if (px < width || py < height || Math.Max(px, py) / Math.Min(px, py) > MaxPitchRatio)
        {
            return (null, 0);
        }

        List<(Token Token, int Row, int Col)>? bestLattice = null;
        foreach (var anchor in group)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lattice = new List<(Token, int, int)>();
            foreach (var token in group)
            {
                var fx = (token.Box.CenterX - anchor.Box.CenterX) / px;
                var fy = (token.Box.CenterY - anchor.Box.CenterY) / py;
                if (Math.Abs(fx) <= MaxLatticeOffset && Math.Abs(fy) <= MaxLatticeOffset
                    && Math.Abs(fx - Math.Round(fx)) <= LatticeTolerance && Math.Abs(fy - Math.Round(fy)) <= LatticeTolerance)
                {
                    lattice.Add((token, (int)Math.Round(fy), (int)Math.Round(fx)));
                }
            }

            if (bestLattice is null || lattice.Count > bestLattice.Count)
            {
                bestLattice = lattice;
            }
        }

        var component = LargestComponent(bestLattice!, cancellationToken);
        var minRow = component.Min(e => e.Row);
        var maxRow = component.Max(e => e.Row);
        var minCol = component.Min(e => e.Col);
        var maxCol = component.Max(e => e.Col);
        var rowSpan = maxRow - minRow + 1;
        var colSpan = maxCol - minCol + 1;
        var size = Math.Max(rowSpan, colSpan);
        var occupied = component.Select(e => (e.Row, e.Col)).Distinct().Count();
        if (size is < MinSize or > MaxSize || occupied < (double)rowSpan * colSpan * MinFill)
        {
            return (null, 0);
        }

        var (originX, fittedPx) = Fit(component.Select(e => (e.Col - minCol, e.Token.Box.CenterX)).ToList(), px);
        var (originY, fittedPy) = Fit(component.Select(e => (e.Row - minRow, e.Token.Box.CenterY)).ToList(), py);
        if (!IsPositiveFinite(fittedPx) || !IsPositiveFinite(fittedPy) || !double.IsFinite(originX) || !double.IsFinite(originY))
        {
            return (null, 0);
        }

        var cellWidth = Median(component.Select(e => e.Token.Box.Width));
        var cellHeight = Median(component.Select(e => e.Token.Box.Height));
        var grid = new GridGeometry(size, originX, originY, fittedPx, fittedPy, cellWidth, cellHeight, size - rowSpan, size - colSpan);
        return (grid, occupied);
    }

    private static double? DominantGap(List<Token> tokens, Func<Token, double> along, Func<Token, double> across, double sameLine)
    {
        var gaps = new List<double>();
        foreach (var line in GroupBy(tokens, across, sameLine))
        {
            var sorted = line.Select(along).OrderBy(v => v).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                var gap = sorted[i] - sorted[i - 1];
                if (gap > sameLine)
                {
                    gaps.Add(gap);
                }
            }
        }

        if (gaps.Count == 0)
        {
            return null;
        }

        return gaps
            .Select(g => (Gap: g, Support: gaps.Where(o => Math.Abs(o - g) <= g * 0.12).ToList()))
            .OrderByDescending(c => c.Support.Count)
            .ThenBy(c => c.Gap)
            .Select(c => c.Support.Average())
            .First();
    }

    private static List<(Token Token, int Row, int Col)> LargestComponent(List<(Token Token, int Row, int Col)> lattice, CancellationToken cancellationToken)
    {
        var remaining = new List<(Token Token, int Row, int Col)>(lattice);
        List<(Token Token, int Row, int Col)> best = [];
        while (remaining.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var component = new List<(Token Token, int Row, int Col)> { remaining[0] };
            remaining.RemoveAt(0);
            for (var i = 0; i < component.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = component[i];
                var neighbours = remaining.Where(e => Math.Abs(e.Row - current.Row) <= 2 && Math.Abs(e.Col - current.Col) <= 2).ToList();
                foreach (var n in neighbours)
                {
                    remaining.Remove(n);
                    component.Add(n);
                }
            }

            if (component.Count > best.Count)
            {
                best = component;
            }
        }

        return best;
    }

    private static (double Origin, double Pitch) Fit(List<(int Index, double Value)> samples, double fallbackPitch)
    {
        var distinct = samples.Select(s => s.Index).Distinct().Count();
        if (distinct < 2)
        {
            return (samples.Average(s => s.Value - (s.Index * fallbackPitch)), fallbackPitch);
        }

        var meanIndex = samples.Average(s => s.Index);
        var meanValue = samples.Average(s => s.Value);
        var slope = samples.Sum(s => (s.Index - meanIndex) * (s.Value - meanValue)) / samples.Sum(s => Math.Pow(s.Index - meanIndex, 2));
        return (meanValue - (slope * meanIndex), slope);
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;

    private static bool IsNearCellSize(GridGeometry grid, Token token) =>
        token.Box.Height >= grid.CellHeight * 0.6 && token.Box.Height <= grid.CellHeight * 1.4;

    private static byte Winner(Dictionary<byte, int>? tally)
    {
        if (tally is null || tally.Count == 0)
        {
            return BreachCodes.Unknown;
        }

        var ordered = tally.OrderByDescending(kv => kv.Value).ToList();
        return ordered.Count > 1 && ordered[0].Value == ordered[1].Value ? BreachCodes.Unknown : ordered[0].Key;
    }

    private static List<List<Token>> GroupLines(List<Token> tokens, double tolerance) =>
        [.. GroupBy(tokens, t => t.Box.CenterY, tolerance).Select(line => line.OrderBy(t => t.Box.CenterX).ToList())];

    private static List<List<Token>> GroupBy(IEnumerable<Token> tokens, Func<Token, double> key, double tolerance)
    {
        var groups = new List<List<Token>>();
        foreach (var token in tokens.OrderBy(key))
        {
            var last = groups.Count > 0 ? groups[^1] : null;
            if (last is not null && Math.Abs(key(token) - last.Average(key)) <= tolerance)
            {
                last.Add(token);
            }
            else
            {
                groups.Add([token]);
            }
        }

        return groups;
    }

    private static List<(byte Code, Box Box, double CenterX)> MergeSamePosition(List<Token> line, double tolerance)
    {
        var merged = new List<(byte Code, Box Box, double CenterX)>();
        foreach (var cluster in GroupBy(line, t => t.Box.CenterX, tolerance))
        {
            var code = Winner(cluster.GroupBy(t => t.Code).ToDictionary(g => g.Key, g => g.Sum(t => t.Votes)));
            if (code != BreachCodes.Unknown)
            {
                merged.Add((code, cluster[0].Box, cluster.Average(t => t.Box.CenterX)));
            }
        }

        return merged;
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
