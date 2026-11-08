using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

// 한 번의 판독에서 여러 엔진·영역을 읽어 모은 토큰.
// 같은 자리(1px 단위)·같은 코드는 한 토큰으로 합치고 Votes만 더해, 투표 결과는 그대로 두면서
// 띠 재스캔·줄 재인식이 같은 글자를 거듭 넣어도 목록이 불어나지 않게 한다.
// 격자 맞추기는 토큰 수의 제곱 이상으로 느려지므로, 화면이 코드처럼 읽히는 글자로 가득 찬 경우에 대비해
// 서로 다른 토큰 수에 상한을 둔다. 상한을 넘는 토큰은 버리고 Truncated로 알린다.
internal sealed class TokenPool
{
    // 정상 퍼즐(격자 8×8, 시퀀스 6줄)은 엔진 수와 재인식을 다 곱해도 수백 개 안이다.
    public const int MaxTokens = 4096;

    private readonly List<Token> _tokens = [];
    private readonly Dictionary<(byte Code, long X, long Y, long Width, long Height), int> _index = [];

    public IReadOnlyList<Token> Tokens => _tokens;

    public int Count => _tokens.Count;

    public bool Truncated { get; private set; }

    public void AddRange(IEnumerable<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        foreach (var token in tokens)
        {
            if (!IsFinite(token.Box) || token.Votes <= 0)
            {
                continue;
            }

            var key = (token.Code, Pixel(token.Box.X), Pixel(token.Box.Y), Pixel(token.Box.Width), Pixel(token.Box.Height));
            if (_index.TryGetValue(key, out var index))
            {
                var existing = _tokens[index];
                _tokens[index] = existing with { Votes = existing.Votes + token.Votes };
            }
            else if (_tokens.Count < MaxTokens)
            {
                _index.Add(key, _tokens.Count);
                _tokens.Add(token);
            }
            else
            {
                Truncated = true;
            }
        }
    }

    private static bool IsFinite(Box box) =>
        double.IsFinite(box.X) && double.IsFinite(box.Y) && double.IsFinite(box.Width) && double.IsFinite(box.Height);

    private static long Pixel(double value) => (long)Math.Round(value);
}
