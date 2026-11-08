using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using NetrunnerSolver.Core;
using NetrunnerSolver.Core.Recognition;
using NetrunnerSolver.Core.Session;
using NetrunnerSolver.Core.Text;

namespace NetrunnerSolver.App.Overlay;

public sealed class PanelView : Border
{
    private const double ChipWidth = 44;
    private const double MiniCell = 28;
    private static readonly FontFamily Mono = new("Consolas");

    private readonly StackPanel _root = new() { MinWidth = 260, MaxWidth = 360 };
    private SolveSession? _session;
    private PuzzleReading? _reading;
    private bool _bufferDetected;
    private string _language = "en";
    private string _failureTitle = "status.notFound";
    private string _failureHint = "warn.fullscreen";

    public PanelView()
    {
        Background = Palette.Panel;
        BorderBrush = Palette.PanelBorder;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(12);
        TextElement.SetFontFamily(this, new FontFamily("Segoe UI, Malgun Gothic"));
        TextElement.SetFontSize(this, 13);
        Child = _root;
    }

    public event EventHandler? CloseRequested;

    public event EventHandler? RereadRequested;

    public event EventHandler? SaveCaptureRequested;

    public PuzzleReading? Reading => _reading;

    public void SetSession(SolveSession session, PuzzleReading reading, bool bufferDetected, string language)
    {
        Detach();
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _reading = reading ?? throw new ArgumentNullException(nameof(reading));
        _bufferDetected = bufferDetected;
        _language = language;
        _session.Changed += OnSessionChanged;
        Build();
    }

    public void SetNotFound(string language) => SetFailure("status.notFound", "warn.fullscreen", null, language);

    public void SetFailure(string titleKey, string hintKey, PuzzleReading? reading, string language)
    {
        Detach();
        _failureTitle = titleKey;
        _failureHint = hintKey;
        _reading = reading;
        _language = language;
        Build();
    }

