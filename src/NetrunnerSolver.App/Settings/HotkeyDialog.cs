using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NetrunnerSolver.App.Overlay;
using NetrunnerSolver.Core.Input;
using NetrunnerSolver.Core.Text;

namespace NetrunnerSolver.App.Settings;

internal sealed class HotkeyDialog : Window
{
    private readonly ChordRecorder _recorder = new();
    private readonly TextBlock _display;
    private readonly Button _save;

    public HotkeyDialog(string language, HotkeyChord current)
    {
        ArgumentNullException.ThrowIfNull(current);
        Chord = current;
        Title = UiText.Get("hotkey.title", language);
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = Palette.Panel;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Malgun Gothic");

        _display = new TextBlock
        {
            Text = current.ToDisplayString(),
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = Palette.Path,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 16),
        };
        _save = DialogButton(UiText.Get("hotkey.save", language), (_, _) => DialogResult = true);
        var cancel = DialogButton(UiText.Get("hotkey.cancel", language), (_, _) => DialogResult = false);
        Content = new StackPanel
        {
            Margin = new Thickness(20),
            MinWidth = 300,
            Children =
            {
                new TextBlock { Text = UiText.Get("hotkey.prompt", language), Foreground = Palette.Text, FontSize = 13 },
                _display,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, _save } },
            },
        };
        PreviewKeyDown += OnKeyDown;
        PreviewKeyUp += OnKeyUp;
    }

    public HotkeyChord Chord { get; private set; }

    private static int VirtualKey(KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        return KeyInterop.VirtualKeyFromKey(key);
    }

    private static Button DialogButton(string text, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 4, 14, 4),
            Margin = new Thickness(6, 0, 0, 0),
            Foreground = Palette.Text,
            Background = Palette.Cell,
            BorderBrush = Palette.PanelBorder,
            Focusable = false,
        };
        button.Click += onClick;
        return button;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var vk = VirtualKey(e);
        if (vk != 0 && !e.IsRepeat)
        {
            _recorder.KeyDown(vk);
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var vk = VirtualKey(e);
        if (vk == 0)
        {
            return;
        }

        _recorder.KeyUp(vk);
        if (_recorder.Result is { IsValid: true } chord)
        {
            Chord = chord;
            _display.Text = chord.ToDisplayString();
            _save.IsEnabled = true;
        }
    }
}
