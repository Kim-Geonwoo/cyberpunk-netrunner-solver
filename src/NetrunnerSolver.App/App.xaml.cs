using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using NetrunnerSolver.App.Logging;
using NetrunnerSolver.App.Preview;
using NetrunnerSolver.App.Settings;
using NetrunnerSolver.App.Simulation;
using NetrunnerSolver.App.Tray;
using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Settings;
using NetrunnerSolver.Core.Text;
using NetrunnerSolver.Windows.Capture;
using NetrunnerSolver.Windows.Imaging;
using NetrunnerSolver.Windows.Input;

namespace NetrunnerSolver.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "WPF Application has no Dispose; fields are disposed in OnExit.")]
public partial class App : Application
{
    private Mutex? _single;
    private bool _ownsMutex;
    private AppController? _controller;
    private HotkeyPoller? _poller;
    private TrayIcon? _tray;
    private Window? _simulation;
    private string _language = "en";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var preview = Array.IndexOf(e.Args, "--preview");
        if (preview >= 0 && preview + 2 < e.Args.Length)
        {
            await RunPreviewAsync(e.Args[preview + 1], e.Args[preview + 2], e.Args).ConfigureAwait(true);
            return;
        }

        _single = new Mutex(true, @"Local\cyberpunk-netrunner-solver", out _ownsMutex);
        if (!_ownsMutex)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            FileLog.Error("unobserved task", args.Exception);
            args.SetObserved();
        };
        FileLog.Prune();

        var settings = SettingsStore.Load(AppPaths.SettingsFile);
        var language = UiText.ResolveLanguage(settings.Language, CultureInfo.CurrentUICulture.Name);
        _language = language;
        var simulatePath = SimulatePath(e.Args);
        BgraImage? simulated = null;
        if (simulatePath is not null)
        {
            try
            {
                simulated = await ImageFile.LoadAsync(simulatePath).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
            {
                // 트레이도 컨트롤러도 없이 뮤텍스만 쥔 채 남지 않도록 기록하고 끝낸다.
                FileLog.Error($"simulate image load failed: {simulatePath}", ex);
                Shutdown(1);
                return;
            }
        }

        _controller = new AppController(Dispatcher, settings, language, simulated);
        _poller = new HotkeyPoller(settings.Hotkey, () => Dispatcher.InvokeAsync(() => _controller.ToggleAsync()));
        _controller.Attach(_poller);
        _tray = new TrayIcon(language, settings.Language);
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.CapturesRequested += (_, _) => OpenCaptures();
        _tray.LanguageRequested += (_, value) => ChangeLanguage(value);
        _tray.HotkeyRequested += (_, _) => ChangeHotkey();
        _poller.Start();
        _tray.ShowStarted(settings.Hotkey.ToDisplayString());
        FileLog.Info($"started, hotkey {settings.Hotkey.ToDisplayString()}, language {language}");

        if (simulated is not null)
        {
            _simulation = new SimulatedGameWindow(simulated, MonitorLocator.Primary());
            _simulation.Show();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                await _controller.ToggleAsync().ConfigureAwait(true);
            };
            timer.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _poller?.Dispose();
        _controller?.Dispose();
        _tray?.Dispose();
        _simulation?.Close();
        if (_ownsMutex)
        {
            _single?.ReleaseMutex();
        }

        _single?.Dispose();
        base.OnExit(e);
    }

    private async Task RunPreviewAsync(string imagePath, string outputPath, string[] args)
    {
        var lang = Array.IndexOf(args, "--lang");
        var language = lang >= 0 && lang + 1 < args.Length ? args[lang + 1] : UiText.ResolveLanguage("auto", CultureInfo.CurrentUICulture.Name);
        try
        {
            await PreviewRenderer.RunAsync(imagePath, outputPath, language, AppSettings.Default.LastBufferSize).ConfigureAwait(true);
            Shutdown(0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            await File.WriteAllTextAsync(outputPath + ".error.txt", ex.ToString()).ConfigureAwait(true);
            Shutdown(1);
        }
    }

    private static string? SimulatePath(string[] args)
    {
        var index = Array.IndexOf(args, "--simulate");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void OpenCaptures()
    {
        Directory.CreateDirectory(AppPaths.CapturesDir);
        using var process = Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.CapturesDir}\"") { UseShellExecute = true });
    }

    private void ChangeLanguage(string setting)
    {
        if (_controller is null || _tray is null)
        {
            return;
        }

        var settings = _controller.Settings with { Language = setting };
        var language = UiText.ResolveLanguage(setting, CultureInfo.CurrentUICulture.Name);
        SaveSettings(settings);
        _language = language;
        _controller.UpdateSettings(settings, language);
        _tray.SetLanguage(language, setting);
    }

    private void ChangeHotkey()
    {
        if (_controller is null || _poller is null || _tray is null)
        {
            return;
        }

        _poller.SetPaused(true);
        try
        {
            var dialog = new HotkeyDialog(_language, _controller.Settings.Hotkey);
            if (dialog.ShowDialog() == true && dialog.Chord.IsValid)
            {
                var settings = _controller.Settings with { Hotkey = dialog.Chord };
                SaveSettings(settings);
                _controller.UpdateSettings(settings, _language);
                _poller.UpdateChord(dialog.Chord);
                _tray.ShowStarted(dialog.Chord.ToDisplayString());
                FileLog.Info($"hotkey changed to {dialog.Chord.ToDisplayString()}");
            }
        }
        finally
        {
            _poller.SetPaused(false);
        }
    }

    private static void SaveSettings(AppSettings settings)
    {
        try
        {
            SettingsStore.Save(AppPaths.SettingsFile, settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileLog.Error("save settings failed", ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        FileLog.Error("unhandled", e.Exception);
        e.Handled = true;
    }
}
