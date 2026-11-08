using System.Globalization;

namespace NetrunnerSolver.Core;

public static class BreachCodes
{
    public const byte Unknown = 0x00;

    public static IReadOnlyList<byte> All { get; } = [0x1C, 0x55, 0x7A, 0xBD, 0xE9, 0xFF];

    public static string Format(byte code) =>
        code == Unknown ? "?" : code.ToString("X2", CultureInfo.InvariantCulture);

    public static bool TryParse(string text, out byte code)
    {
        code = Unknown;
        if (text is not { Length: 2 }
            || !byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            || !All.Contains(value))
        {
            return false;
        }

        code = value;
        return true;
    }

    public static byte Next(byte code)
    {
        var index = -1;
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i] == code)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return All[0];
        }

        return index == All.Count - 1 ? Unknown : All[index + 1];
    }
}
