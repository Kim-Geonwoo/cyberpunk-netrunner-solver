using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public sealed record OcrWord(string Text, Box Box);

// Votes: 같은 자리·같은 코드로 읽힌 횟수. TokenPool이 겹친 인식을 한 토큰으로 합칠 때 표를 잃지 않게 한다.
public readonly record struct Token(byte Code, Box Box, int Votes = 1);

public interface IOcrEngine
{
    string Language { get; }

    Task<IReadOnlyList<OcrWord>> RecognizeAsync(BgraImage image, CancellationToken cancellationToken);
}
