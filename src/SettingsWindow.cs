using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

public class SettingsWindow : Form
{
    private TextBox?       _urlBox;
    private TextBox?       _keyBox;
    private TextBox?       _tokenFileBox;
    private TextBox?       _tokenBox;
    private TextBox?       _browserPathBox;
    private ComboBox?      _flavourCombo;
    private ComboBox?      _browserCombo;
    private NumericUpDown? _pollBox;
    private CheckBox?      _diagCheck;
    private CheckBox?      _manualCheck;
    private Label?         _statusLabel;

    public SettingsWindow()
    {
        Text            = "PandaTools — Settings";
        Size            = new(580, 620);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        TopMost         = true;
        AutoScroll      = true;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var cfg = ConfigLoader.AppConfig;
        var y   = 10;

        // ── Connection ────────────────────────────────────────────────
        var grpConn = new GroupBox
        {
            Text  = "Connection",
            Left  = 10, Top = y,
            Width = 540, Height = 140
        };

        grpConn.Controls.Add(MakeLabel("GitLab Server URL:", 10, 24));
        _urlBox = MakeTextBox(cfg.UrlServer, 140, 22, 380);
        grpConn.Controls.Add(_urlBox);

        grpConn.Controls.Add(MakeLabel("Key File:", 10, 54));
        _keyBox = MakeTextBox(cfg.KeyFile, 140, 52, 380);
        grpConn.Controls.Add(_keyBox);

        grpConn.Controls.Add(MakeLabel("Token File:", 10, 84));
        _tokenFileBox = MakeTextBox(cfg.TokenFile, 140, 82, 380);
        grpConn.Controls.Add(_tokenFileBox);

        grpConn.Controls.Add(MakeLabel("Plain Token:", 10, 114));
        _tokenBox = MakeTextBox(cfg.Token, 140, 112, 380, password: true);
        grpConn.Controls.Add(_tokenBox);

        Controls.Add(grpConn);
        y += 150;

        // ── Flavour ───────────────────────────────────────────────────
        var grpFlavour = new GroupBox
        {
            Text  = "Flavour",
            Left  = 10, Top = y,
            Width = 540, Height = 90
        };

        grpFlavour.Controls.Add(MakeLabel("Active Flavour:", 10, 24));
        _flavourCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 140, Top = 22, Width = 250
        };
        foreach (var name in ConfigLoader.GetAvailableFlavours(includeHidden: true))
            _flavourCombo.Items.Add(name);
        _flavourCombo.SelectedItem = cfg.Flavour;
        grpFlavour.Controls.Add(_flavourCombo);

        var btnLoadLocal = MakeButton("📁 Load Local .json", 400, 21, 130);
        btnLoadLocal.Click += (_, _) => LoadLocalFlavour();
        grpFlavour.Controls.Add(btnLoadLocal);

        _manualCheck = new CheckBox
        {
            Text    = "Manual mode — disable auto-updates from GitLab",
            Checked = cfg.ManualMode,
            Left    = 10, Top = 58,
            Width   = 420
        };
        grpFlavour.Controls.Add(_manualCheck);

        Controls.Add(grpFlavour);
        y += 100;

        // ── Private Browser ───────────────────────────────────────────
        var grpBrowser = new GroupBox
        {
            Text  = "Private Browsing (🕶️ Incognito)",
            Left  = 10, Top = y,
            Width = 540, Height = 100
        };

