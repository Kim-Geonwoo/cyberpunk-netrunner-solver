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

// 정해진 번째 호출의 결과를 돌려준 직후 읽기를 취소한다. OCR 경계가 아닌 계산 단계에서 취소가 먹는지 본다.
internal sealed class CancelOnCallEngine(IOcrEngine inner, CancellationTokenSource source, int cancelOnCall) : IOcrEngine
{
    private int _calls;

    public string Language => inner.Language;

    public async Task<IReadOnlyList<OcrWord>> RecognizeAsync(BgraImage image, CancellationToken cancellationToken)
    {
        var words = await inner.RecognizeAsync(image, cancellationToken).ConfigureAwait(false);
        if (Interlocked.Increment(ref _calls) == cancelOnCall)
        {
            await source.CancelAsync().ConfigureAwait(false);
        }

        return words;
    }
}

internal sealed class ThrowingOcrEngine : IOcrEngine
{
    public string Language => "xx";

    public Task<IReadOnlyList<OcrWord>> RecognizeAsync(BgraImage image, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("engine broke");
}
