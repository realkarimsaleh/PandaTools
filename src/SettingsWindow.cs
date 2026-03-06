using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

public class SettingsWindow : Form
{
    // All nullable — assigned in BuildLayout(), called from constructor
    private TextBox?       _urlBox;
    private TextBox?       _keyBox;
    private TextBox?       _tokenBox;
    private TextBox?       _tokenFileBox;
    private ComboBox?      _flavourCombo;
    private NumericUpDown? _pollBox;
    private CheckBox?      _diagCheck;
    private CheckBox?      _manualCheck;
    private Label?         _statusLabel;

    public SettingsWindow()
    {
        Text            = "PandaTools — Settings";
        Size            = new(560, 520);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        TopMost         = true;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var cfg = ConfigLoader.AppConfig;

        // ── Connection group ──────────────────────────────────────────
        var grpConn = new GroupBox
        {
            Text   = "Connection",
            Left   = 10, Top = 10,
            Width  = 520, Height = 140
        };

        grpConn.Controls.Add(MakeLabel("GitLab Server URL:", 10, 22));
        _urlBox = MakeTextBox(cfg.UrlServer, 130, 20, 370);
        grpConn.Controls.Add(_urlBox);

        grpConn.Controls.Add(MakeLabel("Key File:", 10, 52));
        _keyBox = MakeTextBox(cfg.KeyFile, 130, 50, 370);
        grpConn.Controls.Add(_keyBox);

        grpConn.Controls.Add(MakeLabel("Token File:", 10, 82));
        _tokenFileBox = MakeTextBox(cfg.TokenFile, 130, 80, 370);
        grpConn.Controls.Add(_tokenFileBox);

        grpConn.Controls.Add(MakeLabel("Plain Token:", 10, 112));
        _tokenBox = MakeTextBox(cfg.Token, 130, 110, 370, password: true);
        grpConn.Controls.Add(_tokenBox);

        // ── Flavour group ─────────────────────────────────────────────
        var grpFlavour = new GroupBox
        {
            Text   = "Flavour",
            Left   = 10, Top = 158,
            Width  = 520, Height = 90
        };

        grpFlavour.Controls.Add(MakeLabel("Active Flavour:", 10, 22));
        _flavourCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 130, Top = 20, Width = 250
        };
        foreach (var name in ConfigLoader.GetAvailableFlavours(includeHidden: true))
            _flavourCombo.Items.Add(name);
        _flavourCombo.SelectedItem = cfg.Flavour;
        grpFlavour.Controls.Add(_flavourCombo);

        var btnLoadLocal = MakeButton("📁 Load Local .json", 390, 19, 120);
        btnLoadLocal.Click += (_, _) => LoadLocalFlavour();
        grpFlavour.Controls.Add(btnLoadLocal);

        _manualCheck = new CheckBox
        {
            Text    = "Manual mode — disable auto-updates from GitLab",
            Checked = cfg.ManualMode,
            Left    = 10, Top = 56,
            Width   = 400
        };
        grpFlavour.Controls.Add(_manualCheck);

        // ── Advanced group ────────────────────────────────────────────
        var grpAdv = new GroupBox
        {
            Text   = "Advanced",
            Left   = 10, Top = 256,
            Width  = 520, Height = 80
        };

        grpAdv.Controls.Add(MakeLabel("Poll Interval (sec):", 10, 30));
        _pollBox = new NumericUpDown
        {
            Minimum = 30, Maximum = 3600,
            Value   = Math.Max(30, cfg.FlavourPollSeconds),
            Left    = 140, Top = 28, Width = 80
        };
        grpAdv.Controls.Add(_pollBox);

        _diagCheck = new CheckBox
        {
            Text    = "Enable Diagnostics",
            Checked = cfg.Diagnostics,
            Left    = 240, Top = 30, Width = 160
        };
        grpAdv.Controls.Add(_diagCheck);

        // ── Action buttons ────────────────────────────────────────────
        var btnCheckFlavour = MakeButton("🔄 Check Flavour Updates", 10, 350, 170);
        btnCheckFlavour.Click += (_, _) => CheckFlavourUpdates();

        var btnCheckApp = MakeButton("⬆️ Check App Updates", 190, 350, 155);
        btnCheckApp.Click += (_, _) => _ = Updater.CheckAsync(false);

        var btnSave = MakeButton("💾 Save & Apply", 355, 350, 175);
        btnSave.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
        btnSave.ForeColor = System.Drawing.Color.White;
        btnSave.Click    += (_, _) => SaveSettings();

        // ── Status bar ────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Text      = "Ready",
            Left      = 10, Top = 395,
            Width     = 520, Height = 20,
            ForeColor = System.Drawing.Color.DarkBlue
        };

        var btnClose = MakeButton("Close", 430, 420, 100);
        btnClose.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            grpConn, grpFlavour, grpAdv,
            btnCheckFlavour, btnCheckApp, btnSave,
            _statusLabel, btnClose
        });
    }

    private void SaveSettings()
    {
        try
        {
            var cfg = ConfigLoader.AppConfig;

            cfg.UrlServer          = _urlBox?.Text ?? cfg.UrlServer;
            cfg.KeyFile            = _keyBox?.Text ?? cfg.KeyFile;
            cfg.TokenFile          = _tokenFileBox?.Text ?? cfg.TokenFile;
            cfg.Token              = _tokenBox?.Text ?? cfg.Token;
            cfg.Flavour            = (string?)_flavourCombo?.SelectedItem ?? cfg.Flavour;
            cfg.Diagnostics        = _diagCheck?.Checked ?? cfg.Diagnostics;
            cfg.ManualMode         = _manualCheck?.Checked ?? cfg.ManualMode;
            cfg.FlavourPollSeconds = (int?)_pollBox?.Value ?? cfg.FlavourPollSeconds;

            File.WriteAllText(ConfigLoader.ConfigPath,
                System.Text.Json.JsonSerializer.Serialize(cfg, ConfigLoader.JsonOpts));

            TokenManager.Reset();
            ConfigLoader.Reload();
            Status("✅ Settings saved and applied.");
        }
        catch (Exception ex)
        {
            Status($"Error saving: {ex.Message}");
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
            var name = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
            var dest = System.IO.Path.Combine(ConfigLoader.FlavourDir, $"{name}.json");
            File.Copy(ofd.FileName, dest, overwrite: true);

            if (_flavourCombo != null && !_flavourCombo.Items.Contains(name))
                _flavourCombo.Items.Add(name);

            if (_flavourCombo != null)
                _flavourCombo.SelectedItem = name;

            Status($"✅ Loaded {name}.json into flavours folder.");
        }
        catch (Exception ex)
        {
            Status($"Error loading file: {ex.Message}");
        }
    }

    private void Status(string msg)
    {
        if (_statusLabel == null) return;
        _statusLabel.Text      = msg;
        _statusLabel.ForeColor = msg.StartsWith("Error") || msg.StartsWith("❌")
            ? System.Drawing.Color.DarkRed
            : System.Drawing.Color.DarkBlue;
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private static Label MakeLabel(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y, Width = 120, TextAlign = ContentAlignment.MiddleRight };

    private static TextBox MakeTextBox(string text, int x, int y, int width, bool password = false) =>
        new() { Text = text, Left = x, Top = y, Width = width, UseSystemPasswordChar = password };

    private static Button MakeButton(string text, int x, int y, int width) =>
        new() { Text = text, Left = x, Top = y, Width = width, Height = 28 };
}
