using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetrunnerSolver.App.Overlay;
using NetrunnerSolver.Core.Recognition;
using NetrunnerSolver.Core.Session;
using NetrunnerSolver.Core.Solving;
using NetrunnerSolver.Windows.Imaging;
using NetrunnerSolver.Windows.Ocr;

namespace NetrunnerSolver.App.Preview;

internal static class PreviewRenderer
{
    public static async Task RunAsync(string imagePath, string outputPath, string language, int fallbackBuffer)
    {
        var image = await ImageFile.LoadAsync(imagePath).ConfigureAwait(true);
        var reading = await new PuzzleReader(OcrEngines.CreateAvailable()).ReadAsync(image, CancellationToken.None).ConfigureAwait(true);
        var width = image.Width;
        var height = image.Height;
        var root = new Canvas { Width = width, Height = height };
        var background = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, ImageFile.SharedPixels(image), width * 4);
        root.Children.Add(new Image { Source = background, Width = width, Height = height });
        var overlay = new Canvas();
        root.Children.Add(overlay);

        var panel = new PanelView();
        if (reading.Outcome == ReadOutcome.NotFound)
        {
            panel.SetNotFound(language);
        }
        else if (reading.Outcome == ReadOutcome.CursorCovered)
        {
            panel.SetFailure("status.cursorCovering", "hint.moveCursor", reading, language);
            OverlayCanvas.Draw(overlay, reading.Grid!, new SolveResult([], [], [], false, false), reading.Matrix!, 1.0);
        }
        else
        {
            var detected = reading.Buffer is { HasContent: false };
            using var session = await SolveSession.StartAsync(reading.Matrix!, [.. reading.Sequences.Select(s => s.Codes)], detected ? reading.Buffer!.SlotCount : fallbackBuffer).ConfigureAwait(true);
            panel.SetSession(session, reading, detected, language);
            OverlayCanvas.Draw(overlay, reading.Grid!, session.Result, session.Matrix, 1.0);
        }

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (x, y) = PanelView.Place(reading, panel.DesiredSize.Width, panel.DesiredSize.Height, width, height);
        Canvas.SetLeft(panel, x);
        Canvas.SetTop(panel, y);
        root.Children.Add(panel);
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = File.Create(outputPath);
        encoder.Save(file);
    }
}
