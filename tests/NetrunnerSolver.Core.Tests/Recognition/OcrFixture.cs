using System.Text.Json;
using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;

namespace NetrunnerSolver.Core.Tests.Recognition;

internal static class OcrFixture
{
    public static readonly byte[,] MatrixA =
    {
        { 0xE9, 0xE9, 0x1C, 0x55, 0x1C },
        { 0xBD, 0x1C, 0xBD, 0x55, 0xBD },
        { 0x1C, 0x55, 0xE9, 0xBD, 0xE9 },
        { 0xBD, 0xBD, 0x1C, 0x1C, 0xBD },
        { 0xBD, 0x55, 0x1C, 0x55, 0x1C },
    };

    public static readonly byte[][] SequencesA = [[0x1C, 0x55], [0x1C, 0x1C], [0xBD, 0xBD, 0x1C]];

    public static readonly byte[,] MatrixB =
    {
        { 0x1C, 0xBD, 0x1C, 0x55, 0xE9 },
        { 0x1C, 0xBD, 0xBD, 0x55, 0xE9 },
        { 0xE9, 0x55, 0xE9, 0xBD, 0x55 },
        { 0xBD, 0xBD, 0x1C, 0x1C, 0x1C },
        { 0x55, 0x1C, 0x1C, 0x55, 0x1C },
    };

    public static readonly byte[][] SequencesB = [[0xBD, 0x55], [0x1C, 0xE9], [0xE9, 0xBD, 0x1C]];

    public static (int Width, int Height, IReadOnlyList<OcrWord> Words) Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ocr", name + ".json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var words = root.GetProperty("words").EnumerateArray()
            .Select(w => new OcrWord(
                w.GetProperty("text").GetString() ?? string.Empty,
                new Box(w.GetProperty("x").GetInt32(), w.GetProperty("y").GetInt32(), w.GetProperty("w").GetInt32(), w.GetProperty("h").GetInt32())))
            .ToList();
        return (root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32(), words);
    }

    public static List<Token> Tokens(string name) => [.. Load(name).Words.SelectMany(TokenNormalizer.Normalize)];

    public static void AssertCells(byte[,] expected, byte[,] actual)
    {
        Assert.Equal(expected.GetLength(0), actual.GetLength(0));
        Assert.Equal(expected.GetLength(1), actual.GetLength(1));
        Assert.Equal(expected.Cast<byte>(), actual.Cast<byte>());
    }

    public static void AssertSequences(byte[][] expected, IEnumerable<IReadOnlyList<byte>> actual) =>
        Assert.Equal(expected.Select(Convert.ToHexString), actual.Select(s => Convert.ToHexString([.. s])));
}
