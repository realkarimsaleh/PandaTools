using System.Drawing;
using System.Windows.Forms;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon        _trayIcon;
    private          SynchronizationContext? _syncCtx;
    private static   TrayContext?      _instance;

    public TrayContext()
    {
        _instance = this;

        ConfigLoader.Load();
        TokenManager.Reset();

        // ── First-run: prompt for token if none is configured ─────────
        if (!TokenManager.HasToken())
            ShowFirstRunTokenDialog();

        // ── Warn if no real (non-template) flavour is available ───────
        var flavours = ConfigLoader.GetAvailableFlavours(includeHidden: false);
        if (flavours.Length == 0)
            MessageBox.Show(
                $"No flavour files found.\n\n" +
                $"Drop your .json flavour files into:\n{ConfigLoader.FlavourDir}\n\n" +
                $"A _Template.json has been placed there as a reference.",
                "PandaTools - No Flavours",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

        _trayIcon = new NotifyIcon
        {
            Icon             = AppIcon.Get(),
            Text             = $"PandaTools - {ConfigLoader.AppConfig.Flavour}",
            Visible          = true,
            ContextMenuStrip = MenuBuilder.Build(Exit)
        };

        _syncCtx = SynchronizationContext.Current;

        ConfigLoader.OnConfigReloaded += () =>
            _syncCtx?.Post(_ => RebuildMenu(), null);

        _ = Updater.CheckAsync(silent: true);
        _ = TokenExpiryChecker.CheckAsync();
    }

    //######################################
    //Balloon helper callable from anywhere
    //######################################
    public static void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _instance?._trayIcon.ShowBalloonTip(6000, title, text, icon);
    }

    private static void ShowFirstRunTokenDialog()
    {
        using var frm = new Form
        {
            Text            = "PandaTools - First-Time Setup",
            // FIXED: Using ClientSize guarantees exact 16px padding on all sides!
            ClientSize      = new Size(492, 246), 
            StartPosition   = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox     = false,
            MinimizeBox     = false,
            TopMost         = true,
            Icon            = AppIcon.Get()
        };

        frm.Controls.Add(new Label
        {
            Text      = "🐼 PandaTools - First-Time Setup",
            Left = 16, Top = 16, Width = 460, Height = 24,
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 30)
        });

        frm.Controls.Add(new Panel
        {
            Left = 16, Top = 46, Width = 460, Height = 1,
            BackColor = Color.FromArgb(220, 220, 220)
        });

        frm.Controls.Add(new Label
        {
            Text =
                "Enter your GitLab Personal Access Token to continue.\r\n" +
                "It will be encrypted with Windows DPAPI and stored securely\r\n" +
                "on this machine - it is never saved as plain text.",
            Left = 16, Top = 56, Width = 460, Height = 58,
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(60, 60, 60)
        });

        frm.Controls.Add(new Label
        {
            Text = "Personal Access Token:",
            Left = 16, Top = 122, Width = 220, Height = 18,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        });

        var txt = new TextBox
        {
            Left                  = 16, Top = 143, Width = 460, Height = 26,
            UseSystemPasswordChar = false,
            Font                  = new Font("Segoe UI", 9.5f),
            BorderStyle           = BorderStyle.FixedSingle,
            PlaceholderText       = "glpat-xxxxxxxxxxxxxxxxxxxx"
        };
        frm.Controls.Add(txt);

        frm.Controls.Add(new Label
        {
            Text      = "Generate one at: GitLab → User Settings → Access Tokens  (read_api scope minimum)",
            Left = 16, Top = 174, Width = 460, Height = 16,
            ForeColor = Color.DimGray,
            Font      = new Font("Segoe UI", 7.5f)
        });

        var btnSkip = new Button
        {
            Text         = "Skip for now",
            Left         = 16, Top = 200, Width = 110, Height = 30,
            DialogResult = DialogResult.Cancel,
            Font         = new Font("Segoe UI", 9f)
        };
        
        var btnSave = new Button
        {
            Text         = "💾 Save && Continue",
            Left         = 334, Top = 200, Width = 142, Height = 30,
            DialogResult = DialogResult.OK,
            Font         = new Font("Segoe UI", 9f),
            BackColor    = Color.FromArgb(40, 167, 69),
            ForeColor    = Color.White,
            FlatStyle    = FlatStyle.Flat
        };
        btnSave.FlatAppearance.BorderSize = 0;

        frm.Controls.AddRange(new Control[] { btnSkip, btnSave });
        frm.AcceptButton = btnSave;
        frm.CancelButton = btnSkip;

        if (frm.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
            TokenManager.SaveToken(txt.Text);
    }

    public void RebuildMenu()
    {
        var old = _trayIcon.ContextMenuStrip;
        _trayIcon.ContextMenuStrip = MenuBuilder.Build(Exit);
        _trayIcon.Text = $"PandaTools - {ConfigLoader.AppConfig.Flavour}";
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