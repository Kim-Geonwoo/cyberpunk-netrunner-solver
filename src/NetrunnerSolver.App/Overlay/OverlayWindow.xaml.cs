using System.Windows;
using NetrunnerSolver.Core;
using NetrunnerSolver.Core.Recognition;
using NetrunnerSolver.Core.Solving;
using NetrunnerSolver.Windows.Capture;

namespace NetrunnerSolver.App.Overlay;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowStyles.MakeOverlay(this, clickThrough: true);
    }

    public void Render(GridGeometry grid, SolveResult result, CodeMatrix matrix, MonitorArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        OverlayCanvas.Draw(Surface, grid, result, matrix, area.DpiScale);
    }

    public void ShowOn(MonitorArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (!IsVisible)
        {
            Show();
        }

        WindowStyles.PlaceOnMonitor(this, area);
    }
}
