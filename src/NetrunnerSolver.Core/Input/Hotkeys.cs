using System.Globalization;

namespace NetrunnerSolver.Core.Input;

public sealed record HotkeyChord(IReadOnlyList<int> VirtualKeys)
{
    public const int MaxKeys = 3;

    public static HotkeyChord Default { get; } = new([0xC0, 0x20]);

    public bool IsValid => VirtualKeys.Count is > 0 and <= MaxKeys && VirtualKeys.All(vk => vk is > 0 and < 0xFF) && VirtualKeys.Distinct().Count() == VirtualKeys.Count;

    public string ToDisplayString() => string.Join('+', VirtualKeys.Select(Name));

    private static string Name(int vk) => vk switch
    {
        0xC0 => "`",
        0x20 => "Space",
        0x10 or 0xA0 or 0xA1 => "Shift",
        0x11 or 0xA2 or 0xA3 => "Ctrl",
        0x12 or 0xA4 or 0xA5 => "Alt",
        0x09 => "Tab",
        0x0D => "Enter",
        0x1B => "Esc",
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F).ToString(CultureInfo.InvariantCulture),
        (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A) => ((char)vk).ToString(),
        _ => "VK_" + vk.ToString("X2", CultureInfo.InvariantCulture),
    };
}

public sealed class ChordRecorder
{
    private readonly List<int> _keys = [];
    private readonly HashSet<int> _down = [];

    public HotkeyChord? Result { get; private set; }

    public void KeyDown(int virtualKey)
    {
        var key = Normalize(virtualKey);
        if (_down.Count == 0)
        {
            _keys.Clear();
        }

        if (_down.Add(key) && !_keys.Contains(key) && _keys.Count < HotkeyChord.MaxKeys)
        {
            _keys.Add(key);
        }
    }

    public void KeyUp(int virtualKey)
    {
        _down.Remove(Normalize(virtualKey));
        if (_down.Count == 0 && _keys.Count > 0)
        {
            Result = new HotkeyChord([.. _keys]);
        }
    }

    private static int Normalize(int virtualKey) => virtualKey switch
    {
        0xA0 or 0xA1 => 0x10,
        0xA2 or 0xA3 => 0x11,
        0xA4 or 0xA5 => 0x12,
        _ => virtualKey,
    };
}

public sealed class ChordDetector(HotkeyChord chord)
{
    private const long DebounceMs = 200;

    private readonly HotkeyChord _chord = chord ?? throw new ArgumentNullException(nameof(chord));
    private bool _armed = true;
    private long? _lastFire;

    public bool Update(Func<int, bool> isDown, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(isDown);
        if (_chord.VirtualKeys.Count == 0 || !_chord.VirtualKeys.All(isDown))
        {
            _armed = true;
            return false;
        }

        if (!_armed)
        {
            return false;
        }

        _armed = false;
        if (_lastFire is { } last && nowMs - last < DebounceMs)
        {
            return false;
        }

        _lastFire = nowMs;
        return true;
    }
}
