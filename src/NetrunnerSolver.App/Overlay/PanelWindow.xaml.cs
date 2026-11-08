using System.Windows;
using NetrunnerSolver.Core.Recognition;
using NetrunnerSolver.Core.Session;
using NetrunnerSolver.Windows.Capture;

namespace NetrunnerSolver.App.Overlay;

public partial class PanelWindow : Window
{
    private readonly PanelView _view = new();
    private MonitorArea? _area;

    public PanelWindow()
    {
        InitializeComponent();
        Content = _view;
        _view.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        _view.RereadRequested += (_, _) => RereadRequested?.Invoke(this, EventArgs.Empty);
        _view.SaveCaptureRequested += (_, _) => SaveCaptureRequested?.Invoke(this, EventArgs.Empty);
        SourceInitialized += (_, _) => WindowStyles.MakeOverlay(this, clickThrough: false);
    }

    public event EventHandler? CloseRequested;

    public event EventHandler? RereadRequested;

    public event EventHandler? SaveCaptureRequested;

    public void ShowSession(SolveSession session, PuzzleReading reading, bool bufferDetected, string language, MonitorArea area)
    {
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _view.SetSession(session, reading, bufferDetected, language);
        Present();
    }

    public void ShowNotFound(string language, MonitorArea area)
    {
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _view.SetNotFound(language);
        Present();
    }

    public void ShowFailure(string titleKey, string hintKey, PuzzleReading reading, string language, MonitorArea area)
    {
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _view.SetFailure(titleKey, hintKey, reading, language);
        Present();
    }

    public void SetLanguage(string language) => _view.SetLanguage(language);

    private void Present()
    {
        if (!IsVisible)
        {
            Show();
        }

        var area = _area!;
        for (var pass = 0; pass < 2; pass++)
        {
            UpdateLayout();
            var (x, y) = PanelView.Place(_view.Reading, ActualWidth * area.DpiScale, ActualHeight * area.DpiScale, area.Width, area.Height);
            WindowStyles.MoveTo(this, area.Left + (int)x, area.Top + (int)y);
        }
    }
}
