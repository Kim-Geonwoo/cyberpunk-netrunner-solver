using System.Globalization;
using System.Text.Json;
using NetrunnerSolver.Windows.Capture;
using NetrunnerSolver.Windows.Imaging;
using NetrunnerSolver.Windows.Ocr;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: OcrDump <image> <outDir> | OcrDump --capture <outDir>");
    return 2;
}

if (args[0] == "--capture")
{
    var area = MonitorLocator.Primary();
    var shot = ScreenCapture.Capture(area, out var method);
    var file = Path.Combine(args[1], $"capture-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png");
    await ImageFile.SavePngAsync(shot, file);
    Console.WriteLine($"{file}: {shot.Width}x{shot.Height} via {method}, scale {area.DpiScale}");
    return 0;
}

var imagePath = args[0];
var outDir = args[1];
if (!File.Exists(imagePath))
{
    Console.Error.WriteLine($"image not found: {imagePath}");
    return 1;
}

var image = await ImageFile.LoadAsync(imagePath);
var engines = OcrEngines.CreateAvailable();
if (engines.Count == 0)
{
    Console.Error.WriteLine("no Windows OCR language is installed");
    return 1;
}

Directory.CreateDirectory(outDir);
var name = Path.GetFileNameWithoutExtension(imagePath);
var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
foreach (var engine in engines)
{
    try
    {
        var words = await engine.RecognizeAsync(image, CancellationToken.None);
        var dump = new
        {
            Image = name,
            image.Width,
            image.Height,
            engine.Language,
            Words = words.Select(w => new
            {
                w.Text,
                X = (int)Math.Round(w.Box.X),
                Y = (int)Math.Round(w.Box.Y),
                W = (int)Math.Round(w.Box.Width),
                H = (int)Math.Round(w.Box.Height),
            }),
        };
        var path = Path.Combine(outDir, $"{name}.{engine.Language}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(dump, options) + "\n");
        Console.WriteLine($"{path}: {words.Count} words");
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"{engine.Language}: {ex.Message}");
        return 1;
    }
}

return 0;
