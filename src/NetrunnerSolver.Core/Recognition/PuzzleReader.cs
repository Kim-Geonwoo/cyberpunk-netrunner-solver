using System.Diagnostics;
using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public enum ReadWarning
{
    UnknownCells,
    BufferNotDetected,
    BufferHasContent,
    NoSequences,
    UsedStripScan,
    EngineError,
    CursorCovering,
    TokenLimitReached,
}

public enum ReadOutcome
{
    NotFound,
    CursorCovered,
    Solved,
}

public sealed record PuzzleReading(
    GridGeometry? Grid,
    CodeMatrix? Matrix,
    IReadOnlyList<SequenceLine> Sequences,
    BufferReading? Buffer,
    IReadOnlyList<ReadWarning> Warnings,
    TimeSpan Elapsed)
{
    // 판독에 쓴 서로 다른 토큰 수. 로그에 남겨 TokenPool 상한에 얼마나 가까운지 본다.
    public int TokenCount { get; init; }

    public ReadOutcome Outcome => Grid is null || Matrix is null
        ? ReadOutcome.NotFound
        : Warnings.Contains(ReadWarning.CursorCovering) ? ReadOutcome.CursorCovered : ReadOutcome.Solved;
}

public sealed class PuzzleReader
{
    private const double MinFillBeforeStripScan = 0.5;
    private const int StripsPerScreen = 18;
    private const int MaxGridSize = 8;
    private const double MinEdgeGlyphRatio = 0.6;

    private readonly IReadOnlyList<IOcrEngine> _engines;

    public PuzzleReader(IReadOnlyList<IOcrEngine> engines)
    {
        _engines = engines ?? throw new ArgumentNullException(nameof(engines));
    }

    public async Task<PuzzleReading> ReadAsync(BgraImage screen, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(screen);
        cancellationToken.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        var warnings = new List<ReadWarning>();
        var tokens = new TokenPool();
        tokens.AddRange(await RecognizeAsync(screen, new Box(0, 0, screen.Width, screen.Height), warnings, cancellationToken).ConfigureAwait(false));

        var grid = LayoutParser.FindMatrix(tokens.Tokens, cancellationToken);
        if (grid is null || LayoutParser.FillRatio(LayoutParser.ReadCells(grid, tokens.Tokens)) < MinFillBeforeStripScan)
        {
            Warn(warnings, ReadWarning.UsedStripScan);
            var height = Math.Max(8.0, (double)screen.Height / StripsPerScreen);
            for (var y = 0.0; y < screen.Height; y += height / 2)
            {
                tokens.AddRange(await RecognizeAsync(screen, new Box(0, y, screen.Width, height), warnings, cancellationToken).ConfigureAwait(false));
            }

            grid = LayoutParser.FindMatrix(tokens.Tokens, cancellationToken);
        }

        if (grid is null)
        {
            return Finish(new PuzzleReading(null, null, [], null, warnings, clock.Elapsed), tokens, warnings);
        }

        grid = await ResolveAmbiguityAsync(screen, grid, tokens, warnings, cancellationToken).ConfigureAwait(false);
        grid = ExpandEdges(screen, grid, tokens.Tokens, cancellationToken);
        var cells = await ReadCellsAsync(screen, grid, tokens, warnings, cancellationToken).ConfigureAwait(false);
        var sequences = await ReadSequencesAsync(screen, grid, tokens, warnings, cancellationToken).ConfigureAwait(false);
        sequences = SequenceCompleter.Complete(screen, grid, cells, sequences, cancellationToken);
        if (CursorDetector.CoversSequences(screen, grid, sequences))
        {
            Warn(warnings, ReadWarning.CursorCovering);
        }

        var buffer = BufferDetector.Detect(screen, grid);
        if (buffer is null)
        {
            Warn(warnings, ReadWarning.BufferNotDetected);
        }
        else if (buffer.HasContent)
        {
            Warn(warnings, ReadWarning.BufferHasContent);
        }

        return Finish(new PuzzleReading(grid, new CodeMatrix(cells), sequences, buffer, warnings, clock.Elapsed), tokens, warnings);
    }

    private static PuzzleReading Finish(PuzzleReading reading, TokenPool tokens, List<ReadWarning> warnings)
    {
        if (tokens.Truncated)
        {
            Warn(warnings, ReadWarning.TokenLimitReached);
        }

        return reading with { TokenCount = tokens.Count };
    }

