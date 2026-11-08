using System.Text;
using NetrunnerSolver.Core.Imaging;

namespace NetrunnerSolver.Core.Recognition;

public static class TokenNormalizer
{
    private static readonly Dictionary<char, char> Confusables = new()
    {
        ['I'] = '1',
        ['l'] = '1',
        ['i'] = '1',
        ['|'] = '1',
        ['!'] = '1',
        ['L'] = '1',
        ['O'] = 'D',
        ['o'] = 'D',
        ['0'] = 'D',
        ['Q'] = 'D',
        ['S'] = '5',
        ['s'] = '5',
        ['$'] = '5',
        ['8'] = 'B',
        ['g'] = '9',
        ['q'] = '9',
        ['€'] = 'E',
        ['T'] = '7',
        ['4'] = 'A',
        ['c'] = 'C',
        ['('] = 'C',
        ['f'] = 'F',
    };

    private const string Dropped = ".,:;'\"`-_";

    public static IReadOnlyList<Token> Normalize(OcrWord word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var builder = new StringBuilder(word.Text.Length);
        foreach (var raw in word.Text)
        {
            if (char.IsWhiteSpace(raw) || Dropped.Contains(raw, StringComparison.Ordinal))
            {
                continue;
            }

            var mapped = Confusables.TryGetValue(raw, out var replacement) ? replacement : raw;
            builder.Append(char.ToUpperInvariant(mapped));
        }

        var text = builder.ToString();
        if (text.Length < 2 || text.Length % 2 != 0)
        {
            return [];
        }

        var count = text.Length / 2;
        var codes = new byte[count];
        for (var i = 0; i < count; i++)
        {
            if (!BreachCodes.TryParse(text.Substring(i * 2, 2), out codes[i]))
            {
                return [];
            }
        }

        var width = word.Box.Width / count;
        return [.. codes.Select((code, i) => new Token(code, new Box(word.Box.X + (width * i), word.Box.Y, width, word.Box.Height)))];
    }
}
