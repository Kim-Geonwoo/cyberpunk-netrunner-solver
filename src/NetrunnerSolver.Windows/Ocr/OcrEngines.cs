using NetrunnerSolver.Core.Recognition;
using Windows.Media.Ocr;

namespace NetrunnerSolver.Windows.Ocr;

public static class OcrEngines
{
    public static IReadOnlyList<IOcrEngine> CreateAvailable(string? onlyLanguage = null)
    {
        var engines = new List<IOcrEngine>();
        var languages = OcrEngine.AvailableRecognizerLanguages
            .Where(l => onlyLanguage is null || string.Equals(l.LanguageTag, onlyLanguage, StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
        foreach (var language in languages)
        {
            var engine = OcrEngine.TryCreateFromLanguage(language);
            if (engine is not null)
            {
                engines.Add(new WindowsOcrEngine(engine));
            }
        }

        return engines;
    }
}
