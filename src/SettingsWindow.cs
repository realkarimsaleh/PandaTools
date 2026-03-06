using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

public class SettingsWindow : Form
{
    private TextBox?       _urlBox;
    private TextBox?       _keyBox;
    private TextBox?       _tokenFileBox;
    private TextBox?       _tokenBox;
    private TextBox?       _browserPathBox;
    private TextBox?       _runasUserBox;
    private TextBox?       _runasPassBox;
    private TextBox?       _runasNameBox;
    private ComboBox?      _flavourCombo;
    private ComboBox?      _browserCombo;
    private ListBox?       _runasListBox;
    private NumericUpDown? _pollBox;
    private CheckBox?      _diagCheck;
    private CheckBox?      _manualCheck;
    private Label?         _statusLabel;

    private List<RunAsProfile> _runasProfiles = new();
    private int _selectedProfileIndex = -1;

    public SettingsWindow()
    {
        Text            = "PandaTools — Settings";
        Size            = new(600, 720);
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
        _runasProfiles = cfg.RunAsProfiles
            .Select(p => new RunAsProfile { Name = p.Name, Username = p.Username, Password = p.Password })
            .ToList();

        var y = 10;

        // ── Connection ────────────────────────────────────────────────
        var grpConn = new GroupBox { Text = "Connection", Left = 10, Top = y, Width = 560, Height = 142 };

        grpConn.Controls.Add(MakeLabel("GitLab Server:", 10, 24));
        _urlBox = MakeTextBox(cfg.UrlServer, 130, 22, 410);
        grpConn.Controls.Add(_urlBox);

        grpConn.Controls.Add(MakeLabel("Key File:", 10, 54));
        _keyBox = MakeTextBox(cfg.KeyFile, 130, 52, 410);
        grpConn.Controls.Add(_keyBox);

        grpConn.Controls.Add(MakeLabel("Token File:", 10, 84));
        _tokenFileBox = MakeTextBox(cfg.TokenFile, 130, 82, 410);
        grpConn.Controls.Add(_tokenFileBox);

        grpConn.Controls.Add(MakeLabel("Plain Token:", 10, 114));
        _tokenBox = MakeTextBox(cfg.Token, 130, 112, 410, password: true);
        grpConn.Controls.Add(_tokenBox);

        Controls.Add(grpConn);
        y += 152;

        // ── Flavour ───────────────────────────────────────────────────
        var grpFlavour = new GroupBox { Text = "Flavour", Left = 10, Top = y, Width = 560, Height = 88 };

        grpFlavour.Controls.Add(MakeLabel("Active Flavour:", 10, 24));
        _flavourCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 130, Top = 22, Width = 260
        };
        foreach (var name in ConfigLoader.GetAvailableFlavours(includeHidden: true))
            _flavourCombo.Items.Add(name);
        _flavourCombo.SelectedItem = cfg.Flavour;
        grpFlavour.Controls.Add(_flavourCombo);

        var btnLoadLocal = MakeButton("📁 Load Local .json", 400, 21, 145);
        btnLoadLocal.Click += (_, _) => LoadLocalFlavour();
        grpFlavour.Controls.Add(btnLoadLocal);

        _manualCheck = new CheckBox
        {
            Text = "Manual mode — disable auto-updates from GitLab",
            Checked = cfg.ManualMode,
            Left = 10, Top = 56, Width = 430
        };
        grpFlavour.Controls.Add(_manualCheck);

        Controls.Add(grpFlavour);
        y += 98;

        // ── Private Browser ───────────────────────────────────────────
        var grpBrowser = new GroupBox { Text = "🕶️ Private Browsing", Left = 10, Top = y, Width = 560, Height = 100 };

