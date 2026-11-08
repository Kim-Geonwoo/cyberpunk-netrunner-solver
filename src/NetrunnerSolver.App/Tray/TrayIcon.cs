using NetrunnerSolver.Core.Text;
using Forms = System.Windows.Forms;

namespace NetrunnerSolver.App.Tray;

internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private string _language;
    private string _languageSetting;

    public TrayIcon(string language, string languageSetting)
    {
        _language = language;
        _languageSetting = languageSetting;
        _icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? System.Drawing.SystemIcons.Application,
            Visible = true,
        };
        Rebuild();
    }

    public event EventHandler? HotkeyRequested;

    public event EventHandler<string>? LanguageRequested;

    public event EventHandler? CapturesRequested;

    public event EventHandler? ExitRequested;

    public void SetLanguage(string language, string languageSetting)
    {
        _language = language;
        _languageSetting = languageSetting;
        Rebuild();
    }

    public void ShowStarted(string hotkey)
    {
        _icon.BalloonTipTitle = T("tray.title");
        _icon.BalloonTipText = string.Format(System.Globalization.CultureInfo.InvariantCulture, T("tray.started"), hotkey);
        _icon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }

    private string T(string key) => UiText.Get(key, _language);

    private void Rebuild()
    {
        var old = _icon.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem(T("tray.title")) { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("tray.hotkey"), null, (_, _) => HotkeyRequested?.Invoke(this, EventArgs.Empty));
        var language = new Forms.ToolStripMenuItem(T("tray.language"));
        language.DropDownItems.Add(LanguageItem(T("tray.languageAuto"), "auto"));
        language.DropDownItems.Add(LanguageItem("한국어", "ko"));
        language.DropDownItems.Add(LanguageItem("English", "en"));
        menu.Items.Add(language);
        menu.Items.Add(T("tray.captures"), null, (_, _) => CapturesRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("tray.exit"), null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        _icon.ContextMenuStrip = menu;
        _icon.Text = T("tray.title");
        old?.Dispose();
    }

    private Forms.ToolStripMenuItem LanguageItem(string text, string value) =>
        new(text, null, (_, _) => LanguageRequested?.Invoke(this, value)) { Checked = _languageSetting == value };
}