    private async Task<GridGeometry> ResolveAmbiguityAsync(BgraImage screen, GridGeometry grid, TokenPool tokens, List<ReadWarning> warnings, CancellationToken cancellationToken)
    {
        if (grid.AmbiguousRows == 0 && grid.AmbiguousCols == 0)
        {
            return grid;
        }

        var observedRows = grid.Size - grid.AmbiguousRows;
        var observedCols = grid.Size - grid.AmbiguousCols;
        var padding = 0.35 * Math.Min(grid.PitchX, grid.PitchY);
        var candidates = new List<GridGeometry>();
        var regions = new HashSet<Box>();
        for (var dr = 0; dr <= grid.AmbiguousRows; dr++)
        {
            for (var dc = 0; dc <= grid.AmbiguousCols; dc++)
            {
                var candidate = grid.Shift(-dr, -dc);
                candidates.Add(candidate);
                for (var r = 0; r < grid.Size; r++)
                {
                    if (r < dr || r >= dr + observedRows)
                    {
                        regions.Add(candidate.RowStrip(r, padding));
                    }
                }

                for (var c = 0; c < grid.Size; c++)
                {
                    if (c < dc || c >= dc + observedCols)
                    {
                        regions.Add(candidate.ColumnStrip(c, padding));
                    }
                }
            }
        }

        foreach (var region in regions)
        {
            tokens.AddRange(await RecognizeAsync(screen, region, warnings, cancellationToken).ConfigureAwait(false));
        }

        return candidates
            .Select((candidate, index) =>
            {
                var cells = LayoutParser.ReadCells(candidate, tokens.Tokens);
                var missing = Enumerable.Range(0, candidate.Size)
                    .SelectMany(r => Enumerable.Range(0, candidate.Size).Select(c => new Cell(r, c)))
                    .Where(cell => cells[cell.Row, cell.Col] == BreachCodes.Unknown);
                return (candidate, index, fill: LayoutParser.FillRatio(cells), hits: GlyphHits(screen, candidate, missing, CellSamples(candidate, cells), cancellationToken));
            })
            .OrderByDescending(c => c.fill)
            .ThenByDescending(c => c.hits)
            .ThenBy(c => c.index)
            .First().candidate;
    }

    private static GridGeometry ExpandEdges(BgraImage screen, GridGeometry grid, IReadOnlyList<Token> tokens, CancellationToken cancellationToken)
    {
        while (grid.Size < MaxGridSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = CellSamples(grid, LayoutParser.ReadCells(grid, tokens));
            if (samples.Count < 4)
            {
                return grid;
            }

            GridGeometry? best = null;
            var bestRatio = 0.0;
            foreach (var (dr, dc) in new[] { (-1, -1), (-1, 0), (0, -1), (0, 0) })
            {
                var candidate = grid.Shift(dr, dc) with { Size = grid.Size + 1 };
                var bounds = candidate.Bounds;
                if (bounds.X < 0 || bounds.Y < 0 || bounds.Right > screen.Width || bounds.Bottom > screen.Height)
                {
                    continue;
                }

                var newRow = dr == -1 ? 0 : grid.Size;
                var newCol = dc == -1 ? 0 : grid.Size;
                var added = Enumerable.Range(0, candidate.Size).Select(c => new Cell(newRow, c))
                    .Concat(Enumerable.Range(0, candidate.Size).Where(r => r != newRow).Select(r => new Cell(r, newCol)))
                    .ToList();
                var ratio = (double)GlyphHits(screen, candidate, added, samples, cancellationToken) / added.Count;
                if (ratio > bestRatio)
                {
                    best = candidate;
                    bestRatio = ratio;
                }
            }

            if (best is null || bestRatio < MinEdgeGlyphRatio)
            {
                return grid;
            }

            grid = best;
        }

        return grid;
    }

    private static List<Token> CellSamples(GridGeometry grid, byte[,] cells)
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

