using System.Globalization;
using System.Windows.Threading;
using NetrunnerSolver.App.Logging;
using NetrunnerSolver.App.Overlay;
using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;
using NetrunnerSolver.Core.Session;
using NetrunnerSolver.Core.Settings;
using NetrunnerSolver.Core.Solving;
using NetrunnerSolver.Windows.Capture;
using NetrunnerSolver.Windows.Imaging;
using NetrunnerSolver.Windows.Input;
using NetrunnerSolver.Windows.Ocr;

namespace NetrunnerSolver.App;

internal sealed class AppController : IDisposable
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RecaptureDelay = TimeSpan.FromMilliseconds(150);
    private static readonly SolveResult Unsolved = new([], [], [], false, false);

    private readonly Dispatcher _dispatcher;
    private readonly OverlayWindow _overlay = new();
    private readonly PanelWindow _panel = new();
    private readonly BgraImage? _simulated;
    private readonly PuzzleReader _reader;
    private HotkeyPoller? _poller;
    private AppSettings _settings;
    private string _language;
    private SolveSession? _session;
    private PuzzleReading? _reading;
    private MonitorArea? _area;
    private BgraImage? _lastScreen;
    private bool _busy;

    public AppController(Dispatcher dispatcher, AppSettings settings, string language, BgraImage? simulated)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _language = language;
        _simulated = simulated;
        var engines = OcrEngines.CreateAvailable();
        FileLog.Info($"OCR engines: {string.Join(", ", engines.Select(e => e.Language))}");
        _reader = new PuzzleReader(engines);
        _panel.CloseRequested += (_, _) => Hide();
        _panel.RereadRequested += (_, _) => _ = RereadAsync();
        _panel.SaveCaptureRequested += (_, _) => _ = SaveCaptureAsync();
    }

    public bool IsShown { get; private set; }

    public AppSettings Settings => _settings;

    public void Attach(HotkeyPoller poller) => _poller = poller ?? throw new ArgumentNullException(nameof(poller));

    public void UpdateSettings(AppSettings settings, string language)
    {
        _settings = settings;
        _language = language;
        _panel.SetLanguage(language);
    }

    public async Task ToggleAsync()
    {
        if (_busy)
        {
            return;
        }

        if (IsShown)
        {
            Hide();
            return;
        }

        await ShowAsync().ConfigureAwait(true);
    }

    public void Hide()
    {
        _overlay.Hide();
        _panel.Hide();
        _poller?.WatchEscape(null);
        IsShown = false;
    }

    public async Task RereadAsync()
    {
        if (_busy)
        {
            return;
        }

        Hide();
        await Task.Delay(RecaptureDelay).ConfigureAwait(true);
        await ShowAsync().ConfigureAwait(true);
    }

    public void Dispose()
    {
        DropSession();
        _overlay.Close();
        _panel.Close();
    }

    private async Task ShowAsync()
    {
        _busy = true;
        MonitorArea? area = null;
        try
        {
            area = _simulated is null ? MonitorLocator.FromForegroundWindow() : MonitorLocator.Primary();
            var target = area;
            using var timeout = new CancellationTokenSource(ReadTimeout);
            var screen = _simulated ?? await Task.Run(() => ScreenCapture.Capture(target), timeout.Token).ConfigureAwait(true);
            var reading = await Task.Run(() => _reader.ReadAsync(screen, timeout.Token), timeout.Token).ConfigureAwait(true);
            FileLog.Info($"read {screen.Width}x{screen.Height} in {reading.Elapsed.TotalMilliseconds:F0} ms, tokens {reading.TokenCount}, grid {reading.Grid?.Size.ToString(CultureInfo.InvariantCulture) ?? "-"}, sequences {reading.Sequences.Count}, buffer {reading.Buffer?.SlotCount.ToString(CultureInfo.InvariantCulture) ?? "-"}, warnings [{string.Join(", ", reading.Warnings)}]");
            _area = area;
            _lastScreen = screen;
            _reading = reading;
            DropSession();

            if (reading.Outcome == ReadOutcome.NotFound)
            {
                _overlay.Hide();
                _panel.ShowNotFound(_language, area);
            }
            else if (reading.Outcome == ReadOutcome.CursorCovered)
            {
                _overlay.ShowOn(area);
                _overlay.Render(reading.Grid!, Unsolved, reading.Matrix!, area);
                _panel.ShowFailure("status.cursorCovering", "hint.moveCursor", reading, _language, area);
            }
            else
            {
                var detected = reading.Buffer is not null && !reading.Buffer.HasContent;
                var buffer = detected ? reading.Buffer!.SlotCount : _settings.LastBufferSize;
                _session = await SolveSession.StartAsync(reading.Matrix!, [.. reading.Sequences.Select(s => s.Codes)], buffer, timeout.Token).ConfigureAwait(true);
                _session.Changed += OnSessionChanged;
                RememberBuffer(_session.BufferSize);
                _overlay.ShowOn(area);
                _overlay.Render(reading.Grid!, _session.Result, _session.Matrix, area);
                _panel.ShowSession(_session, reading, detected, _language, area);
            }

            _poller?.WatchEscape(() => _dispatcher.InvokeAsync(Hide));
            IsShown = true;
        }
#pragma warning disable CA1031 // UI 진입점: 어떤 실패도 앱을 멈추거나 조용히 사라지면 안 된다.
        catch (Exception ex) when (ex is not OutOfMemoryException)
#pragma warning restore CA1031
        {
            FileLog.Error(ex is OperationCanceledException ? "read timed out" : "show failed", ex);
            ShowFailure(area);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ShowFailure(MonitorArea? area)
    {
        _overlay.Hide();
        if (area is null)
        {
            Hide();
            return;
        }

        _reading = null;
        _area = area;
        _panel.ShowNotFound(_language, area);
        _poller?.WatchEscape(() => _dispatcher.InvokeAsync(Hide));
        IsShown = true;
    }

    private void DropSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.Changed -= OnSessionChanged;
        _session.Dispose();
        _session = null;
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (_session is null || _reading?.Grid is null || _area is null)
        {
            return;
        }

        _overlay.Render(_reading.Grid, _session.Result, _session.Matrix, _area);
        RememberBuffer(_session.BufferSize);
    }

    private void RememberBuffer(int size)
    {
        if (_settings.LastBufferSize == size)
        {
            return;
        }

        _settings = _settings with { LastBufferSize = size };
        try
        {
            SettingsStore.Save(AppPaths.SettingsFile, _settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileLog.Error("save settings failed", ex);
        }
    }

    private async Task SaveCaptureAsync()
    {
        if (_lastScreen is null)
        {
            return;
        }

        var path = Path.Combine(AppPaths.CapturesDir, $"capture-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png");
        try
        {
            await ImageFile.SavePngAsync(_lastScreen, path).ConfigureAwait(true);
            FileLog.Info($"capture saved: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            FileLog.Error("save capture failed", ex);
        }
    }
}
