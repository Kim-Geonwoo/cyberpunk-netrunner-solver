using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;
using NetrunnerSolver.Windows.Imaging;
using Windows.Media.Ocr;
using CoreOcrWord = NetrunnerSolver.Core.Recognition.OcrWord;

namespace NetrunnerSolver.Windows.Ocr;

public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly OcrEngine _engine;

    public WindowsOcrEngine(OcrEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        Language = engine.RecognizerLanguage.LanguageTag;
    }

    public string Language { get; }

    public async Task<IReadOnlyList<CoreOcrWord>> RecognizeAsync(BgraImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        var max = OcrEngine.MaxImageDimension;
        if (image.Width > max || image.Height > max)
        {
            throw new ArgumentException($"Image {image.Width}x{image.Height} exceeds the OCR limit of {max}px.", nameof(image));
        }

        using var bitmap = ImageFile.ToSoftwareBitmap(image);
        var result = await _engine.RecognizeAsync(bitmap).AsTask(cancellationToken).ConfigureAwait(false);
        return [.. result.Lines
            .SelectMany(line => line.Words)
            .Select(word => new CoreOcrWord(
                word.Text,
                new Box(word.BoundingRect.X, word.BoundingRect.Y, word.BoundingRect.Width, word.BoundingRect.Height)))];
    }
}