        grpBrowser.Controls.Add(MakeLabel("Browser:", 10, 24));
        _browserCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 140, Top = 22, Width = 180
        };
        _browserCombo.Items.AddRange(new object[]
            { "Default", "Chrome", "Edge", "Firefox", "Brave", "Custom" });

        // Match saved value (case-insensitive)
        var savedBrowser = cfg.BrowserName;
        foreach (var b in _browserCombo.Items)
        {
            if (b.ToString()!.Equals(savedBrowser, StringComparison.OrdinalIgnoreCase))
            {
                _browserCombo.SelectedItem = b;
                break;
            }
        }
        if (_browserCombo.SelectedIndex < 0) _browserCombo.SelectedIndex = 0;

        // Browse button to find custom browser exe
        var btnBrowseBrowser = MakeButton("📁 Browse", 330, 21, 80);
        btnBrowseBrowser.Click += (_, _) =>
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                Title  = "Select your browser .exe"
            };
            if (ofd.ShowDialog() == DialogResult.OK && _browserPathBox != null)
            {
                _browserPathBox.Text = ofd.FileName;
                _browserCombo.SelectedItem = "Custom";
            }
        };

        grpBrowser.Controls.Add(_browserCombo);
        grpBrowser.Controls.Add(btnBrowseBrowser);

        grpBrowser.Controls.Add(MakeLabel("Custom Path:", 10, 60));
        _browserPathBox = MakeTextBox(cfg.BrowserPath, 140, 58, 270);
        grpBrowser.Controls.Add(_browserPathBox);

        Controls.Add(grpBrowser);
        y += 110;

        // ── Advanced ──────────────────────────────────────────────────
        var grpAdv = new GroupBox
        {
            Text  = "Advanced",
            Left  = 10, Top = y,
            Width = 540, Height = 60
        };

        grpAdv.Controls.Add(MakeLabel("Poll Interval (sec):", 10, 26));
        _pollBox = new NumericUpDown
        {
            Minimum = 30, Maximum = 3600,
            Value   = Math.Max(30, cfg.FlavourPollSeconds),
            Left    = 150, Top = 24, Width = 80
        };
        grpAdv.Controls.Add(_pollBox);

        _diagCheck = new CheckBox
        {
            Text    = "Enable Diagnostics",
            Checked = cfg.Diagnostics,
            Left    = 260, Top = 26,
            Width   = 180
        };
        grpAdv.Controls.Add(_diagCheck);

        Controls.Add(grpAdv);
        y += 70;

        // ── Action Buttons ────────────────────────────────────────────
        var btnCheckFlavour = MakeButton("🔄 Flavour Updates", 10, y, 145);
        btnCheckFlavour.Click += (_, _) => CheckFlavourUpdates();

        var btnCheckApp = MakeButton("⬆️ App Updates", 165, y, 130);
        btnCheckApp.Click += (_, _) => _ = Updater.CheckAsync(false);

        var btnSave = MakeButton("💾 Save & Apply", 305, y, 140);
        btnSave.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
        btnSave.ForeColor = System.Drawing.Color.White;
        btnSave.Click    += (_, _) => SaveSettings();

        var btnClose = MakeButton("✖ Close", 455, y, 95);
        btnClose.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { btnCheckFlavour, btnCheckApp, btnSave, btnClose });
        y += 38;

        // ── Status ────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Text      = "Ready",
            Left      = 10, Top = y,
            Width     = 540, Height = 22,
            ForeColor = System.Drawing.Color.DarkBlue
        };
        Controls.Add(_statusLabel);
    }

    private void SaveSettings()
    {
        try
        {
            var cfg = ConfigLoader.AppConfig;

            cfg.UrlServer          = _urlBox?.Text.Trim()        ?? cfg.UrlServer;
            cfg.KeyFile            = _keyBox?.Text.Trim()        ?? cfg.KeyFile;
            cfg.TokenFile          = _tokenFileBox?.Text.Trim()  ?? cfg.TokenFile;
            cfg.Token              = _tokenBox?.Text.Trim()      ?? cfg.Token;
            cfg.Flavour            = (string?)_flavourCombo?.SelectedItem ?? cfg.Flavour;
            cfg.Diagnostics        = _diagCheck?.Checked         ?? cfg.Diagnostics;
            cfg.ManualMode         = _manualCheck?.Checked       ?? cfg.ManualMode;
            cfg.FlavourPollSeconds = (int?)_pollBox?.Value       ?? cfg.FlavourPollSeconds;
            cfg.BrowserName        = (_browserCombo?.SelectedItem?.ToString() ?? "default").ToLowerInvariant();
            cfg.BrowserPath        = _browserPathBox?.Text.Trim() ?? cfg.BrowserPath;

            File.WriteAllText(ConfigLoader.ConfigPath,
                System.Text.Json.JsonSerializer.Serialize(cfg, ConfigLoader.JsonOpts));

            TokenManager.Reset();
            ConfigLoader.Reload();
            Status("✅ Settings saved and applied.");
        }
        catch (Exception ex)
        {
            Status($"❌ Error saving: {ex.Message}");
        }
    }

    private void CheckFlavourUpdates()
    {
        Status("Checking GitLab for flavour updates...");
        _ = Task.Run(async () =>
        {
            await ConfigLoader.CheckFlavourUpdateAsync();
            Invoke(() => Status("✅ Flavour check complete."));
        });
    }

    private void LoadLocalFlavour()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title  = "Select a flavour .json file"
        };

        if (ofd.ShowDialog() != DialogResult.OK) return;

        try
        {
            var name = Path.GetFileNameWithoutExtension(ofd.FileName);
            var dest = Path.Combine(ConfigLoader.FlavourDir, $"{name}.json");
            File.Copy(ofd.FileName, dest, overwrite: true);

            if (_flavourCombo != null && !_flavourCombo.Items.Contains(name))
                _flavourCombo.Items.Add(name);

            if (_flavourCombo != null)
                _flavourCombo.SelectedItem = name;

            Status($"✅ Loaded {name}.json into flavours folder.");
        }
        catch (Exception ex)
        {
            Status($"❌ Error loading file: {ex.Message}");
        }
    }

    private void Status(string msg)
    {
        if (_statusLabel == null) return;
        _statusLabel.Text      = msg;
        _statusLabel.ForeColor = msg.StartsWith("❌")
            ? System.Drawing.Color.DarkRed
            : System.Drawing.Color.DarkBlue;
    }

    private static Label MakeLabel(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y, Width = 130, TextAlign = ContentAlignment.MiddleRight };

    private static TextBox MakeTextBox(string text, int x, int y, int width, bool password = false) =>
        new() { Text = text, Left = x, Top = y, Width = width, UseSystemPasswordChar = password };

    private static Button MakeButton(string text, int x, int y, int width) =>
        new() { Text = text, Left = x, Top = y, Width = width, Height = 28 };
}
