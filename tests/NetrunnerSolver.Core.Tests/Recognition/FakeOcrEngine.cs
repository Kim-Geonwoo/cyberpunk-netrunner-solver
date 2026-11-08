using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

internal sealed class FakeOcrEngine(
    string language,
    IReadOnlyList<OcrWord> truth,
    int fullWidth,
    int fullHeight,
    double blindAboveHeight = double.MaxValue,
    Func<OcrWord, bool>? hideInFullImage = null) : IOcrEngine
{
    public string Language { get; } = language;

    public int Calls { get; private set; }

    public Task<IReadOnlyList<OcrWord>> RecognizeAsync(BgraImage image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        if (image.Height > blindAboveHeight)
        {
            return Task.FromResult<IReadOnlyList<OcrWord>>([]);
        }

        var isFull = image.OffsetX == 0 && image.OffsetY == 0 && image.Width == fullWidth && image.Height == fullHeight;
        var area = new Box(image.OffsetX, image.OffsetY, image.Width, image.Height);
        IReadOnlyList<OcrWord> words = [.. truth
            .Where(w => area.Contains(w.Box.CenterX, w.Box.CenterY))
            .Where(w => !isFull || hideInFullImage is null || !hideInFullImage(w))
            .Select(w => w with { Box = w.Box.Offset(-image.OffsetX, -image.OffsetY) })];
        return Task.FromResult(words);
    }
}

internal sealed class ThrowingOcrEngine : IOcrEngine
{
    public string Language => "xx";

    public Task<IReadOnlyList<OcrWord>> RecognizeAsync(BgraImage image, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("engine broke");
}
