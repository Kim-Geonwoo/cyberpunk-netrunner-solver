using System.Globalization;
using NetrunnerSolver.Core.Text;

namespace NetrunnerSolver.App.Logging;

internal static class FileLog
{
    private const int KeepDays = 7;
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string context, Exception exception) => Write("ERROR", $"{context}: {exception}");

    public static void Prune()
    {
        try
        {
            if (!Directory.Exists(AppPaths.LogsDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(AppPaths.LogsDir, "app-*.log").OrderDescending().Skip(KeepDays))
            {
                File.Delete(file);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Write(string level, string message)
    {
        var now = DateTime.Now;
        var line = LogText.Line(now, level, message);
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.LogsDir);
                File.AppendAllText(Path.Combine(AppPaths.LogsDir, $"app-{now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log"), line);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
