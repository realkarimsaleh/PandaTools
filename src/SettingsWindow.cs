using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
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

    // ── Layout constants ──────────────────────────────────────────────
    // FormW = GrpL + GrpW + GrpL  →  10 + 580 + 10 = 600
    // This ensures no empty gap on the right.
    private const int GrpL  = 10;
    private const int GrpW  = 580;
    private const int FormW = 600;   // GrpL + GrpW + GrpL

    private const int LblL  = 8;
    private const int LblW  = 112;
    private const int FldL  = 124;                // LblL + LblW + 4
    private const int FldW  = GrpW - FldL - 12;  // 580 - 124 - 12 = 444

    // Flavour row (tighter label so buttons have room)
    private const int FlavLblL  = 6;
    private const int FlavLblW  = 88;
    private const int FlavFldL  = 97;             // FlavLblL + FlavLblW + 3
    private const int FlavCmbW  = 210;

    public SettingsWindow()
    {
        Text            = "PandaTools - Settings";
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        TopMost         = true;
        Icon            = AppIcon.Get();
        AutoScroll      = false;

        BuildLayout();

        // Height fits content exactly; width is fixed by constants above
        var lastBottom = Controls.Cast<Control>().Max(c => c.Bottom);
        ClientSize = new Size(FormW, lastBottom + 14);
    }

    private void BuildLayout()
    {
        var cfg = ConfigLoader.AppConfig;
        _runasProfiles = cfg.RunAsProfiles
            .Select(p => new RunAsProfile { Name = p.Name, Username = p.Username, Password = p.Password })
            .ToList();

        var y = 10;

        // ── Version Banner ─────────────────────────────────────────────
        var appVersion     = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var flavourVersion = ConfigLoader.FlavourConfig.Version;

        var pnlVersion = new Panel
        {
            Left = GrpL, Top = y, Width = GrpW, Height = 34,
            BackColor = Color.FromArgb(28, 28, 28)
        };
        pnlVersion.Controls.Add(new Label
        {
            Text      = $"🐼  PandaTools  v{appVersion}",
            Left = 10, Top = 7, Width = 210, Height = 20,
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        });
        pnlVersion.Controls.Add(new Label
        {
            Text      = $"Flavour: {cfg.Flavour}  •  v{flavourVersion}",
            Left = 220, Top = 7, Width = GrpW - 230, Height = 20,
            ForeColor = Color.FromArgb(170, 170, 170), BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleRight
        });
        Controls.Add(pnlVersion);
        y += 42;

        // ── Connection ─────────────────────────────────────────────────
        var grpConn = MakeGroup("Connection", y, 148);

        grpConn.Controls.Add(MakeLabel("GitLab Server:", LblL, 22));
        _urlBox = MakeTextBox(cfg.UrlServer, FldL, 20, FldW);
        grpConn.Controls.Add(_urlBox);

        grpConn.Controls.Add(MakeLabel("Key File:", LblL, 52));
        _keyBox = MakeTextBox(cfg.KeyFile, FldL, 50, FldW);
        grpConn.Controls.Add(_keyBox);

        grpConn.Controls.Add(MakeLabel("Token File:", LblL, 82));
        _tokenFileBox = MakeTextBox(cfg.TokenFile, FldL, 80, FldW);
        grpConn.Controls.Add(_tokenFileBox);

        grpConn.Controls.Add(MakeLabel("Plain Token:", LblL, 112));
        _tokenBox = MakeTextBox(cfg.Token, FldL, 110, FldW, password: true);
        grpConn.Controls.Add(_tokenBox);

        Controls.Add(grpConn);
        y += 158;

        // ── Flavour ─────────────────────────────────────────────────────
        var grpFlavour = MakeGroup("Flavour", y, 108);

        grpFlavour.Controls.Add(new Label
        {
            Text = "Active Flavour:", Left = FlavLblL, Top = 25,
            Width = FlavLblW, Height = 20,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9f)
        });

        _flavourCombo = new ComboBox
        {
            DropDownStyle    = ComboBoxStyle.DropDownList,
            Left             = FlavFldL, Top = 22,
            Width            = FlavCmbW,
            Font             = new Font("Segoe UI", 9f),
            MaxDropDownItems = 15,
            DropDownWidth    = 300
        };
        RefreshFlavourCombo(cfg.Flavour);
        grpFlavour.Controls.Add(_flavourCombo);

        // Buttons: sit right after combo, equal width, fill to group right edge
        int bxStart = FlavFldL + FlavCmbW + 6;
        int bxRemain = GrpW - bxStart - 8;  // remaining width for 3 buttons
        int bw3 = bxRemain / 3;             // each button width

        var btnFlavourAdd    = MakeButton("+ Add",    bxStart,          21, bw3 - 2);
        var btnFlavourRemove = MakeButton("− Remove", bxStart + bw3,    21, bw3 - 2);
        var btnFlavourFolder = MakeButton("📂 Folder", bxStart + bw3*2, 21, bw3 - 2);

        btnFlavourAdd.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "JSON files (*.json)|*.json", Title = "Import flavour .json" };
            if (ofd.ShowDialog() != DialogResult.OK) return;
            try
            {
                var name = Path.GetFileNameWithoutExtension(ofd.FileName);
                File.Copy(ofd.FileName, Path.Combine(ConfigLoader.FlavourDir, $"{name}.json"), overwrite: true);
                RefreshFlavourCombo(name);
                Status($"✅ Added: {name}");
            }
            catch (Exception ex) { Status($"❌ {ex.Message}"); }
        };

        btnFlavourRemove.Click += (_, _) =>
        {
            var sel = _flavourCombo?.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(sel)) { Status("❌ No flavour selected."); return; }
            if (sel.Equals(ConfigLoader.AppConfig.Flavour, StringComparison.OrdinalIgnoreCase))
            { Status("❌ Cannot remove the active flavour."); return; }
            if (MessageBox.Show($"Delete flavour '{sel}'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                var path = Path.Combine(ConfigLoader.FlavourDir, $"{sel}.json");
                if (File.Exists(path)) File.Delete(path);
                RefreshFlavourCombo(ConfigLoader.AppConfig.Flavour);
                Status($"✅ Removed: {sel}");
            }
            catch (Exception ex) { Status($"❌ {ex.Message}"); }
        };

        btnFlavourFolder.Click += (_, _) =>
        {
            Directory.CreateDirectory(ConfigLoader.FlavourDir);
            System.Diagnostics.Process.Start("explorer.exe", ConfigLoader.FlavourDir);
        };

        grpFlavour.Controls.AddRange(new Control[] { btnFlavourAdd, btnFlavourRemove, btnFlavourFolder });

        _manualCheck = new CheckBox
        {
            Text    = "Manual mode — disable auto-updates from GitLab",
            Checked = cfg.ManualMode,
            Left    = LblL, Top = 58,
            Width   = GrpW - 20,
            Font    = new Font("Segoe UI", 9f)
        };
        grpFlavour.Controls.Add(_manualCheck);

        grpFlavour.Controls.Add(new Label
        {
            Text      = "💡  Add imports a .json from disk  •  Remove deletes the file  •  Active flavour cannot be removed",
            Left      = LblL, Top = 84, Width = GrpW - 16, Height = 16,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f)
        });

        Controls.Add(grpFlavour);
        y += 118;

        // ── Private Browser ────────────────────────────────────────────
        var grpBrowser = MakeGroup("🕶️ Private Browsing", y, 96);

        grpBrowser.Controls.Add(MakeLabel("Browser:", LblL, 24));
        _browserCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = FldL, Top = 22, Width = 165,
            Font = new Font("Segoe UI", 9f)
        };
        _browserCombo.Items.AddRange(new object[] { "Default", "Chrome", "Edge", "Firefox", "Brave", "Custom" });
        foreach (var b in _browserCombo.Items)
            if (b.ToString()!.Equals(cfg.BrowserName, StringComparison.OrdinalIgnoreCase))
            { _browserCombo.SelectedItem = b; break; }
        if (_browserCombo.SelectedIndex < 0) _browserCombo.SelectedIndex = 0;
        grpBrowser.Controls.Add(_browserCombo);

        var btnBrowse = MakeButton("📁 Browse...", FldL + 171, 21, 100);
        btnBrowse.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe", Title = "Select browser .exe" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _browserPathBox!.Text      = ofd.FileName;
                _browserCombo.SelectedItem = "Custom";
            }
        };
        grpBrowser.Controls.Add(btnBrowse);

        grpBrowser.Controls.Add(MakeLabel("Custom Path:", LblL, 58));
        _browserPathBox = MakeTextBox(cfg.BrowserPath, FldL, 56, FldW);
        grpBrowser.Controls.Add(_browserPathBox);

        Controls.Add(grpBrowser);
        y += 106;

        // ── RunAs Profiles ─────────────────────────────────────────────
        var grpRunAs = MakeGroup("👤 RunAs Profiles", y, 188);

        _runasListBox = new ListBox
        {
            Left = LblL, Top = 22, Width = 165, Height = 154,
            Font = new Font("Segoe UI", 9f)
        };
        foreach (var p in _runasProfiles)
            _runasListBox.Items.Add(p.Name);
        _runasListBox.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        grpRunAs.Controls.Add(_runasListBox);

        const int pnlLeft = 182;
        var pnlRight = new Panel
        {
            Left   = pnlLeft, Top = 14,
            Width  = GrpW - pnlLeft - 8,
            Height = 172
        };

        const int rFldX = 96;
        int       rFldW = pnlRight.Width - rFldX - 2;

        pnlRight.Controls.Add(MakePanelLabel("Profile Name:", 0, 4));
        _runasNameBox = MakePanelBox(rFldX, 2, rFldW);
        pnlRight.Controls.Add(_runasNameBox);

        pnlRight.Controls.Add(MakePanelLabel("Username:", 0, 34));
        _runasUserBox = MakePanelBox(rFldX, 32, rFldW);
        pnlRight.Controls.Add(_runasUserBox);
        pnlRight.Controls.Add(new Label
        {
            Text = "Format: DOMAIN\\user  or  user@domain",
            Left = rFldX, Top = 53, Width = rFldW, Height = 15,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f)
        });

        pnlRight.Controls.Add(MakePanelLabel("Password:", 0, 72));
        _runasPassBox = MakePanelBox(rFldX, 70, rFldW, password: true);
        pnlRight.Controls.Add(_runasPassBox);
        pnlRight.Controls.Add(new Label
        {
            Text = "Leave blank → Windows prompts at launch",
            Left = rFldX, Top = 91, Width = rFldW, Height = 15,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f)
        });

        var btnAdd  = MakeButton("+ Add",    0,   112, 82);
        var btnSave = MakeButton("💾 Save",  88,  112, 82);
        var btnDel  = MakeButton("🗑 Delete", 176, 112, 82);

        btnAdd.Click += (_, _) =>
        {
            var p = new RunAsProfile { Name = "New Profile" };
            _runasProfiles.Add(p);
            _runasListBox.Items.Add(p.Name);
            _runasListBox.SelectedIndex = _runasListBox.Items.Count - 1;
        };

        btnSave.Click += (_, _) =>
        {
            if (_selectedProfileIndex < 0 || _selectedProfileIndex >= _runasProfiles.Count) return;
            _runasProfiles[_selectedProfileIndex].Name     = _runasNameBox?.Text.Trim() ?? "";
            _runasProfiles[_selectedProfileIndex].Username = _runasUserBox?.Text.Trim() ?? "";
            _runasProfiles[_selectedProfileIndex].Password = _runasPassBox?.Text        ?? "";
            _runasListBox.Items[_selectedProfileIndex]     = _runasProfiles[_selectedProfileIndex].Name;
            Status("✅ Profile saved — click 'Save & Apply' to write to disk.");
        };

        btnDel.Click += (_, _) =>
        {
            if (_selectedProfileIndex < 0 || _runasProfiles.Count <= 1)
            { Status("❌ Cannot delete — at least one profile required."); return; }
            _runasProfiles.RemoveAt(_selectedProfileIndex);
            _runasListBox.Items.RemoveAt(_selectedProfileIndex);
            if (_runasListBox.Items.Count > 0)
                _runasListBox.SelectedIndex = Math.Max(0, _selectedProfileIndex - 1);
        };

        pnlRight.Controls.AddRange(new Control[] { btnAdd, btnSave, btnDel });
        grpRunAs.Controls.Add(pnlRight);
        Controls.Add(grpRunAs);
        y += 198;

        // ── Advanced ───────────────────────────────────────────────────
        var grpAdv = MakeGroup("Advanced", y, 56);

        grpAdv.Controls.Add(MakeLabel("Poll Interval (s):", LblL, 22));
        _pollBox = new NumericUpDown
        {
            Minimum = 30, Maximum = 3600,
            Value   = Math.Max(30, cfg.FlavourPollSeconds),
            Left    = FldL, Top = 20, Width = 90,
            Font    = new Font("Segoe UI", 9f)
        };
        grpAdv.Controls.Add(_pollBox);

        _diagCheck = new CheckBox
        {
            Text    = "Enable Diagnostics",
            Checked = cfg.Diagnostics,
            Left    = FldL + 100, Top = 22, Width = 180,
            Font    = new Font("Segoe UI", 9f)
        };
        grpAdv.Controls.Add(_diagCheck);
        Controls.Add(grpAdv);
        y += 66;

        // ── Action Buttons — 4 equal widths spanning GrpW exactly ──────
        int btnW = (GrpW - 6) / 4;
        var btnCheckFlavour = MakeButton("🔄 Flavour Updates", GrpL,               y, btnW);
        var btnCheckApp     = MakeButton("⬆️ App Updates",     GrpL + btnW + 2,    y, btnW);
        var btnSaveApply    = MakeButton("💾 Save & Apply",    GrpL + (btnW+2)*2,  y, btnW);
        var btnClose        = MakeButton("✖ Close",            GrpL + (btnW+2)*3,  y, btnW);

        btnSaveApply.BackColor = Color.FromArgb(40, 167, 69);
        btnSaveApply.ForeColor = Color.White;
        btnSaveApply.FlatStyle = FlatStyle.Flat;

        btnCheckFlavour.Click += (_, _) => CheckFlavourUpdates();
        btnCheckApp.Click     += (_, _) => _ = Updater.CheckAsync(false);
        btnSaveApply.Click    += (_, _) => SaveSettings();
        btnClose.Click        += (_, _) => Close();

        Controls.AddRange(new Control[] { btnCheckFlavour, btnCheckApp, btnSaveApply, btnClose });
        y += 34;

        // ── Status ─────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Text      = "Ready",
            Left      = GrpL, Top = y,
            Width     = GrpW, Height = 20,
            ForeColor = Color.DarkBlue,
            Font      = new Font("Segoe UI", 9f)
        };
        Controls.Add(_statusLabel);

        if (_runasListBox.Items.Count > 0) _runasListBox.SelectedIndex = 0;
    }

    // ── Flavour combo refresh ──────────────────────────────────────────
    private void RefreshFlavourCombo(string selectName)
    {
        if (_flavourCombo == null) return;
        _flavourCombo.Items.Clear();
        foreach (var name in ConfigLoader.GetAvailableFlavours(includeHidden: true))
            _flavourCombo.Items.Add(name);
        _flavourCombo.SelectedItem = selectName;
        if (_flavourCombo.SelectedIndex < 0 && _flavourCombo.Items.Count > 0)
            _flavourCombo.SelectedIndex = 0;
    }

    // ── RunAs ──────────────────────────────────────────────────────────
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

    // ── Save ───────────────────────────────────────────────────────────
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
        catch (Exception ex) { Status($"❌ Save failed: {ex.Message}"); }
    }

    private void CheckFlavourUpdates()
    {
        Status("⏳ Checking GitLab for flavour updates...");
        _ = Task.Run(async () =>
        {
            await ConfigLoader.CheckFlavourUpdateAsync();
            Invoke(() => Status("✅ Flavour check complete."));
        });
    }

    private void Status(string msg)
    {
        if (_statusLabel == null) return;
        _statusLabel.Text      = msg;
        _statusLabel.ForeColor = msg.StartsWith("❌") ? Color.DarkRed : Color.DarkBlue;
    }

    // ── Control factories ──────────────────────────────────────────────
    private static GroupBox MakeGroup(string text, int top, int height) =>
        new() { Text = text, Left = GrpL, Top = top, Width = GrpW, Height = height, Font = new Font("Segoe UI", 9f) };

    private static Label MakeLabel(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y + 3, Width = LblW, Height = 20, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9f) };

    private static Label MakePanelLabel(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y + 3, Width = 92, Height = 20, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9f) };

    private static TextBox MakeTextBox(string text, int x, int y, int width, bool password = false) =>
        new() { Text = text, Left = x, Top = y, Width = width, UseSystemPasswordChar = password, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };

    private static TextBox MakePanelBox(int x, int y, int width, bool password = false) =>
        new() { Left = x, Top = y, Width = width, UseSystemPasswordChar = password, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };

    private static Button MakeButton(string text, int x, int y, int width) =>
        new() { Text = text, Left = x, Top = y, Width = width, Height = 27, Font = new Font("Segoe UI", 9f) };
}
