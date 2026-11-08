namespace NetrunnerSolver.Windows.Tests;

internal static class TestFixtures
{
    private static readonly Lazy<string?> Root = new(FindRoot);

    public static string Path(string name) =>
        System.IO.Path.Combine(Root.Value ?? AppContext.BaseDirectory, ".local", "fixtures", name);

    public static bool Exists(string name) => File.Exists(Path(name));

    private static string? FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "CyberpunkNetrunnerSolver.slnx")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class FixtureFactAttribute : FactAttribute
{
    private string? _ocrLanguage;

    public FixtureFactAttribute(params string[] names)
    {
        var missing = names.Where(n => !TestFixtures.Exists(n)).ToArray();
        if (missing.Length > 0)
        {
            Skip = $"Local fixture missing: {string.Join(", ", missing)}";
        }
    }

    public string? OcrLanguage
    {
        get => _ocrLanguage;
        set
        {
            _ocrLanguage = value;
            if (value is not null && Skip is null && Ocr.OcrEngines.CreateAvailable(value).Count == 0)
            {
                Skip = $"Windows OCR language not installed: {value}";
            }
        }
    }
}
