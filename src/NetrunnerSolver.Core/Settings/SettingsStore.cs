using System.Text.Json;
using NetrunnerSolver.Core.Input;
using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.Core.Settings;

public sealed record AppSettings(HotkeyChord Hotkey, int LastBufferSize, string Language)
{
    public static AppSettings Default { get; } = new(HotkeyChord.Default, 4, "auto");
}

public static class SettingsStore
{
    private static readonly string[] Languages = ["auto", "ko", "en"];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static AppSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            return AppSettings.Default;
        }

        Stored? stored;
        try
        {
            stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path), Options);
        }
        catch (JsonException)
        {
            TryBackup(path);
            return AppSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }

        if (stored is null)
        {
            return AppSettings.Default;
        }

        var hotkey = new HotkeyChord(stored.Hotkey ?? []);
        return new AppSettings(
            hotkey.IsValid ? hotkey : HotkeyChord.Default,
            Math.Clamp(stored.LastBufferSize ?? AppSettings.Default.LastBufferSize, SolveRequest.MinBufferSize, SolveRequest.MaxBufferSize),
            Languages.Contains(stored.Language) ? stored.Language! : "auto");
    }

    public static void Save(string path, AppSettings settings)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stored = new Stored([.. settings.Hotkey.VirtualKeys], settings.LastBufferSize, settings.Language);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(stored, Options));
        File.Move(temp, path, overwrite: true);
    }

    private static void TryBackup(string path)
    {
        try
        {
            File.Copy(path, path + ".bak", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record Stored(int[]? Hotkey, int? LastBufferSize, string? Language);
}
