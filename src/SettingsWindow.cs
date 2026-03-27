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
    //######################################
    //Singleton Instance Tracker
    //######################################
    private static SettingsWindow? _instance;

    public static void ShowWindow()
    {
        if (_instance == null || _instance.IsDisposed)
        {
            _instance = new SettingsWindow();
            _instance.Show();
        }
        else
        {
            if (_instance.WindowState == FormWindowState.Minimized)
                _instance.WindowState = FormWindowState.Normal;

            _instance.BringToFront();
            _instance.Activate();
        }
    }

    private TextBox?       _urlBox;
    private TextBox?       _tokenPlainBox;
    private TextBox?       _browserPathBox;
    private TextBox?       _urlBrowserPathBox;
    private TextBox?       _runasUserBox;
    private TextBox?       _runasPassBox;
    private TextBox?       _runasNameBox;
    private TextBox?       _appRepoPathBox;
    private ComboBox?      _flavourCombo;
    private ComboBox?      _browserCombo;
    private ComboBox?      _urlBrowserCombo;
    private ListBox?       _runasListBox;
    private NumericUpDown? _pollBox;
    private NumericUpDown? _projectIdBox;
    private NumericUpDown? _appProjectIdBox;
    private NumericUpDown? _warnDaysBox;
    private CheckBox?      _diagCheck;
    private CheckBox?      _manualCheck;
    private CheckBox?      _onlySubscribedCheck;
    private Label?         _statusLabel;

    private List<RunAsProfile> _runasProfiles = new();
    private int _selectedProfileIndex = -1;

    private const int GrpL  = 10;
    private const int GrpW  = 580;
    private const int FormW = 600;

    private const int LblL = 8;
    private const int LblW = 112;
    private const int FldL = 124;
    private const int FldW = GrpW - FldL - 12;

    private const int FlavLblL = 6;
    private const int FlavLblW = 88;
    private const int FlavFldL = 97;
    private const int FlavCmbW = 210;

    //Changed to private to force the use of ShowWindow()
    private SettingsWindow()
    {
        Text            = "PandaTools - Settings";
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        TopMost         = true;
        Icon            = AppIcon.Get();
        AutoScroll      = false;

        BuildLayout();

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

        //######################################
        //Version banner
        //######################################
        var appVersion     = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var flavourVersion = ConfigLoader.FlavourConfig.Version;

        var pnlVersion = new Panel
        {
            Left = GrpL, Top = y, Width = GrpW, Height = 34,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        pnlVersion.Controls.Add(new Label
        {
            Text      = $"🐼 PandaTools v{appVersion}",
            Left = 10, Top = 7, Width = 210, Height = 20,
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        });

        pnlVersion.Controls.Add(new Label
        {
            Text      = $"Flavour: {cfg.Flavour} • v{flavourVersion}",
            Left = 220, Top = 7, Width = GrpW - 230, Height = 20,
            ForeColor = Color.FromArgb(170, 170, 170), BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleRight
        });

        Controls.Add(pnlVersion);
        y += 42;

        //######################################
        //Connection
        //######################################
        var grpConn = MakeGroup("Connection", y, 142);

        grpConn.Controls.Add(MakeLabel("GitLab Server:", LblL, 22));
        _urlBox = MakeTextBox(cfg.UrlServer, FldL, 20, FldW);
        grpConn.Controls.Add(_urlBox);

        grpConn.Controls.Add(MakeLabel("Project ID:", LblL, 52));
        _projectIdBox = new NumericUpDown
        {
            Minimum = 0, Maximum = 999999,
            Value   = Math.Max(0, cfg.FlavourProjectId),
            Left    = FldL, Top = 50, Width = 120,
            Font    = new Font("Segoe UI", 9f)
        };
        grpConn.Controls.Add(_projectIdBox);
        grpConn.Controls.Add(new Label
        {
            Text      = "GitLab project ID used for flavour polling",
            Left = FldL + 126, Top = 53, Width = 290, Height = 16,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f)
        });

        grpConn.Controls.Add(MakeLabel("New Token:", LblL, 82));

        const int tkBtnW = 110;
        const int tkGap  = 4;
        int       tkBoxW = FldW - (tkBtnW * 2) - (tkGap * 2);

        _tokenPlainBox = MakeTextBox("", FldL, 80, tkBoxW, password: true);
        _tokenPlainBox.PlaceholderText = "Leave blank to keep current token";
        grpConn.Controls.Add(_tokenPlainBox);

        var btnUpdateToken = MakeButton("🔑 Update Token", FldL + tkBoxW + tkGap, 79, tkBtnW);
        btnUpdateToken.BackColor = Color.FromArgb(0, 123, 255);
        btnUpdateToken.ForeColor = Color.White;
        btnUpdateToken.FlatStyle = FlatStyle.Flat;
        btnUpdateToken.Click += (_, _) =>
        {
            var plain = _tokenPlainBox?.Text.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(plain))
            { Status("❌ Token field is empty, nothing updated"); return; }
            TokenManager.SaveToken(plain);
            if (_tokenPlainBox != null) _tokenPlainBox.Text = "";
            Status("✅ Token encrypted and saved");
        };
        grpConn.Controls.Add(btnUpdateToken);

        var btnCheckExpiry = MakeButton("🔍 Check Expiry", FldL + tkBoxW + tkGap + tkBtnW + tkGap, 79, tkBtnW);
        btnCheckExpiry.Click += async (_, _) =>
        {
            Status("⏳ Checking token expiry...");
            var result = await TokenExpiryChecker.GetExpiryInfoAsync();
            Status(result);
        };
        grpConn.Controls.Add(btnCheckExpiry);

        grpConn.Controls.Add(new Label
        {
            Text      = "💡 Token is encrypted with DPAPI (per-user, this machine only). Plain-text is never stored.",
            Left = LblL, Top = 112, Width = GrpW - 16, Height = 16,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f)
        });

        Controls.Add(grpConn);
        y += 152;

        //######################################
        //Flavour
        //######################################
        var grpFlavour = MakeGroup("Flavour", y, 134);

        grpFlavour.Controls.Add(new Label
        {
            Text      = "Active Flavour:", Left = FlavLblL, Top = 25,
            Width     = FlavLblW, Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 9f)
        });

        _flavourCombo = new ComboBox
        {
            DropDownStyle    = ComboBoxStyle.DropDownList,
            Left = FlavFldL, Top = 22, Width = FlavCmbW,
            Font             = new Font("Segoe UI", 9f),
            MaxDropDownItems = 15,
            DropDownWidth    = 300
        };

        RefreshFlavourCombo(cfg.Flavour);
        grpFlavour.Controls.Add(_flavourCombo);

        int bxStart  = FlavFldL + FlavCmbW + 6;
        int bxRemain = GrpW - bxStart - 8;
        int bw3      = bxRemain / 3;

        var btnFlavourAdd    = MakeButton("+ Add",     bxStart,         21, bw3 - 2);
        var btnFlavourRemove = MakeButton("− Remove",  bxStart + bw3,   21, bw3 - 2);
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
            if (string.IsNullOrEmpty(sel)) { Status("❌ No flavour selected"); return; }
            if (sel.Equals(ConfigLoader.AppConfig.Flavour, StringComparison.OrdinalIgnoreCase))
            { Status("❌ Cannot remove the active flavour"); return; }
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
            Text    = "Manual mode - Disable auto-updates from GitLab",
            Checked = cfg.ManualMode,
            Left = LblL, Top = 58, Width = GrpW - 20,
            Font    = new Font("Segoe UI", 9f)
        };
        grpFlavour.Controls.Add(_manualCheck);

        //Local Flavour Toggle
        _onlySubscribedCheck = new CheckBox
        {
            Text    = "Hide Personal Menu (This will only show GitLab Subscribed menu)",
            Checked = cfg.ShowOnlySubscribedFlavour,
            Left = LblL, Top = 82, Width = GrpW - 20,
            Font    = new Font("Segoe UI", 9f)
        };
        grpFlavour.Controls.Add(_onlySubscribedCheck);

        grpFlavour.Controls.Add(new Label
        {
            Text      = "💡 Add imports a .json from disk • Remove deletes the file • Active flavour cannot be removed",
            Left = LblL, Top = 108, Width = GrpW - 16, Height = 16,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f)
        });

        Controls.Add(grpFlavour);
        y += 144;

        //######################################
        //Browser
        //######################################
        var grpBrowser = MakeGroup("🌐 Browser", y, 148);

        grpBrowser.Controls.Add(MakeLabel("Default:", LblL, 22));
        _urlBrowserCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = FldL, Top = 20, Width = 165,
            Font = new Font("Segoe UI", 9f)
        };
        _urlBrowserCombo.Items.AddRange(new object[] { "Default", "Chrome", "Edge", "Firefox", "Brave", "Custom" });
        foreach (var b in _urlBrowserCombo.Items)
            if (b.ToString()!.Equals(cfg.UrlBrowserName, StringComparison.OrdinalIgnoreCase))
            { _urlBrowserCombo.SelectedItem = b; break; }
        if (_urlBrowserCombo.SelectedIndex < 0) _urlBrowserCombo.SelectedIndex = 0;
        grpBrowser.Controls.Add(_urlBrowserCombo);

        var btnBrowseUrl = MakeButton("📁 Browse...", FldL + 171, 19, 100);
        btnBrowseUrl.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe", Title = "Select browser .exe" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _urlBrowserPathBox!.Text      = ofd.FileName;
                _urlBrowserCombo.SelectedItem = "Custom";
            }
        };
        grpBrowser.Controls.Add(btnBrowseUrl);

        grpBrowser.Controls.Add(MakeLabel("Custom Path:", LblL, 50));
        _urlBrowserPathBox = MakeTextBox(cfg.UrlBrowserPath, FldL, 48, FldW);
        grpBrowser.Controls.Add(_urlBrowserPathBox);

        grpBrowser.Controls.Add(new Panel
        {
            Left = LblL, Top = 76, Width = GrpW - 20, Height = 1,
            BackColor = Color.FromArgb(220, 220, 220)
        });

        grpBrowser.Controls.Add(MakeLabel("Incognito:", LblL, 86));
        _browserCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = FldL, Top = 84, Width = 165,
            Font = new Font("Segoe UI", 9f)
        };
        _browserCombo.Items.AddRange(new object[] { "Default", "Chrome", "Edge", "Firefox", "Brave", "Custom" });
        foreach (var b in _browserCombo.Items)
            if (b.ToString()!.Equals(cfg.BrowserName, StringComparison.OrdinalIgnoreCase))
            { _browserCombo.SelectedItem = b; break; }
        if (_browserCombo.SelectedIndex < 0) _browserCombo.SelectedIndex = 0;
        grpBrowser.Controls.Add(_browserCombo);

        var btnBrowseIncognito = MakeButton("📁 Browse...", FldL + 171, 83, 100);
        btnBrowseIncognito.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe", Title = "Select browser .exe" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _browserPathBox!.Text      = ofd.FileName;
                _browserCombo.SelectedItem = "Custom";
            }
        };
        grpBrowser.Controls.Add(btnBrowseIncognito);

        grpBrowser.Controls.Add(MakeLabel("Custom Path:", LblL, 116));
        _browserPathBox = MakeTextBox(cfg.BrowserPath, FldL, 114, FldW);
        grpBrowser.Controls.Add(_browserPathBox);

        Controls.Add(grpBrowser);
        y += 158;

        //######################################
        //RunAs profiles
        //######################################
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
        int rFldW = pnlRight.Width - rFldX - 2;

        pnlRight.Controls.Add(MakePanelLabel("Profile Name:", 0, 4));
        _runasNameBox = MakePanelBox(rFldX, 2, rFldW);
        pnlRight.Controls.Add(_runasNameBox);

        pnlRight.Controls.Add(MakePanelLabel("Username:", 0, 34));
        _runasUserBox = MakePanelBox(rFldX, 32, rFldW);
        pnlRight.Controls.Add(_runasUserBox);
        pnlRight.Controls.Add(new Label
        {
            Text = "Format: DOMAIN\\user or user@domain",
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

        int rbw = (rFldW - 4) / 3;
        var btnAdd  = MakeButton("+ Add",     rFldX,               112, rbw);
        var btnSave = MakeButton("💾 Save",   rFldX + rbw + 2,     112, rbw);
        var btnDel  = MakeButton("🗑 Delete", rFldX + rbw * 2 + 4, 112, rbw);

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
            _runasProfiles[_selectedProfileIndex].Password = _runasPassBox?.Text ?? "";
            _runasListBox.Items[_selectedProfileIndex] = _runasProfiles[_selectedProfileIndex].Name;
            Status("✅ Profile saved – click 'Save & Apply' to write to disk");
        };

        btnDel.Click += (_, _) =>
        {
            if (_selectedProfileIndex < 0 || _runasProfiles.Count <= 1)
            { Status("❌ Cannot delete, at least one profile required"); return; }
            _runasProfiles.RemoveAt(_selectedProfileIndex);
            _runasListBox.Items.RemoveAt(_selectedProfileIndex);
            if (_runasListBox.Items.Count > 0)
                _runasListBox.SelectedIndex = Math.Max(0, _selectedProfileIndex - 1);
        };

        pnlRight.Controls.AddRange(new Control[] { btnAdd, btnSave, btnDel });
        grpRunAs.Controls.Add(pnlRight);
        Controls.Add(grpRunAs);
        y += 198;

        //######################################
        //Advanced
        //######################################
        var grpAdv = MakeGroup("Advanced", y, 200);

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

        grpAdv.Controls.Add(MakeLabel("Token Warn Days:", LblL, 52));
        _warnDaysBox = new NumericUpDown
        {
            Minimum = 1, Maximum = 90,
            Value   = Math.Max(1, cfg.TokenExpiryWarnDays),
            Left    = FldL, Top = 50, Width = 90,
            Font    = new Font("Segoe UI", 9f)
        };
        grpAdv.Controls.Add(_warnDaysBox);
        grpAdv.Controls.Add(new Label
        {
            Text      = "Days before expiry to show the tray warning balloon",
            Left = FldL + 96, Top = 53, Width = 300, Height = 16,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f)
        });

        grpAdv.Controls.Add(MakeLabel("App Project ID:", LblL, 82));
        _appProjectIdBox = new NumericUpDown
        {
            Minimum = 0, Maximum = 999999,
            Value   = Math.Max(0, cfg.AppProjectId),
            Left    = FldL, Top = 80, Width = 120,
            Font    = new Font("Segoe UI", 9f)
        };
        grpAdv.Controls.Add(_appProjectIdBox);

        grpAdv.Controls.Add(MakeLabel("App Repo Path:", LblL, 112));
        _appRepoPathBox = MakeTextBox(cfg.AppRepoPath, FldL, 110, FldW);
        grpAdv.Controls.Add(_appRepoPathBox);

        grpAdv.Controls.Add(new Label
        {
            Text      = "💡 App Project ID and Repo Path control the update checker - update these if the app repo changes",
            Left = LblL, Top = 136, Width = GrpW - 16, Height = 30,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f)
        });

        int advBtnW = (GrpW - (LblL * 2) - 12) / 3;

        var btnClearCache = MakeButton("🗑 Clear Update Cache", LblL, 168, advBtnW);
        btnClearCache.Click += (_, _) => Updater.ClearUpdateCache();
        grpAdv.Controls.Add(btnClearCache);

        var btnLapsSettings = MakeButton("🛡️ LAPS Settings", LblL + advBtnW + 6, 168, advBtnW);
        btnLapsSettings.Click += (_, _) => { using var w = new LapsSettingsWindow(); w.ShowDialog(this); };
        grpAdv.Controls.Add(btnLapsSettings);

        var btnRestoreDefaults = MakeButton("♻️ Restore Defaults", LblL + (advBtnW + 6) * 2, 168, advBtnW);
        btnRestoreDefaults.Click += async (_, _) =>
        {
            if (MessageBox.Show(
                "Reset advanced settings and UI fields to their default values?\n\n" +
                "LAPS config, token warn days, and RunAs profile seeds will be restored from org defaults (pandatools-config).\n\n" +
                "Your RunAs profile passwords and personal settings will not be deleted.",
                "Restore Defaults", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                //######################################
                //Reset UI fields to blank/safe defaults
                //Org-managed fields (LAPS, token warn days, RunAs seeds) are restored
                //from pandatools-config via ForceApplyOrgDefaultsAsync below.
                //######################################
                _urlBox!.Text                 = "";
                _pollBox.Value                = 300;
                _diagCheck.Checked            = false;
                _manualCheck!.Checked         = false;
                _onlySubscribedCheck!.Checked = false;
                _warnDaysBox.Value            = 0;
                _appProjectIdBox.Value        = 0;
                _appRepoPathBox!.Text         = "";
                _urlBrowserCombo!.SelectedItem = "Default";
                _urlBrowserPathBox!.Text      = "";
                _browserCombo!.SelectedItem   = "Default";
                _browserPathBox!.Text         = "";

                Status("⏳ Restoring org defaults...");

                var orgResult = await ConfigLoader.ForceApplyOrgDefaultsAsync();

                //Reload the warn days spinner from config after org defaults are applied
                _warnDaysBox.Value = Math.Max(1, ConfigLoader.AppConfig.TokenExpiryWarnDays);

                Status($"{orgResult} - click 'Save & Apply' to keep UI changes.");
            }
        };
        grpAdv.Controls.Add(btnRestoreDefaults);

        Controls.Add(grpAdv);
        y += 210;

        //######################################
        //Action buttons
        //######################################
        int btnW            = (GrpW - 6) / 4;
        var btnCheckFlavour = MakeButton("🔄 Flavour Updates",  GrpL,              y, btnW);
        var btnCheckApp     = MakeButton("⬆️ App Updates",      GrpL + btnW + 2,   y, btnW);
        var btnSaveApply    = MakeButton("💾 Save && Apply",    GrpL + (btnW+2)*2, y, btnW);
        var btnClose        = MakeButton("✖ Close",             GrpL + (btnW+2)*3, y, btnW);

        btnSaveApply.BackColor = Color.FromArgb(40, 167, 69);
        btnSaveApply.ForeColor = Color.White;
        btnSaveApply.FlatStyle = FlatStyle.Flat;

        btnCheckFlavour.Click += (_, _) => CheckFlavourUpdates();
        btnCheckApp.Click     += (_, _) => _ = Updater.CheckAsync(false);
        btnSaveApply.Click    += (_, _) => SaveSettings();
        btnClose.Click        += (_, _) => Close();

        Controls.AddRange(new Control[] { btnCheckFlavour, btnCheckApp, btnSaveApply, btnClose });
        y += 34;

        //######################################
        //Status
        //######################################
        _statusLabel = new Label
        {
            Text      = "Ready",
            Left = GrpL, Top = y, Width = GrpW, Height = 20,
            ForeColor = Color.DarkBlue,
            Font      = new Font("Segoe UI", 9f)
        };
        Controls.Add(_statusLabel);

        if (_runasListBox.Items.Count > 0) _runasListBox.SelectedIndex = 0;
    }

    private void RefreshFlavourCombo(string selectName)
    {
        if (_flavourCombo == null) return;
        _flavourCombo.Items.Clear();
        foreach (var name in ConfigLoader.GetAvailableFlavours(includeHidden: false))
            _flavourCombo.Items.Add(name);
        _flavourCombo.SelectedItem = selectName;
        if (_flavourCombo.SelectedIndex < 0 && _flavourCombo.Items.Count > 0)
            _flavourCombo.SelectedIndex = 0;
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
            cfg.UrlServer            = _urlBox?.Text.Trim()                 ?? cfg.UrlServer;
            cfg.Flavour              = (string?)_flavourCombo?.SelectedItem ?? cfg.Flavour;
            cfg.Diagnostics          = _diagCheck?.Checked                  ?? cfg.Diagnostics;
            cfg.ManualMode           = _manualCheck?.Checked                ?? cfg.ManualMode;
            cfg.ShowOnlySubscribedFlavour = _onlySubscribedCheck?.Checked   ?? cfg.ShowOnlySubscribedFlavour;
            cfg.FlavourPollSeconds   = (int?)_pollBox?.Value                ?? cfg.FlavourPollSeconds;
            cfg.FlavourProjectId     = (int?)_projectIdBox?.Value           ?? cfg.FlavourProjectId;
            cfg.TokenExpiryWarnDays  = (int?)_warnDaysBox?.Value            ?? cfg.TokenExpiryWarnDays;
            cfg.AppProjectId         = (int?)_appProjectIdBox?.Value        ?? cfg.AppProjectId;
            cfg.AppRepoPath          = _appRepoPathBox?.Text.Trim()         ?? cfg.AppRepoPath;
            cfg.UrlBrowserName       = (_urlBrowserCombo?.SelectedItem?.ToString() ?? "default").ToLowerInvariant();
            cfg.UrlBrowserPath       = _urlBrowserPathBox?.Text.Trim()      ?? cfg.UrlBrowserPath;
            cfg.BrowserName          = (_browserCombo?.SelectedItem?.ToString() ?? "default").ToLowerInvariant();
            cfg.BrowserPath          = _browserPathBox?.Text.Trim()         ?? cfg.BrowserPath;
            cfg.RunAsProfiles        = _runasProfiles;

            ConfigLoader.Save(cfg);
            TokenManager.Reset();
            Status("✅ Settings saved and applied");
        }
        catch (Exception ex) { Status($"❌ Save failed: {ex.Message}"); }
    }

    private void CheckFlavourUpdates()
    {
        Status("⏳ Checking GitLab for flavour updates...");
        _ = Task.Run(async () =>
        {
            await ConfigLoader.CheckFlavourUpdateAsync();
            Invoke(() => Status("✅ Flavour check complete"));
        });
    }

    private void Status(string msg)
    {
        if (_statusLabel == null) return;
        _statusLabel.Text      = msg;
        _statusLabel.ForeColor = msg.StartsWith("❌") ? Color.DarkRed : Color.DarkBlue;
    }

    private static GroupBox MakeGroup(string text, int top, int height) =>
        new() { Text = text, Left = GrpL, Top = top, Width = GrpW, Height = height, Font = new Font("Segoe UI", 9f) };

    private static Label MakeLabel(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y + 3, Width = LblW, Height = 20, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f) };

    private static Label MakePanelLabel(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y + 3, Width = 92, Height = 20, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f) };

    private static TextBox MakeTextBox(string text, int x, int y, int width, bool password = false) =>
        new() { Text = text, Left = x, Top = y, Width = width, UseSystemPasswordChar = password, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };

    private static TextBox MakePanelBox(int x, int y, int width, bool password = false) =>
        new() { Left = x, Top = y, Width = width, UseSystemPasswordChar = password, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };

    private static Button MakeButton(string text, int x, int y, int width) =>
        new() { Text = text, Left = x, Top = y, Width = width, Height = 27, Font = new Font("Segoe UI", 9f) };
}
