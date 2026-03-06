using System;
using System.Drawing;
using System.Windows.Forms;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;

    public TrayContext()
    {
        //  Force fresh token decryption — clears any stale cached value from previous session
        TokenManager.Reset();

        _trayIcon = new NotifyIcon
        {
            Icon             = new Icon("assets/PandaTool.ico"),
            Text             = "PandaTools",
            Visible          = true,
            ContextMenuStrip = MenuBuilder.Build(Exit)
        };

        _ = Updater.CheckAsync(silent: true);
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