    public void Detach()
    {
        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
            _session = null;
        }
    }

    public static (double X, double Y) Place(PuzzleReading? reading, double width, double height, double screenWidth, double screenHeight)
    {
        double x, y;
        var grid = reading?.Grid;
        if (grid is null)
        {
            x = (screenWidth - width) / 2;
            y = (screenHeight - height) / 2;
        }
        else if (reading!.Sequences.Count > 0)
        {
            x = reading.Sequences[0].Boxes[0].X;
            y = reading.Sequences[^1].Boxes.Max(b => b.Bottom) + grid.PitchY;
        }
        else
        {
            x = grid.Bounds.Right + grid.PitchX;
            y = grid.Bounds.Y;
        }

        return (Math.Clamp(x, 8, Math.Max(8, screenWidth - width - 8)), Math.Clamp(y, 8, Math.Max(8, screenHeight - height - 8)));
    }

    public void SetLanguage(string language)
    {
        _language = language;
        Build();
    }

    private string T(string key) => UiText.Get(key, _language);

    private void OnSessionChanged(object? sender, EventArgs e) => Build();

    private void Build()
    {
        _root.Children.Clear();
        _root.Children.Add(Header());
        if (_session is null)
        {
            _root.Children.Add(Label(T(_failureTitle), Palette.Unknown, 15, bold: true));
            _root.Children.Add(Label(T(_failureHint), Palette.Muted, 12));
            _root.Children.Add(Actions());
            return;
        }

        var result = _session.Result;
        var (status, color) = result switch
        {
            { TimedOut: true } => (T("status.timedOut"), Palette.Unknown),
            { AllCompleted: true } => (T("status.allSolvable"), Palette.Success),
            _ when result.CompletedCount > 0 => (string.Format(CultureInfo.InvariantCulture, T("status.partial"), result.CompletedCount, result.Completed.Count), Palette.Path),
            _ => (T("status.none"), Palette.Unknown),
        };
        _root.Children.Add(Label(status, color, 15, bold: true));
        foreach (var warning in Warnings())
        {
            _root.Children.Add(Label(warning, Palette.Muted, 12));
        }

        _root.Children.Add(Spacer());
        for (var sequence = 0; sequence < _session.Sequences.Count; sequence++)
        {
            _root.Children.Add(SequenceRow(sequence));
        }

        _root.Children.Add(Spacer());
        _root.Children.Add(SolutionRow());
        _root.Children.Add(BufferRow());
        _root.Children.Add(Spacer());
        _root.Children.Add(MiniMatrix());
        _root.Children.Add(Actions());
    }

    private IEnumerable<string> Warnings()
    {
        if (_session!.Matrix.Size > 0 && Enumerable.Range(0, _session.Matrix.Size).Any(r => Enumerable.Range(0, _session.Matrix.Size).Any(c => _session.Matrix[r, c] == BreachCodes.Unknown)))
        {
            yield return T("warn.unknownCells");
        }

        var warnings = _reading?.Warnings ?? [];
        if (!_bufferDetected && warnings.Contains(ReadWarning.BufferNotDetected))
        {
            yield return T("warn.bufferNotDetected");
        }

        if (warnings.Contains(ReadWarning.BufferHasContent))
        {
            yield return T("warn.bufferHasContent");
        }

        if (warnings.Contains(ReadWarning.NoSequences))
        {
            yield return T("warn.noSequences");
        }
    }

    private DockPanel Header()
    {
        var header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 6) };
        var close = SmallButton("✕", (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty));
        close.ToolTip = T("action.close");
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(Label(T("tray.title"), Palette.Muted, 11));
        return header;
    }

    private StackPanel SequenceRow(int sequence)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(Label($"#{sequence + 1}", Palette.Muted, 12, margin: new Thickness(0, 0, 6, 0)));
        var codes = _session!.Sequences[sequence];
        for (var i = 0; i < codes.Count; i++)
        {
            var index = i;
            row.Children.Add(CodeButton(codes[i], (_, _) => _session.CycleSequenceCode(sequence, index), highlight: null));
        }

        var done = _session.Result.Completed[sequence];
        var chip = new Border
        {
            Width = ChipWidth,
            Margin = new Thickness(8, 0, 0, 0),
            CornerRadius = new CornerRadius(3),
            Background = done ? Palette.Success : Palette.Unknown,
            Child = new TextBlock
            {
                Text = done ? T("seq.done") : T("seq.fail"),
                Foreground = Palette.BadgeText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        var dock = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(chip, Dock.Right);
        dock.Children.Add(chip);
        dock.Children.Add(row);
        return new StackPanel { Children = { dock } };
    }

    private WrapPanel SolutionRow()
    {
        var row = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(Label(T("label.solution"), Palette.Muted, 12, margin: new Thickness(0, 0, 8, 0)));
        var buffer = _session!.Result.Buffer;
        for (var i = 0; i < _session.BufferSize; i++)
        {
            var filled = i < buffer.Count;
            row.Children.Add(new Border
            {
                Width = MiniCell,
                Height = MiniCell - 4,
                Margin = new Thickness(0, 0, 3, 0),
                BorderThickness = new Thickness(1),
                BorderBrush = filled ? Palette.Path : Palette.Muted,
                CornerRadius = new CornerRadius(2),
                Child = new TextBlock
                {
                    Text = filled ? BreachCodes.Format(buffer[i]) : string.Empty,
                    Foreground = Palette.Text,
                    FontFamily = Mono,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
        }

        return row;
    }

    private StackPanel BufferRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };
        row.Children.Add(Label(T("label.buffer"), Palette.Muted, 12, margin: new Thickness(0, 0, 8, 0)));
        row.Children.Add(SmallButton("−", (_, _) => _session!.SetBufferSize(_session.BufferSize - 1)));
        row.Children.Add(Label(_session!.BufferSize.ToString(CultureInfo.InvariantCulture), Palette.Text, 13, bold: true, margin: new Thickness(8, 0, 8, 0)));
        row.Children.Add(SmallButton("+", (_, _) => _session!.SetBufferSize(_session.BufferSize + 1)));
        if (_bufferDetected)
        {
            row.Children.Add(Label($"({T("label.auto")})", Palette.Muted, 11, margin: new Thickness(8, 0, 0, 0)));
        }

        return row;
    }

    private UniformGrid MiniMatrix()
    {
        var matrix = _session!.Matrix;
        var steps = new Dictionary<Cell, int>();
        for (var i = 0; i < _session.Result.Path.Count; i++)
        {
            steps[_session.Result.Path[i]] = i + 1;
        }

        var grid = new UniformGrid { Rows = matrix.Size, Columns = matrix.Size, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 6) };
        for (var r = 0; r < matrix.Size; r++)
        {
            for (var c = 0; c < matrix.Size; c++)
            {
                var cell = new Cell(r, c);
                var button = CodeButton(matrix[r, c], (_, _) => _session.CycleMatrixCell(cell), steps.TryGetValue(cell, out var step) ? step : null);
                button.Width = MiniCell + 6;
                button.Height = MiniCell;
                grid.Children.Add(button);
            }
        }

        return grid;
    }

    private StackPanel Actions()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        row.Children.Add(TextButton(T("action.reread"), (_, _) => RereadRequested?.Invoke(this, EventArgs.Empty)));
        row.Children.Add(TextButton(T("action.saveCapture"), (_, _) => SaveCaptureRequested?.Invoke(this, EventArgs.Empty)));
        return row;
    }

    private static Button CodeButton(byte code, RoutedEventHandler onClick, int? highlight)
    {
        var unknown = code == BreachCodes.Unknown;
        var text = BreachCodes.Format(code);
        var button = new Button
        {
            Content = highlight is { } step ? $"{text}·{step.ToString(CultureInfo.InvariantCulture)}" : text,
            FontFamily = Mono,
            FontSize = highlight is null ? 12 : 10,
            Foreground = highlight is null ? (unknown ? Palette.Unknown : Palette.Text) : Palette.BadgeText,
            Background = highlight is null ? Palette.Cell : Palette.Path,
            BorderThickness = new Thickness(unknown ? 1 : 0),
            BorderBrush = Palette.Unknown,
            Margin = new Thickness(1),
            Padding = new Thickness(3, 1, 3, 1),
            MinWidth = MiniCell,
            Focusable = false,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        button.Click += onClick;
        return button;
    }

    private static Button SmallButton(string text, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            Margin = new Thickness(1, 0, 1, 0),
            Foreground = Palette.Text,
            Background = Palette.Cell,
            BorderThickness = new Thickness(0),
            Focusable = false,
        };
        button.Click += onClick;
        return button;
    }

    private static Button TextButton(string text, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 6, 0),
            Foreground = Palette.Text,
            Background = Palette.Cell,
            BorderBrush = Palette.PanelBorder,
            BorderThickness = new Thickness(1),
            Focusable = false,
        };
        button.Click += onClick;
        return button;
    }

    private static TextBlock Label(string text, Brush color, double size, bool bold = false, Thickness? margin = null) => new()
    {
        Text = text,
        Foreground = color,
        FontSize = size,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = margin ?? new Thickness(0, 1, 0, 1),
    };

    private static Border Spacer() => new() { Height = 6 };
}