        grpBrowser.Controls.Add(MakeLabel("Browser:", 10, 24));
        _browserCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 130, Top = 22, Width = 160
        };
        _browserCombo.Items.AddRange(new object[] { "Default", "Chrome", "Edge", "Firefox", "Brave", "Custom" });
        foreach (var b in _browserCombo.Items)
        {
            if (b.ToString()!.Equals(cfg.BrowserName, StringComparison.OrdinalIgnoreCase))
            {
                _browserCombo.SelectedItem = b;
                break;
            }
        }
        if (_browserCombo.SelectedIndex < 0) _browserCombo.SelectedIndex = 0;
        grpBrowser.Controls.Add(_browserCombo);

        var btnBrowseBrowser = MakeButton("📁 Browse...", 300, 21, 90);
        btnBrowseBrowser.Click += (_, _) =>
        {
            var ofd = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe", Title = "Select Browser .exe" };
            if (ofd.ShowDialog() == DialogResult.OK && _browserPathBox != null)
            {
                _browserPathBox.Text    = ofd.FileName;
                _browserCombo.SelectedItem = "Custom";
            }
        };
        grpBrowser.Controls.Add(btnBrowseBrowser);

        grpBrowser.Controls.Add(MakeLabel("Custom Path:", 10, 60));
        _browserPathBox = MakeTextBox(cfg.BrowserPath, 130, 58, 260);
        grpBrowser.Controls.Add(_browserPathBox);

        Controls.Add(grpBrowser);
        y += 110;

        // ── RunAs Profiles ────────────────────────────────────────────
        var grpRunAs = new GroupBox { Text = "👤 RunAs Profiles", Left = 10, Top = y, Width = 560, Height = 175 };

        _runasListBox = new ListBox { Left = 10, Top = 22, Width = 180, Height = 140 };
        foreach (var p in _runasProfiles)
            _runasListBox.Items.Add(p.Name);
        _runasListBox.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        grpRunAs.Controls.Add(_runasListBox);

        grpRunAs.Controls.Add(MakeLabel("Profile Name:", 200, 24));
        _runasNameBox = MakeTextBox("", 310, 22, 230);
        grpRunAs.Controls.Add(_runasNameBox);

        grpRunAs.Controls.Add(MakeLabel("Username:", 200, 54));
        _runasUserBox = MakeTextBox("", 310, 52, 230);
        grpRunAs.Controls.Add(_runasUserBox);
        grpRunAs.Controls.Add(new Label
        {
            Text = "Format: DOMAIN\\user or user@domain",
            Left = 310, Top = 74, Width = 230,
            ForeColor = System.Drawing.Color.Gray,
            Font = new System.Drawing.Font("Segoe UI", 7f)
        });

        grpRunAs.Controls.Add(MakeLabel("Password:", 200, 94));
        _runasPassBox = MakeTextBox("", 310, 92, 230, password: true);
        grpRunAs.Controls.Add(_runasPassBox);
        grpRunAs.Controls.Add(new Label
        {
            Text = "Leave blank to prompt at launch",
            Left = 310, Top = 114, Width = 230,
            ForeColor = System.Drawing.Color.Gray,
            Font = new System.Drawing.Font("Segoe UI", 7f)
        });

        var btnAddProfile  = MakeButton("+ Add",   200, 140, 70);
        var btnSaveProfile = MakeButton("💾 Save",  280, 140, 70);
        var btnDelProfile  = MakeButton("🗑 Delete", 360, 140, 80);

        btnAddProfile.Click += (_, _) =>
        {
            var p = new RunAsProfile { Name = "New Profile", Username = "", Password = "" };
            _runasProfiles.Add(p);
            _runasListBox.Items.Add(p.Name);
            _runasListBox.SelectedIndex = _runasListBox.Items.Count - 1;
        };

        btnSaveProfile.Click += (_, _) =>
        {
            if (_selectedProfileIndex < 0 || _selectedProfileIndex >= _runasProfiles.Count) return;
            _runasProfiles[_selectedProfileIndex].Name     = _runasNameBox?.Text ?? "";
            _runasProfiles[_selectedProfileIndex].Username = _runasUserBox?.Text ?? "";
            _runasProfiles[_selectedProfileIndex].Password = _runasPassBox?.Text ?? "";
            _runasListBox.Items[_selectedProfileIndex]     = _runasProfiles[_selectedProfileIndex].Name;
            Status("✅ Profile saved — click 'Save & Apply' to write to config.");
        };

        btnDelProfile.Click += (_, _) =>
        {
            if (_selectedProfileIndex < 0 || _runasProfiles.Count <= 1)
            {
                Status("❌ Cannot delete — at least one profile required.");
                return;
            }
            _runasProfiles.RemoveAt(_selectedProfileIndex);
            _runasListBox.Items.RemoveAt(_selectedProfileIndex);
            _runasListBox.SelectedIndex = 0;
        };

        grpRunAs.Controls.AddRange(new Control[] { btnAddProfile, btnSaveProfile, btnDelProfile });
        Controls.Add(grpRunAs);
        y += 185;

        // ── Advanced ──────────────────────────────────────────────────
        var grpAdv = new GroupBox { Text = "Advanced", Left = 10, Top = y, Width = 560, Height = 58 };

        grpAdv.Controls.Add(MakeLabel("Poll Interval (s):", 10, 26));
        _pollBox = new NumericUpDown
        {
            Minimum = 30, Maximum = 3600,
            Value   = Math.Max(30, cfg.FlavourPollSeconds),
            Left = 130, Top = 24, Width = 80
        };
        grpAdv.Controls.Add(_pollBox);

        _diagCheck = new CheckBox
        {
            Text    = "Enable Diagnostics",
            Checked = cfg.Diagnostics,
            Left    = 250, Top = 26, Width = 180
        };
        grpAdv.Controls.Add(_diagCheck);

        Controls.Add(grpAdv);
        y += 68;

        // ── Action Buttons ────────────────────────────────────────────
        var btnCheckFlavour = MakeButton("🔄 Flavour Updates", 10, y, 150);
        btnCheckFlavour.Click += (_, _) => CheckFlavourUpdates();

        var btnCheckApp = MakeButton("⬆️ App Updates", 170, y, 130);
        btnCheckApp.Click += (_, _) => _ = Updater.CheckAsync(false);

        var btnSave = MakeButton("💾 Save & Apply", 310, y, 145);
        btnSave.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
        btnSave.ForeColor = System.Drawing.Color.White;
        btnSave.Click += (_, _) => SaveSettings();

        var btnClose = MakeButton("✖ Close", 465, y, 95);
        btnClose.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { btnCheckFlavour, btnCheckApp, btnSave, btnClose });
        y += 38;

        // ── Status ────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Text      = "Ready",
            Left      = 10, Top = y,
            Width     = 560, Height = 22,
            ForeColor = System.Drawing.Color.DarkBlue
        };
        Controls.Add(_statusLabel);

        // Select first runas profile
        if (_runasListBox.Items.Count > 0)
            _runasListBox.SelectedIndex = 0;
    }

    private void LoadSelectedProfile()
    {
        if (_runasListBox == null) return;
        _selectedProfileIndex = _runasListBox.SelectedIndex;
        if (_selectedProfileIndex < 0 || _selectedProfileIndex >= _runasProfiles.Count) return;

        var p = _runasProfiles[_selectedProfileIndex];
        if (_runasNameBox != null) _runasNameBox.Text = p.Name;
        if (_runasUserBox != null) _runasUserBox.Text = p.Username;
        if (_runasPassBox != null) _runasPassBox.Text = p.Password;
    }

    private void SaveSettings()
    {
        try
        {
            var cfg = ConfigLoader.AppConfig;

            cfg.UrlServer          = _urlBox?.Text.Trim()          ?? cfg.UrlServer;
            cfg.KeyFile            = _keyBox?.Text.Trim()          ?? cfg.KeyFile;
            cfg.TokenFile          = _tokenFileBox?.Text.Trim()    ?? cfg.TokenFile;
            cfg.Token              = _tokenBox?.Text.Trim()        ?? cfg.Token;
            cfg.Flavour            = (string?)_flavourCombo?.SelectedItem ?? cfg.Flavour;
            cfg.Diagnostics        = _diagCheck?.Checked           ?? cfg.Diagnostics;
            cfg.ManualMode         = _manualCheck?.Checked         ?? cfg.ManualMode;
            cfg.FlavourPollSeconds = (int?)_pollBox?.Value         ?? cfg.FlavourPollSeconds;
            cfg.BrowserName        = (_browserCombo?.SelectedItem?.ToString() ?? "default").ToLowerInvariant();
            cfg.BrowserPath        = _browserPathBox?.Text.Trim()  ?? cfg.BrowserPath;
            cfg.RunAsProfiles      = _runasProfiles;

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
        new() { Text = text, Left = x, Top = y, Width = 120, TextAlign = ContentAlignment.MiddleRight };

    private static TextBox MakeTextBox(string text, int x, int y, int width, bool password = false) =>
        new() { Text = text, Left = x, Top = y, Width = width, UseSystemPasswordChar = password };

    private static Button MakeButton(string text, int x, int y, int width) =>
        new() { Text = text, Left = x, Top = y, Width = width, Height = 28 };
}
