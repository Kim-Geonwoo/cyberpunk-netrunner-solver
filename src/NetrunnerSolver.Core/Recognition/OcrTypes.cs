using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public sealed record OcrWord(string Text, Box Box);

public readonly record struct Token(byte Code, Box Box);

public interface IOcrEngine
{
    string Language { get; }

    Task<IReadOnlyList<OcrWord>> RecognizeAsync(BgraImage image, CancellationToken cancellationToken);
}
