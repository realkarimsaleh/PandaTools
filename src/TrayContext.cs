using System.Drawing;
using System.Windows.Forms;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private SynchronizationContext? _syncCtx;

    public TrayContext()
    {
        TokenManager.Reset();

        _trayIcon = new NotifyIcon
        {
            Icon             = new Icon("assets/PandaTools.ico"),
            Text             = $"PandaTools — {ConfigLoader.AppConfig.Flavour}",
            Visible          = true,
            ContextMenuStrip = MenuBuilder.Build(Exit)
        };

        // Capture AFTER WinForms has initialised the sync context
        _syncCtx = SynchronizationContext.Current;

        // Rebuild menu on UI thread when config/flavour reloads
        ConfigLoader.OnConfigReloaded += () =>
            _syncCtx?.Post(_ => RebuildMenu(), null);

        _ = Updater.CheckAsync(silent: true);
    }

    // PUBLIC — called by SettingsWindow after changes
    public void RebuildMenu()
    {
        var old = _trayIcon.ContextMenuStrip;
        _trayIcon.ContextMenuStrip = MenuBuilder.Build(Exit);
        _trayIcon.Text             = $"PandaTools — {ConfigLoader.AppConfig.Flavour}";
        old?.Dispose();
    }

    private void Exit()
    {
        _trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _trayIcon.Dispose();
        base.Dispose(disposing);
    }
}
