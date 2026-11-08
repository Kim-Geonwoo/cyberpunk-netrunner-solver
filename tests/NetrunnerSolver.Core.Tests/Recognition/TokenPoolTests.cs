using System.Collections;
using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

public class TokenPoolTests
{
    private static readonly GridGeometry Grid = new(5, 100, 200, 40, 50, 24, 20, 0, 0);

    [Fact]
    public void SamePlaceAndCodeMergeIntoOneTokenWithTheirVotes()
    {
        var box = Grid.CellBox(1, 1);
        var pool = new TokenPool();

        pool.AddRange([new Token(0x1C, box), new Token(0x1C, box.Offset(0.2, -0.2)), new Token(0x55, box)]);

        Assert.Equal(2, pool.Count);
        Assert.Equal(2, pool.Tokens.Single(t => t.Code == 0x1C).Votes);
        Assert.Equal(1, pool.Tokens.Single(t => t.Code == 0x55).Votes);
        Assert.False(pool.Truncated);
    }

    [Fact]
    public void MergedVotesStillDecideTheCell()
    {
        // 같은 엔진이 전체 화면과 줄 띠에서 같은 글자를 두 번 읽으면 두 표다. 합쳐도 표는 그대로여야 한다.
        var box = Grid.CellBox(1, 1);
        var pool = new TokenPool();

        pool.AddRange([new Token(0x1C, box), new Token(0x1C, box), new Token(0x55, box.Offset(1, 0))]);

        Assert.Equal(2, pool.Count);
        Assert.Equal(0x1C, LayoutParser.ReadCells(Grid, pool.Tokens)[1, 1]);
    }

    [Fact]
    public void RereadingTheSameScreenAddsVotesNotTokens()
    {
        var once = OcrFixture.Tokens("game-ko-5x5-a.ko");
        var pool = new TokenPool();

        pool.AddRange(once);
        var distinct = pool.Count;
        pool.AddRange(once);
        pool.AddRange(once);

        Assert.Equal(distinct, pool.Count);
        var grid = LayoutParser.FindMatrix(pool.Tokens);
        Assert.NotNull(grid);
        OcrFixture.AssertCells(OcrFixture.MatrixA, LayoutParser.ReadCells(grid, pool.Tokens));
        OcrFixture.AssertSequences(OcrFixture.SequencesA, LayoutParser.FindSequences(grid, pool.Tokens).Select(s => s.Codes));
    }

    [Fact]
    public void DistinctTokensStopAtTheCap()
    {
        var pool = new TokenPool();

        pool.AddRange(Enumerable.Range(0, TokenPool.MaxTokens + 10).Select(i => new Token(0x1C, new Box(i * 30, 0, 24, 20))));
        pool.AddRange([new Token(0x1C, new Box(0, 0, 24, 20))]);

        Assert.Equal(TokenPool.MaxTokens, pool.Count);
        Assert.True(pool.Truncated);
        Assert.Equal(2, pool.Tokens[0].Votes);
    }

    [Fact]
    public void TokensWithoutAFinitePositionAreDropped()
    {
        var pool = new TokenPool();

        pool.AddRange([
            new Token(0x1C, new Box(double.NaN, 0, 24, 20)),
            new Token(0x1C, new Box(0, double.PositiveInfinity, 24, 20)),
            new Token(0x1C, new Box(0, 0, 24, 20), Votes: 0),
            new Token(0x55, new Box(0, 0, 24, 20)),
        ]);

        Assert.Equal([0x55], pool.Tokens.Select(t => t.Code));
    }

    [Fact]
    public void FindMatrixStopsWhenCancelledMidway()
    {
        // 토큰 목록을 한 바퀴 다 읽은 순간(첫 격자 맞추기 직전) 취소한다. 그 뒤 계산이 취소를 봐야 한다.
        using var cts = new CancellationTokenSource();
        var tokens = new CancelAfterFirstPass(OcrFixture.Tokens("game-ko-5x5-a.ko"), cts);

        Assert.Throws<OperationCanceledException>(() => LayoutParser.FindMatrix(tokens, cts.Token));
        Assert.NotNull(LayoutParser.FindMatrix(OcrFixture.Tokens("game-ko-5x5-a.ko"), CancellationToken.None));
    }

    [Fact]
    public void LatticeAlignedTokenFarAwayDoesNotDisturbTheGrid()
    {
        var tokens = OcrFixture.Tokens("game-ko-5x5-a.ko");
        var grid = LayoutParser.FindMatrix(tokens)!;
        var far = grid.CellBox(0, 0).Offset(grid.PitchX * 4e9, grid.PitchY * 4e9);

        var again = LayoutParser.FindMatrix([.. tokens, new Token(0x1C, far)]);

        Assert.NotNull(again);
        Assert.Equal(grid.Size, again.Size);
        OcrFixture.AssertCells(OcrFixture.MatrixA, LayoutParser.ReadCells(again, tokens));
    }

    private sealed class CancelAfterFirstPass(IReadOnlyList<Token> inner, CancellationTokenSource source) : IReadOnlyList<Token>
    {
        private int _passes;

        public int Count => inner.Count;

        public Token this[int index] => inner[index];

        public IEnumerator<Token> GetEnumerator()
        {
            foreach (var token in inner)
            {
                yield return token;
            }

            if (++_passes == 1)
            {
                source.Cancel();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
