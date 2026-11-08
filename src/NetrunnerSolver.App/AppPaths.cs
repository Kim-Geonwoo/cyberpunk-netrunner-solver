namespace NetrunnerSolver.App;

internal static class AppPaths
{
    private const string Folder = "cyberpunk-netrunner-solver";

    public static string SettingsFile { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Folder, "settings.json");

    public static string CapturesDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Folder, "captures");

    public static string LogsDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Folder, "logs");
}