        return samples;
    }

    private static int GlyphHits(BgraImage screen, GridGeometry grid, IEnumerable<Cell> cells, List<Token> samples, CancellationToken cancellationToken) =>
        samples.Count == 0 ? 0 : cells.Count(cell =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return GlyphMatcher.Classify(screen, grid.CellBox(cell.Row, cell.Col), samples) != BreachCodes.Unknown;
        });

    private async Task<byte[,]> ReadCellsAsync(BgraImage screen, GridGeometry grid, TokenPool tokens, List<ReadWarning> warnings, CancellationToken cancellationToken)
    {
        var cells = LayoutParser.ReadCells(grid, tokens.Tokens);
        var padding = 0.35 * grid.PitchY;
        var retried = false;
        for (var r = 0; r < grid.Size; r++)
        {
            if (Enumerable.Range(0, grid.Size).Any(c => cells[r, c] == BreachCodes.Unknown))
            {
                tokens.AddRange(await RecognizeAsync(screen, grid.RowStrip(r, padding), warnings, cancellationToken).ConfigureAwait(false));
                retried = true;
            }
        }

        if (retried)
        {
            cells = LayoutParser.ReadCells(grid, tokens.Tokens);
        }

        bool learned;
        do
        {
            learned = false;
            var samples = CellSamples(grid, cells);
            for (var r = 0; r < grid.Size; r++)
            {
                for (var c = 0; c < grid.Size; c++)
                {
                    if (cells[r, c] != BreachCodes.Unknown)
                    {
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    cells[r, c] = GlyphMatcher.Classify(screen, grid.CellBox(r, c), samples);
                    learned |= cells[r, c] != BreachCodes.Unknown;
                }
            }
        }
        while (learned);

        if (cells.Cast<byte>().Any(c => c == BreachCodes.Unknown))
        {
            Warn(warnings, ReadWarning.UnknownCells);
            for (var r = 0; r < grid.Size; r++)
            {
                for (var c = 0; c < grid.Size; c++)
                {
                    if (cells[r, c] == BreachCodes.Unknown && CursorDetector.Covers(screen, grid, new Cell(r, c)))
                    {
                        Warn(warnings, ReadWarning.CursorCovering);
                    }
                }
            }
        }

        return cells;
    }

    private async Task<IReadOnlyList<SequenceLine>> ReadSequencesAsync(BgraImage screen, GridGeometry grid, TokenPool tokens, List<ReadWarning> warnings, CancellationToken cancellationToken)
    {
        var sequences = LayoutParser.FindSequences(grid, tokens.Tokens);
        if (sequences.Count > 0)
        {
            return sequences;
        }

        var bounds = grid.Bounds;
        var region = new Box(bounds.Right, bounds.Y - grid.PitchY, screen.Width - bounds.Right, bounds.Height + (4 * grid.PitchY));
        tokens.AddRange(await RecognizeAsync(screen, region, warnings, cancellationToken).ConfigureAwait(false));
        sequences = LayoutParser.FindSequences(grid, tokens.Tokens);
        if (sequences.Count == 0)
        {
            Warn(warnings, ReadWarning.NoSequences);
        }

        return sequences;
    }

    private async Task<List<Token>> RecognizeAsync(BgraImage screen, Box region, List<ReadWarning> warnings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BgraImage image;
        if (region.X <= 0 && region.Y <= 0 && region.Right >= screen.Width && region.Bottom >= screen.Height)
        {
            image = screen;
        }
        else
        {
            try
            {
                image = screen.Crop(region);
            }
            catch (ArgumentException)
            {
                return [];
            }
        }

        var results = await Task.WhenAll(_engines.Select(engine => RecognizeOneAsync(engine, image, cancellationToken))).ConfigureAwait(false);
        var dx = image.OffsetX - screen.OffsetX;
        var dy = image.OffsetY - screen.OffsetY;
        var tokens = new List<Token>();
        foreach (var words in results)
        {
            if (words is null)
            {
                Warn(warnings, ReadWarning.EngineError);
                continue;
            }

            foreach (var word in words)
            {
                tokens.AddRange(TokenNormalizer.Normalize(word with { Box = word.Box.Offset(dx, dy) }));
            }
        }

        return tokens;
    }

    private static async Task<IReadOnlyList<OcrWord>?> RecognizeOneAsync(IOcrEngine engine, BgraImage image, CancellationToken cancellationToken)
    {
        try
        {
            return await engine.RecognizeAsync(image, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Warn(List<ReadWarning> warnings, ReadWarning warning)
    {
        if (!warnings.Contains(warning))
        {
            warnings.Add(warning);
        }
    }
}
