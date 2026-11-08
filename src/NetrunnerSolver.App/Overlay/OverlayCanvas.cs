using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NetrunnerSolver.Core;
using NetrunnerSolver.Core.Imaging;
using NetrunnerSolver.Core.Recognition;
using NetrunnerSolver.Core.Solving;

namespace NetrunnerSolver.App.Overlay;

internal static class OverlayCanvas
{
    private const double BadgeSize = 22;

    public static void Draw(Canvas surface, GridGeometry grid, SolveResult result, CodeMatrix matrix, double scale)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(matrix);
        surface.Children.Clear();
        var margin = 0.15 * Math.Min(grid.PitchX, grid.PitchY);
        Box Local(Box box) => box.ToLocalDip(0, 0, scale);
        Box Outline(Cell cell) => Local(grid.CellBox(cell.Row, cell.Col).Inflate(margin, margin));

        for (var r = 0; r < matrix.Size; r++)
        {
            for (var c = 0; c < matrix.Size; c++)
            {
                if (matrix[r, c] == BreachCodes.Unknown)
                {
                    AddRectangle(surface, Outline(new Cell(r, c)), Palette.Unknown, 2, dashed: true);
                }
            }
        }

        for (var i = 1; i < result.Path.Count; i++)
        {
            var from = Outline(result.Path[i - 1]);
            var to = Outline(result.Path[i]);
            surface.Children.Add(new Line
            {
                X1 = from.CenterX,
                Y1 = from.CenterY,
                X2 = to.CenterX,
                Y2 = to.CenterY,
                Stroke = Palette.Path,
                StrokeThickness = 2,
                Opacity = 0.7,
            });
        }

        for (var step = 0; step < result.Path.Count; step++)
        {
            var box = Outline(result.Path[step]);
            AddRectangle(surface, box, Palette.Path, 3, dashed: false);
            AddBadge(surface, box.X, box.Y, (step + 1).ToString(CultureInfo.InvariantCulture));
        }

        if (result.Path.Count > 0)
        {
            var first = Outline(result.Path[0]);
            var marker = new TextBlock { Text = "▼", Foreground = Palette.Path, FontSize = 16, FontWeight = FontWeights.Bold };
            Canvas.SetLeft(marker, first.CenterX - 7);
            Canvas.SetTop(marker, first.Y - 24);
            surface.Children.Add(marker);
        }
    }

    private static void AddRectangle(Canvas surface, Box box, Brush stroke, double thickness, bool dashed)
    {
        var rectangle = new Rectangle
        {
            Width = box.Width,
            Height = box.Height,
            Stroke = stroke,
            StrokeThickness = thickness,
            RadiusX = 3,
            RadiusY = 3,
        };
        if (dashed)
        {
            rectangle.StrokeDashArray = [3, 2];
        }

        Canvas.SetLeft(rectangle, box.X);
        Canvas.SetTop(rectangle, box.Y);
        surface.Children.Add(rectangle);
    }

    private static void AddBadge(Canvas surface, double x, double y, string text)
    {
        var badge = new Border
        {
            Width = BadgeSize,
            Height = BadgeSize,
            CornerRadius = new CornerRadius(BadgeSize / 2),
            Background = Palette.Path,
            Child = new TextBlock
            {
                Text = text,
                Foreground = Palette.BadgeText,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Canvas.SetLeft(badge, x - (BadgeSize / 2));
        Canvas.SetTop(badge, y - (BadgeSize / 2));
        surface.Children.Add(badge);
    }
}
