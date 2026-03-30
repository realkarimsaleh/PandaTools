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
    //Singleton
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

    //######################################
    //Field refs
    //######################################
    //Config Server fields
    private TextBox?       _urlBox;
    private TextBox?       _tokenPlainBox;
    private TextBox?       _cfgRepoOwnerBox;
    private TextBox?       _cfgRepoNameBox;
    //App Updates fields
    private TextBox?       _appUrlBox;
    private TextBox?       _appRepoOwnerBox;
    private TextBox?       _appRepoNameBox;
    private TextBox?       _appRepoPathBox;
    private TextBox?       _appTokenPlainBox;
    //Browser / RunAs fields
    private TextBox?       _browserPathBox;
    private TextBox?       _urlBrowserPathBox;
    private TextBox?       _runasUserBox;
    private TextBox?       _runasPassBox;
    private TextBox?       _runasNameBox;
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
    private CheckBox?      _chkAppUrlSame;
    private Label?         _statusLabel;
    //Platform / visibility radios
    private RadioButton?   _radCfgGitHub,  _radCfgGitLab;
    private RadioButton?   _radCfgPublic,  _radCfgPrivate;
    private RadioButton?   _radAppGitHub,  _radAppGitLab;
    private RadioButton?   _radAppPublic,  _radAppPrivate;
    // Conditional panels
    private Panel?         _pnlCfgOwnerRepo, _pnlCfgProjectId;
    private Panel?         _pnlCfgToken;
    private Button?        _btnCfgExpiry;
    private Panel?         _pnlAppOwnerRepo, _pnlAppProjectId;
    private Panel?         _pnlAppToken,     _pnlAppReuseHint;

    private List<RunAsProfile> _runasProfiles       = new();
    private int                _selectedProfileIndex = -1;

    //######################################
    //Layout  sidebar + content
    //######################################
    private const int SideW   = 158;   // sidebar width
    private const int FormW   = 760;
    private const int FormH   = 640;
    private const int HeadH   = 52;
    private const int FootH   = 62;
    private const int ContentW = FormW - SideW;
    private const int ContentH = FormH - HeadH - FootH;

    //Field layout within content panels
    private const int Pad  = 16;
    private const int LblW = 118;
    private const int FldX = 136;
    private const int FldW = ContentW - FldX - Pad - 2;

    private Panel? _activeSection;

    private SettingsWindow()
    {
        Text            = "PandaTools - Settings";
        ClientSize      = new Size(FormW, FormH);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        TopMost         = true;
        Icon            = AppIcon.Get();
        BackColor       = Color.FromArgb(245, 245, 245);

        BuildLayout();
    }

    private void BuildLayout()
    {
        var cfg = ConfigLoader.AppConfig;
        _runasProfiles = cfg.RunAsProfiles
            .Select(p => new RunAsProfile { Name = p.Name, Username = p.Username, PasswordEncrypted = p.PasswordEncrypted, LegacyPassword = p.LegacyPassword })
            .ToList();

        var appVersion     = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var flavourVersion = ConfigLoader.FlavourConfig.Version;

        //######################################
        //Header
        //######################################
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = HeadH,
            BackColor = Color.FromArgb(28, 28, 28)
        };
        try
        {
            var exeIcon = Icon.ExtractAssociatedIcon(System.Environment.ProcessPath!);
            if (exeIcon != null)
                header.Controls.Add(new PictureBox
                {
                    Image     = exeIcon.ToBitmap(),
                    SizeMode  = PictureBoxSizeMode.StretchImage,
                    Bounds    = new Rectangle(10, 10, 30, 30),
                    BackColor = Color.Transparent
                });
        }
        catch { }

        header.Controls.Add(new Label
        {
            Text      = $"PandaTools v{appVersion}",
            Left = 48, Top = 16, Width = 210, Height = 20,
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold)
        });
        header.Controls.Add(new Label
        {
            Text      = $"Flavour: {cfg.Flavour} • v{flavourVersion}",
            Left = 270, Top = 16, Width = FormW - 280, Height = 20,
            ForeColor = Color.FromArgb(170, 170, 170), BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleRight
        });
        Controls.Add(header);

        //######################################
        //Footer
        //######################################
        var footer = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = FootH,
            BackColor = Color.White
        };
        footer.Controls.Add(new Panel
        {
            Dock = DockStyle.Top, Height = 1,
            BackColor = Color.FromArgb(220, 220, 220)
        });

        _statusLabel = new Label
        {
            Text      = "Ready",
            Left = 10, Top = 6, Width = FormW - 20, Height = 16,
            ForeColor = Color.DarkBlue,
            Font      = new Font("Segoe UI", 8f)
        };
        footer.Controls.Add(_statusLabel);

        const int bw  = 118;
        const int bgap = 6;
        var btnFlavourUpdates = MakeBtn("🔄 Flavours",      FormW - (bw + bgap) * 4 + bgap, 24, bw);
        var btnAppUpdates     = MakeBtn("⬆️ Updates",       FormW - (bw + bgap) * 3 + bgap, 24, bw);
        var btnSaveApply      = MakeBtn("💾 Save && Apply", FormW - (bw + bgap) * 2 + bgap, 24, bw);
        var btnClose          = MakeBtn("✖ Close",          FormW - bw - bgap,               24, bw);

        btnSaveApply.BackColor = Color.FromArgb(40, 167, 69);
        btnSaveApply.ForeColor = Color.White;
        btnSaveApply.FlatStyle = FlatStyle.Flat;
        btnSaveApply.FlatAppearance.BorderSize = 0;

        btnFlavourUpdates.Click += (_, _) => CheckFlavourUpdates();
        btnAppUpdates.Click     += (_, _) => _ = Updater.CheckAsync(false);
        btnSaveApply.Click      += (_, _) => SaveSettings();
        btnClose.Click          += (_, _) => Close();

        footer.Controls.AddRange(new Control[] { btnFlavourUpdates, btnAppUpdates, btnSaveApply, btnClose });
        Controls.Add(footer);

        //######################################
        //Sidebar
        //######################################
        var sidebar = new Panel
        {
            Left = 0, Top = HeadH,
            Width = SideW, Height = ContentH,
            BackColor = Color.FromArgb(36, 36, 36)
        };
        Controls.Add(sidebar);

        //######################################
        //Content area
        //######################################
        var content = new Panel
        {
            Left = SideW, Top = HeadH,
            Width = ContentW, Height = ContentH,
            BackColor = Color.FromArgb(245, 245, 245)
        };
        Controls.Add(content);

        //######################################
        //Build section panels
        //######################################
        var secConnection = BuildSectionConnection(cfg);
        var secFlavour    = BuildSectionFlavour(cfg);
        var secBrowser    = BuildSectionBrowser(cfg);
        var secRunAs      = BuildSectionRunAs(cfg);
        var secAdvanced   = BuildSectionAdvanced(cfg);

        foreach (var sec in new[] { secConnection, secFlavour, secBrowser, secRunAs, secAdvanced })
        {
            sec.Dock    = DockStyle.Fill;
            sec.Visible = false;
            content.Controls.Add(sec);
        }

        //######################################
        //Sidebar nav items
        //######################################
        var navItems = new (string label, Panel section)[]
        {
            ("🔗  Connection",    secConnection),
            ("🎨  Flavour",       secFlavour),
            ("🌐  Browser",       secBrowser),
            ("👤  RunAs Profiles", secRunAs),
            ("⚙️  Advanced",      secAdvanced),
        };

        int navY = 12;
        Label? activeLabel = null;

        foreach (var (label, section) in navItems)
        {
            var nav = new Label
            {
                Text      = label,
                Left      = 0, Top = navY,
                Width     = SideW, Height = 34,
                ForeColor = Color.FromArgb(170, 170, 170),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0),
                Cursor    = Cursors.Hand,
                Tag       = section
            };

            var capturedNav     = nav;
            var capturedSection = section;

            nav.Click += (_, _) =>
            {
                //Deactivate previous
                if (activeLabel != null)
                {
                    activeLabel.ForeColor = Color.FromArgb(170, 170, 170);
                    activeLabel.BackColor = Color.Transparent;
                    activeLabel.Font      = new Font("Segoe UI", 9f);
                }
                //Activate this
                capturedNav.ForeColor = Color.White;
                capturedNav.BackColor = Color.FromArgb(52, 52, 52);
                capturedNav.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
                activeLabel           = capturedNav;

                //Swap panel
                foreach (Control c in content.Controls)
                    c.Visible = c == capturedSection;
                _activeSection = capturedSection;
            };

            sidebar.Controls.Add(nav);
            navY += 36;
        }

        //Select first item  trigger the Connection section directly
        foreach (Control c in content.Controls) c.Visible = c == secConnection;
        var firstNav = sidebar.Controls.OfType<Label>().First();
        firstNav.ForeColor = Color.White;
        firstNav.BackColor = Color.FromArgb(52, 52, 52);
        firstNav.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        if (_runasListBox?.Items.Count > 0) _runasListBox.SelectedIndex = 0;
    }

    //######################################
    //Section: Connection
    //Fixed-position layout so toggling platform radios never causes gaps.
    //All conditional panels sit at the same Top - only one is visible at a time.
    //######################################
    private Panel BuildSectionConnection(AppConfig cfg)
    {
        var p = MakeSection();
        const int tkBtnW = 110; const int tkGap = 4;
        const int RowH   = 32;  // standard row height
        const int PnlH   = 68;  // fixed height for all swappable panels (owner+repo or projectid)
        int tkBoxW       = FldW - (tkBtnW * 2) - (tkGap * 2);
        int appTkBoxW    = FldW - (tkBtnW + tkGap);

        //######################################
        //Config Server - header with inline subtitle
        //######################################
        int y = Pad;

        //Title + subtitle on same line
        p.Controls.Add(new Label
        {
            Text = "⚙️  Config Server", Left = Pad, Top = y,
            Width = 180, Height = 24,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 28, 28)
        });
        p.Controls.Add(new Label
        {
            Text      = "Flavours · Defaults · Scripts",
            Left      = Pad, Top = y + 4, Width = ContentW - Pad * 2, Height = 16,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8f),
            TextAlign = ContentAlignment.MiddleRight
        });
        y += 26;
        p.Controls.Add(new Panel { Left = Pad, Top = y, Width = ContentW - Pad * 2, Height = 1, BackColor = Color.FromArgb(220, 220, 220) });
        y += 10;

        //Platform - own panel so radios don't bleed into other groups
        p.Controls.Add(MakeLbl("Platform:", Pad, y));
        var pnlCfgPlat = new Panel { Left = FldX, Top = y, Width = 200, Height = 24, BackColor = Color.Transparent };
        _radCfgGitHub = MakeRadio("GitHub", 0,  0, cfg.CfgIsGitHub);
        _radCfgGitLab = MakeRadio("GitLab", 80, 0, !cfg.CfgIsGitHub);
        _radCfgGitHub.CheckedChanged += (_, _) => { if (_radCfgGitHub.Checked) RefreshCfgVisibility(); };
        _radCfgGitLab.CheckedChanged += (_, _) => { if (_radCfgGitLab.Checked) RefreshCfgVisibility(); };
        pnlCfgPlat.Controls.AddRange(new Control[] { _radCfgGitHub, _radCfgGitLab });
        p.Controls.Add(pnlCfgPlat);
        y += RowH;

        //Server URL
        p.Controls.Add(MakeLbl("Server URL:", Pad, y));
        _urlBox = MakeTxt(cfg.UrlServer, FldX, y, FldW);
        _urlBox.PlaceholderText = cfg.CfgIsGitHub ? "https://github.com" : "https://gitlab.example.com";
        p.Controls.Add(_urlBox);
        y += RowH;

        //Swappable row: Repo Owner+Name (GitHub) OR Project ID (GitLab) - SAME Top, SAME height
        _pnlCfgOwnerRepo = new Panel { Left = 0, Top = y, Width = ContentW, Height = PnlH, Visible = cfg.CfgIsGitHub };
        _pnlCfgOwnerRepo.Controls.Add(MakeLbl("Repo Owner:", Pad, 0));
        _cfgRepoOwnerBox = MakeTxt(cfg.CfgRepoOwner, FldX, 0, FldW);
        _cfgRepoOwnerBox.PlaceholderText = "e.g. realKarimSaleh";
        _pnlCfgOwnerRepo.Controls.Add(_cfgRepoOwnerBox);
        _pnlCfgOwnerRepo.Controls.Add(MakeLbl("Repo Name:", Pad, RowH));
        _cfgRepoNameBox = MakeTxt(cfg.CfgRepoName, FldX, RowH, FldW);
        _cfgRepoNameBox.PlaceholderText = "e.g. pandatools-config";
        _pnlCfgOwnerRepo.Controls.Add(_cfgRepoNameBox);
        p.Controls.Add(_pnlCfgOwnerRepo);

        _pnlCfgProjectId = new Panel { Left = 0, Top = y, Width = ContentW, Height = PnlH, Visible = !cfg.CfgIsGitHub };
        _pnlCfgProjectId.Controls.Add(MakeLbl("Project ID:", Pad, 0));
        _projectIdBox = new NumericUpDown { Minimum = 0, Maximum = 999999999, Value = Math.Max(0, cfg.FlavourProjectId), Left = FldX, Top = 0, Width = 130, Font = new Font("Segoe UI", 9f) };
        _pnlCfgProjectId.Controls.Add(_projectIdBox);
        _pnlCfgProjectId.Controls.Add(new Label { Text = "Used for flavour and defaults polling", Left = FldX + 136, Top = 3, Width = 260, Height = 16, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f) });
        p.Controls.Add(_pnlCfgProjectId);
        //always advance by fixed PnlH
        y += PnlH;  

        //Visibility - own panel
        p.Controls.Add(MakeLbl("Visibility:", Pad, y));
        var pnlCfgVis = new Panel { Left = FldX, Top = y, Width = 320, Height = 24, BackColor = Color.Transparent };
        _radCfgPublic  = MakeRadio("Public",  0,  0, cfg.CfgPublic);
        _radCfgPrivate = MakeRadio("Private", 80, 0, !cfg.CfgPublic);
        var cfgVisHint = new Label { Text = "no token needed", Left = 168, Top = 2, Width = 140, Height = 20, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8f), TextAlign = ContentAlignment.MiddleLeft, Visible = cfg.CfgPublic };
        _radCfgPublic.CheckedChanged  += (_, _) => { RefreshCfgTokenVisibility(); cfgVisHint.Visible = _radCfgPublic.Checked; };
        _radCfgPrivate.CheckedChanged += (_, _) => RefreshCfgTokenVisibility();
        pnlCfgVis.Controls.AddRange(new Control[] { _radCfgPublic, _radCfgPrivate, cfgVisHint });
        p.Controls.Add(pnlCfgVis);
        y += RowH;

        //Token (always present, greyed when public)
        _pnlCfgToken = new Panel { Left = 0, Top = y, Width = ContentW, Height = 44 };
        _pnlCfgToken.Controls.Add(MakeLbl("Token:", Pad, 0));
        _tokenPlainBox = MakeTxt("", FldX, 0, tkBoxW, password: true);
        _tokenPlainBox.PlaceholderText = cfg.CfgIsGitHub ? "ghp_xxxxxxxxxxxx" : "glpat-xxxxxxxxxxxx";
        _pnlCfgToken.Controls.Add(_tokenPlainBox);
        var btnToken = MakeBtn("🔑 Update Token", FldX + tkBoxW + tkGap, -1, tkBtnW);
        btnToken.BackColor = Color.FromArgb(0, 123, 255); btnToken.ForeColor = Color.White;
        btnToken.FlatStyle = FlatStyle.Flat; btnToken.FlatAppearance.BorderSize = 0;
        btnToken.Click += (_, _) =>
        {
            var plain = _tokenPlainBox?.Text.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(plain)) { Status("❌ Token field is empty"); return; }
            TokenManager.SaveToken(plain);
            if (_tokenPlainBox != null) _tokenPlainBox.Text = "";
            Status("✅ Config server token saved");
        };
        _pnlCfgToken.Controls.Add(btnToken);
        _btnCfgExpiry = MakeBtn("🔍 Check Expiry", FldX + tkBoxW + tkGap + tkBtnW + tkGap, -1, tkBtnW);
        _btnCfgExpiry.Visible = !cfg.CfgIsGitHub;
        _btnCfgExpiry.Click += async (_, _) =>
        {
            Status("⏳ Checking token expiry...");
            Status(await TokenExpiryChecker.GetExpiryInfoAsync());
        };
        _pnlCfgToken.Controls.Add(_btnCfgExpiry);
        _pnlCfgToken.Controls.Add(new Label { Text = "💡 Encrypted with DPAPI - plain-text is never stored", Left = FldX, Top = 26, Width = ContentW - FldX - Pad, Height = 16, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f) });
        p.Controls.Add(_pnlCfgToken);
        y += 48;

        AddDivider(p, ref y);

        //######################################
        //App Updates - header with inline subtitle
        //######################################
        p.Controls.Add(new Label
        {
            Text = "⬆️  App Updates", Left = Pad, Top = y,
            Width = 180, Height = 24,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 28, 28)
        });
        p.Controls.Add(new Label
        {
            Text      = "Installer releases",
            Left      = Pad, Top = y + 4, Width = ContentW - Pad * 2, Height = 16,
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8f),
            TextAlign = ContentAlignment.MiddleRight
        });
        y += 26;
        p.Controls.Add(new Panel { Left = Pad, Top = y, Width = ContentW - Pad * 2, Height = 1, BackColor = Color.FromArgb(220, 220, 220) });
        y += 10;

        //Platform - own panel
        p.Controls.Add(MakeLbl("Platform:", Pad, y));
        var pnlAppPlat = new Panel { Left = FldX, Top = y, Width = 200, Height = 24, BackColor = Color.Transparent };
        _radAppGitHub = MakeRadio("GitHub", 0,  0, cfg.AppIsGitHub);
        _radAppGitLab = MakeRadio("GitLab", 80, 0, !cfg.AppIsGitHub);
        _radAppGitHub.CheckedChanged += (_, _) => { if (_radAppGitHub.Checked) RefreshAppVisibility(); };
        _radAppGitLab.CheckedChanged += (_, _) => { if (_radAppGitLab.Checked) RefreshAppVisibility(); };
        pnlAppPlat.Controls.AddRange(new Control[] { _radAppGitHub, _radAppGitLab });
        p.Controls.Add(pnlAppPlat);
        y += RowH;

        //Server URL + Same as Config
        p.Controls.Add(MakeLbl("Server URL:", Pad, y));
        _appUrlBox = MakeTxt(cfg.AppUrlServer, FldX, y, FldW - 180);
        _appUrlBox.ReadOnly  = cfg.AppSameAsConfig;
        _appUrlBox.BackColor = cfg.AppSameAsConfig ? Color.FromArgb(240, 240, 240) : Color.White;
        p.Controls.Add(_appUrlBox);
        bool sameEnabled = cfg.AppPlatform.Equals(cfg.CfgPlatform, StringComparison.OrdinalIgnoreCase);
        _chkAppUrlSame = new CheckBox
        {
            Text      = "Same as Config Server",
            Checked   = cfg.AppSameAsConfig,
            Enabled   = sameEnabled,
            Left      = FldX + (FldW - 178), Top = y + 2,
            Width     = 178, Height = 20,
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = sameEnabled ? Color.FromArgb(30, 30, 30) : Color.DimGray
        };
        _chkAppUrlSame.CheckedChanged += (_, _) =>
        {
            var isSame = _chkAppUrlSame.Checked;
            _appUrlBox!.ReadOnly  = isSame;
            _appUrlBox.BackColor  = isSame ? Color.FromArgb(240, 240, 240) : Color.White;
            if (isSame) _appUrlBox.Text = _urlBox?.Text.Trim() ?? "";
            RefreshAppTokenVisibility();
        };
        p.Controls.Add(_chkAppUrlSame);
        y += RowH;

        //Swappable: Repo Owner+Name (GitHub) OR Project ID + Repo Path (GitLab) - SAME Top
        _pnlAppOwnerRepo = new Panel { Left = 0, Top = y, Width = ContentW, Height = PnlH, Visible = cfg.AppIsGitHub };
        _pnlAppOwnerRepo.Controls.Add(MakeLbl("Repo Owner:", Pad, 0));
        _appRepoOwnerBox = MakeTxt(cfg.AppRepoOwner, FldX, 0, FldW);
        _appRepoOwnerBox.PlaceholderText = "e.g. realKarimSaleh";
        _pnlAppOwnerRepo.Controls.Add(_appRepoOwnerBox);
        _pnlAppOwnerRepo.Controls.Add(MakeLbl("Repo Name:", Pad, RowH));
        _appRepoNameBox = MakeTxt(cfg.AppRepoName, FldX, RowH, FldW);
        _appRepoNameBox.PlaceholderText = "e.g. PandaTools";
        _pnlAppOwnerRepo.Controls.Add(_appRepoNameBox);
        p.Controls.Add(_pnlAppOwnerRepo);

        _pnlAppProjectId = new Panel { Left = 0, Top = y, Width = ContentW, Height = PnlH, Visible = !cfg.AppIsGitHub };
        _pnlAppProjectId.Controls.Add(MakeLbl("Project ID:", Pad, 0));
        _appProjectIdBox = new NumericUpDown { Minimum = 0, Maximum = 999999999, Value = Math.Max(0, cfg.AppProjectId), Left = FldX, Top = 0, Width = 130, Font = new Font("Segoe UI", 9f) };
        _pnlAppProjectId.Controls.Add(_appProjectIdBox);
        _pnlAppProjectId.Controls.Add(MakeLbl("Repo Path:", Pad, RowH));
        _appRepoPathBox = MakeTxt(cfg.AppRepoPath, FldX, RowH, FldW);
        _pnlAppProjectId.Controls.Add(_appRepoPathBox);
        p.Controls.Add(_pnlAppProjectId);
        //Always advance by fixed PnlH
        y += PnlH;

        //Visibility - own panel
        p.Controls.Add(MakeLbl("Visibility:", Pad, y));
        var pnlAppVis = new Panel { Left = FldX, Top = y, Width = 320, Height = 24, BackColor = Color.Transparent };
        _radAppPublic  = MakeRadio("Public",  0,  0, cfg.AppPublic);
        _radAppPrivate = MakeRadio("Private", 80, 0, !cfg.AppPublic);
        var appVisHint = new Label { Text = "no token needed", Left = 168, Top = 2, Width = 140, Height = 20, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8f), TextAlign = ContentAlignment.MiddleLeft, Visible = cfg.AppPublic };
        _radAppPublic.CheckedChanged  += (_, _) => { RefreshAppTokenVisibility(); appVisHint.Visible = _radAppPublic.Checked; };
        _radAppPrivate.CheckedChanged += (_, _) => RefreshAppTokenVisibility();
        pnlAppVis.Controls.AddRange(new Control[] { _radAppPublic, _radAppPrivate, appVisHint });
        p.Controls.Add(pnlAppVis);
        y += RowH;

        //Token (always present, greyed when public or same-server)
        _pnlAppToken = new Panel { Left = 0, Top = y, Width = ContentW, Height = 44 };
        _pnlAppToken.Controls.Add(MakeLbl("App Token:", Pad, 0));
        _appTokenPlainBox = MakeTxt("", FldX, 0, appTkBoxW, password: true);
        _appTokenPlainBox.PlaceholderText = cfg.AppIsGitHub ? "ghp_xxxxxxxxxxxx" : "glpat-xxxxxxxxxxxx";
        _pnlAppToken.Controls.Add(_appTokenPlainBox);
        var btnAppToken = MakeBtn("🔑 Update Token", FldX + appTkBoxW + tkGap, -1, tkBtnW);
        btnAppToken.BackColor = Color.FromArgb(0, 123, 255); btnAppToken.ForeColor = Color.White;
        btnAppToken.FlatStyle = FlatStyle.Flat; btnAppToken.FlatAppearance.BorderSize = 0;
        btnAppToken.Click += (_, _) =>
        {
            var plain = _appTokenPlainBox?.Text.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(plain)) { Status("❌ Token field is empty"); return; }
            TokenManager.SaveAppToken(plain);
            if (_appTokenPlainBox != null) _appTokenPlainBox.Text = "";
            Status("✅ App token saved");
        };

        _pnlAppToken.Controls.Add(btnAppToken);
        _pnlAppToken.Controls.Add(new Label { Text = "💡 Encrypted with DPAPI - plain-text is never stored", Left = FldX, Top = 26, Width = ContentW - FldX - Pad, Height = 16, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f) });
        p.Controls.Add(_pnlAppToken);

        _pnlAppReuseHint = new Panel { Left = 0, Top = y, Width = ContentW, Height = 20 };
        _pnlAppReuseHint.Controls.Add(new Label { Text = "ℹ️ Using config server token - no second token needed", Left = FldX, Top = 0, Width = ContentW - FldX - Pad, Height = 20, ForeColor = Color.FromArgb(0, 100, 150), Font = new Font("Segoe UI", 8.5f) });
        p.Controls.Add(_pnlAppReuseHint);

        RefreshCfgTokenVisibility();
        RefreshAppTokenVisibility();

        return p;
    }

    private void RefreshCfgVisibility()
    {
        var isGitHub = _radCfgGitHub?.Checked ?? false;
        if (_pnlCfgOwnerRepo != null) _pnlCfgOwnerRepo.Visible = isGitHub;
        if (_pnlCfgProjectId != null) _pnlCfgProjectId.Visible = !isGitHub;
        if (_btnCfgExpiry    != null) _btnCfgExpiry.Visible     = !isGitHub;
        if (_urlBox          != null) _urlBox.PlaceholderText   = isGitHub ? "https://github.com" : "https://gitlab.example.com";
        if (_tokenPlainBox   != null) _tokenPlainBox.PlaceholderText = isGitHub ? "ghp_xxxxxxxxxxxx" : "glpat-xxxxxxxxxxxx";
        RefreshAppSameAvailability();
    }

    private void RefreshCfgTokenVisibility()
    {
        var isPublic = _radCfgPublic?.Checked ?? false;
        if (_pnlCfgToken != null)
        {
            foreach (Control c in _pnlCfgToken.Controls)
            {
                if (c is TextBox tb)  { tb.ReadOnly = isPublic; tb.BackColor = isPublic ? Color.FromArgb(240, 240, 240) : Color.White; }
                if (c is Button btn && btn.Text.Contains("Update")) btn.Enabled = !isPublic;
                if (c is Button exp && exp.Text.Contains("Expiry")) exp.Enabled = !isPublic;
            }
        }
        RefreshAppTokenVisibility();
    }

    private void RefreshAppVisibility()
    {
        var isGitHub = _radAppGitHub?.Checked ?? false;
        if (_pnlAppOwnerRepo  != null) _pnlAppOwnerRepo.Visible  = isGitHub;
        if (_pnlAppProjectId  != null) _pnlAppProjectId.Visible  = !isGitHub;
        if (_appTokenPlainBox != null) _appTokenPlainBox.PlaceholderText = isGitHub ? "ghp_xxxxxxxxxxxx" : "glpat-xxxxxxxxxxxx";
        RefreshAppSameAvailability();
        RefreshAppTokenVisibility();
    }

    private void RefreshAppSameAvailability()
    {
        if (_chkAppUrlSame == null) return;
        var cfgIsGitHub = _radCfgGitHub?.Checked ?? false;
        var appIsGitHub = _radAppGitHub?.Checked ?? false;
        var sameEnabled = cfgIsGitHub == appIsGitHub;
        _chkAppUrlSame.Enabled   = sameEnabled;
        _chkAppUrlSame.ForeColor = sameEnabled ? Color.FromArgb(30, 30, 30) : Color.DimGray;
        if (!sameEnabled && _chkAppUrlSame.Checked)
        {
            _chkAppUrlSame.Checked = false;
            if (_appUrlBox != null) { _appUrlBox.ReadOnly = false; _appUrlBox.BackColor = Color.White; }
        }
    }

    private void RefreshAppTokenVisibility()
    {
        if (_pnlAppToken == null || _pnlAppReuseHint == null) return;
        var isPublic    = _radAppPublic?.Checked  ?? true;
        var isSame      = _chkAppUrlSame?.Checked ?? false;
        var cfgHasToken = !(_radCfgPublic?.Checked ?? false);

        if (isPublic)
        {
            //Grey out token fields rather than hiding them
            SetPanelGreyed(_pnlAppToken, true);
            _pnlAppReuseHint.Visible = false;
        }
        else if (isSame && cfgHasToken)
        {
            SetPanelGreyed(_pnlAppToken, true);
            _pnlAppReuseHint.Visible = true;
        }
        else
        {
            SetPanelGreyed(_pnlAppToken, false);
            _pnlAppReuseHint.Visible = false;
        }
    }

    private static void SetPanelGreyed(Panel pnl, bool greyed)
    {
        foreach (Control c in pnl.Controls)
        {
            if (c is TextBox tb)  { tb.ReadOnly = greyed; tb.BackColor = greyed ? Color.FromArgb(240, 240, 240) : Color.White; }
            if (c is Button btn)  btn.Enabled = !greyed;
        }
    }

    //######################################
    //Section: Flavour
    //######################################
    private Panel BuildSectionFlavour(AppConfig cfg)
    {
        var p = MakeSection();
        int y = Pad;

        AddSectionHeader(p, "🎨  Flavour", ref y);

        //Active flavour row
        p.Controls.Add(MakeLbl("Active Flavour:", Pad, y));
        _flavourCombo = new ComboBox
        {
            DropDownStyle    = ComboBoxStyle.DropDownList,
            Left = FldX, Top = y, Width = 200,
            Font             = new Font("Segoe UI", 9f),
            MaxDropDownItems = 15, DropDownWidth = 300
        };
        RefreshFlavourCombo(cfg.Flavour);
        p.Controls.Add(_flavourCombo);

        int bxStart  = FldX + 206;
        int bxRemain = ContentW - bxStart - Pad;
        int bw3      = bxRemain / 3;
        var btnAdd    = MakeBtn("+ Add",     bxStart,          y, bw3 - 2);
        var btnRemove = MakeBtn("− Remove",  bxStart + bw3,    y, bw3 - 2);
        var btnFolder = MakeBtn("📂 Folder", bxStart + bw3*2,  y, bw3 - 2);

        btnAdd.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "JSON files (*.json)|*.json", Title = "Import flavour .json" };
            if (ofd.ShowDialog() != DialogResult.OK) return;
            try
            {
                var name = Path.GetFileNameWithoutExtension(ofd.FileName);
                File.Copy(ofd.FileName, Path.Combine(ConfigLoader.FlavourDir, $"{name}.json"), overwrite: true);
                RefreshFlavourCombo(name); Status($"✅ Added: {name}");
            }
            catch (Exception ex) { Status($"❌ {ex.Message}"); }
        };
        btnRemove.Click += (_, _) =>
        {
            var sel = _flavourCombo?.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(sel)) { Status("❌ No flavour selected"); return; }
            if (sel.Equals(ConfigLoader.AppConfig.Flavour, StringComparison.OrdinalIgnoreCase))
            { Status("❌ Cannot remove the active flavour"); return; }
            if (MessageBox.Show($"Delete flavour '{sel}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                var path = Path.Combine(ConfigLoader.FlavourDir, $"{sel}.json");
                if (File.Exists(path)) File.Delete(path);
                RefreshFlavourCombo(ConfigLoader.AppConfig.Flavour); Status($"✅ Removed: {sel}");
            }
            catch (Exception ex) { Status($"❌ {ex.Message}"); }
        };
        btnFolder.Click += (_, _) =>
        {
            Directory.CreateDirectory(ConfigLoader.FlavourDir);
            System.Diagnostics.Process.Start("explorer.exe", ConfigLoader.FlavourDir);
        };
        p.Controls.AddRange(new Control[] { btnAdd, btnRemove, btnFolder });
        y += 32;

        AddDivider(p, ref y);

        _manualCheck = new CheckBox
        {
            Text = "Manual mode - Disable auto-updates from GitLab",
            Checked = cfg.ManualMode,
            Left = Pad, Top = y, Width = ContentW - Pad * 2, Height = 22,
            Font = new Font("Segoe UI", 9f)
        };
        p.Controls.Add(_manualCheck);
        y += 28;

        _onlySubscribedCheck = new CheckBox
        {
            Text = "Hide Personal Menu (only show GitLab subscribed menu)",
            Checked = cfg.ShowOnlySubscribedFlavour,
            Left = Pad, Top = y, Width = ContentW - Pad * 2, Height = 22,
            Font = new Font("Segoe UI", 9f)
        };
        p.Controls.Add(_onlySubscribedCheck);
        y += 28;

        p.Controls.Add(MakeHint("💡 Add imports a .json from disk  •  Remove deletes the file  •  Active flavour cannot be removed", Pad, y));

        return p;
    }

    //######################################
    //Section: Browser
    //######################################
    private Panel BuildSectionBrowser(AppConfig cfg)
    {
        var p = MakeSection();
        int y = Pad;

        AddSectionHeader(p, "🌐  Browser", ref y);

        //Default browser
        p.Controls.Add(MakeLbl("Default:", Pad, y));
        _urlBrowserCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = FldX, Top = y, Width = 160, Font = new Font("Segoe UI", 9f)
        };
        _urlBrowserCombo.Items.AddRange(new object[] { "Default", "Chrome", "Edge", "Firefox", "Brave", "Custom" });
        foreach (var b in _urlBrowserCombo.Items)
            if (b.ToString()!.Equals(cfg.UrlBrowserName, StringComparison.OrdinalIgnoreCase))
            { _urlBrowserCombo.SelectedItem = b; break; }
        if (_urlBrowserCombo.SelectedIndex < 0) _urlBrowserCombo.SelectedIndex = 0;
        p.Controls.Add(_urlBrowserCombo);

        var btnBrowseUrl = MakeBtn("📁 Browse...", FldX + 166, y, 100);
        btnBrowseUrl.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe" };
            if (ofd.ShowDialog() == DialogResult.OK) { _urlBrowserPathBox!.Text = ofd.FileName; _urlBrowserCombo.SelectedItem = "Custom"; }
        };
        p.Controls.Add(btnBrowseUrl);
        y += 32;

        p.Controls.Add(MakeLbl("Custom Path:", Pad, y));
        _urlBrowserPathBox = MakeTxt(cfg.UrlBrowserPath, FldX, y, FldW);
        p.Controls.Add(_urlBrowserPathBox);
        y += 32;

        AddDivider(p, ref y);

        //Incognito browser
        p.Controls.Add(MakeLbl("Incognito:", Pad, y));
        _browserCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = FldX, Top = y, Width = 160, Font = new Font("Segoe UI", 9f)
        };
        _browserCombo.Items.AddRange(new object[] { "Default", "Chrome", "Edge", "Firefox", "Brave", "Custom" });
        foreach (var b in _browserCombo.Items)
            if (b.ToString()!.Equals(cfg.BrowserName, StringComparison.OrdinalIgnoreCase))
            { _browserCombo.SelectedItem = b; break; }
        if (_browserCombo.SelectedIndex < 0) _browserCombo.SelectedIndex = 0;
        p.Controls.Add(_browserCombo);

        var btnBrowseInc = MakeBtn("📁 Browse...", FldX + 166, y, 100);
        btnBrowseInc.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe" };
            if (ofd.ShowDialog() == DialogResult.OK) { _browserPathBox!.Text = ofd.FileName; _browserCombo.SelectedItem = "Custom"; }
        };
        p.Controls.Add(btnBrowseInc);
        y += 32;

        p.Controls.Add(MakeLbl("Custom Path:", Pad, y));
        _browserPathBox = MakeTxt(cfg.BrowserPath, FldX, y, FldW);
        p.Controls.Add(_browserPathBox);

        return p;
    }

    //######################################
    //Section: RunAs Profiles
    //######################################
    private Panel BuildSectionRunAs(AppConfig cfg)
    {
        var p = MakeSection();
        int y = Pad;

        AddSectionHeader(p, "👤  RunAs Profiles", ref y);

        const int listW = 160;
        const int listH = 200;

        _runasListBox = new ListBox
        {
            Left = Pad, Top = y, Width = listW, Height = listH,
            Font = new Font("Segoe UI", 9f)
        };
        foreach (var profile in _runasProfiles)
            _runasListBox.Items.Add(profile.Name);
        _runasListBox.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        p.Controls.Add(_runasListBox);

        //Left edge of right panel
        int rx  = Pad + listW + 14;
        //Available width
        int rw  = ContentW - rx - Pad; 
        int ry  = y;
        //Label width
        int rlw = 96;       
        //Field X
        int rfx = rx + rlw + 4;      
        //Field Width
        int rfw = rw - rlw - 4;       

        p.Controls.Add(new Label { Text = "Profile Name:", Left = rx, Top = ry + 3, Width = rlw, Height = 20, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f) });
        _runasNameBox = MakeTxt("", rfx, ry, rfw); p.Controls.Add(_runasNameBox); ry += 30;

        p.Controls.Add(new Label { Text = "Username:", Left = rx, Top = ry + 3, Width = rlw, Height = 20, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f) });
        _runasUserBox = MakeTxt("", rfx, ry, rfw); p.Controls.Add(_runasUserBox);
        p.Controls.Add(MakeHint("Format: DOMAIN\\user or user@domain", rfx, ry + 24));
        ry += 46;

        p.Controls.Add(new Label { Text = "Password:", Left = rx, Top = ry + 3, Width = rlw, Height = 20, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f) });
        _runasPassBox = MakeTxt("", rfx, ry, rfw, password: true); p.Controls.Add(_runasPassBox);
        p.Controls.Add(MakeHint("Leave blank  Windows prompts at launch", rfx, ry + 24));
        ry += 46;

        int rbw = (rfw - 4) / 3;
        var btnAdd  = MakeBtn("+ Add",     rfx,                   ry, rbw - 2);
        var btnSave = MakeBtn("💾 Save",   rfx + rbw,             ry, rbw - 2);
        var btnDel  = MakeBtn("🗑 Delete", rfx + rbw * 2 + 4,     ry, rbw - 2);

        btnAdd.Click += (_, _) =>
        {
            var profile = new RunAsProfile { Name = "New Profile" };
            _runasProfiles.Add(profile);
            _runasListBox.Items.Add(profile.Name);
            _runasListBox.SelectedIndex = _runasListBox.Items.Count - 1;
        };
        btnSave.Click += (_, _) =>
        {
            if (_selectedProfileIndex < 0 || _selectedProfileIndex >= _runasProfiles.Count) return;
            _runasProfiles[_selectedProfileIndex].Name     = _runasNameBox?.Text.Trim() ?? "";
            _runasProfiles[_selectedProfileIndex].Username = _runasUserBox?.Text.Trim() ?? "";
            //Encrypt password directly from textbox without storing as plain string
            var passText = _runasPassBox?.Text ?? "";
            if (!string.IsNullOrEmpty(passText))
            {
                var secure = new System.Security.SecureString();
                foreach (var c in passText) secure.AppendChar(c);
                secure.MakeReadOnly();
                _runasProfiles[_selectedProfileIndex].EncryptFromSecureString(secure);
                secure.Dispose();
            }
            else
            {
                _runasProfiles[_selectedProfileIndex].PasswordEncrypted = "";
            }
            if (_runasPassBox != null) _runasPassBox.Clear();
            _runasListBox.Items[_selectedProfileIndex] = _runasProfiles[_selectedProfileIndex].Name;
            Status("✅ Profile saved - click 'Save & Apply' to write to disk");
        };

        btnDel.Click += (_, _) =>
        {
            if (_selectedProfileIndex < 0 || _runasProfiles.Count <= 1)
            { Status("❌ Cannot delete - at least one profile required"); return; }
            _runasProfiles.RemoveAt(_selectedProfileIndex);
            _runasListBox.Items.RemoveAt(_selectedProfileIndex);
            if (_runasListBox.Items.Count > 0)
                _runasListBox.SelectedIndex = Math.Max(0, _selectedProfileIndex - 1);
        };
        p.Controls.AddRange(new Control[] { btnAdd, btnSave, btnDel });

        return p;
    }

    //######################################
    //Section: Advanced
    //######################################
    private Panel BuildSectionAdvanced(AppConfig cfg)
    {
        var p = MakeSection();
        int y = Pad;

        AddSectionHeader(p, "⚙️  Advanced", ref y);

        //Poll interval + diagnostics on same row
        p.Controls.Add(MakeLbl("Poll Interval (s):", Pad, y));
        _pollBox = new NumericUpDown
        {
            Minimum = 30, Maximum = 3600,
            Value   = Math.Max(30, cfg.FlavourPollSeconds),
            Left    = FldX, Top = y, Width = 90,
            Font    = new Font("Segoe UI", 9f)
        };
        p.Controls.Add(_pollBox);
        _diagCheck = new CheckBox
        {
            Text    = "Enable Diagnostics",
            Checked = cfg.Diagnostics,
            Left    = FldX + 100, Top = y + 2, Width = 180,
            Font    = new Font("Segoe UI", 9f)
        };
        p.Controls.Add(_diagCheck);
        y += 32;

        //Token warn days
        p.Controls.Add(MakeLbl("Token Warn Days:", Pad, y));
        _warnDaysBox = new NumericUpDown
        {
            Minimum = 1, Maximum = 90,
            Value   = Math.Max(1, cfg.TokenExpiryWarnDays),
            Left    = FldX, Top = y, Width = 90,
            Font    = new Font("Segoe UI", 9f)
        };
        p.Controls.Add(_warnDaysBox);
        p.Controls.Add(MakeHint("Days before expiry to show tray warning", FldX + 96, y + 3));
        y += 32;

        AddDivider(p, ref y);

        int advBtnW = (ContentW - Pad * 2 - 16) / 3;
        var btnClearCache     = MakeBtn("🗑 Clear Update Cache", Pad,                    y, advBtnW);
        var btnLapsSettings   = MakeBtn("🛡️ LAPS Settings",     Pad + advBtnW + 8,      y, advBtnW);
        var btnRestoreDefault = MakeBtn("♻️ Restore Defaults",   Pad + (advBtnW + 8) * 2, y, advBtnW);

        btnClearCache.Click   += (_, _) => Updater.ClearUpdateCache();
        btnLapsSettings.Click += (_, _) => { using var w = new LapsSettingsWindow(); w.ShowDialog(this); };
        btnRestoreDefault.Click += async (_, _) =>
        {
            if (MessageBox.Show(
                "Reset advanced settings and UI fields to their default values?\n\n" +
                "LAPS config, token warn days, and RunAs profile seeds will be restored from org defaults.\n\n" +
                "Your RunAs profile passwords and personal settings will not be deleted.",
                "Restore Defaults", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _urlBox!.Text                  = "";
                if (_cfgRepoOwnerBox != null) _cfgRepoOwnerBox.Text = "";
                if (_cfgRepoNameBox  != null) _cfgRepoNameBox.Text  = "";
                if (_radCfgPrivate   != null) _radCfgPrivate.Checked = true;
                if (_appUrlBox       != null) { _appUrlBox.Text = "https://github.com"; _appUrlBox.ReadOnly = false; _appUrlBox.BackColor = Color.White; }
                if (_chkAppUrlSame   != null) _chkAppUrlSame.Checked = false;
                if (_appProjectIdBox != null) _appProjectIdBox.Value = 0;
                if (_appRepoPathBox  != null) _appRepoPathBox.Text   = "";
                if (_appRepoOwnerBox != null) _appRepoOwnerBox.Text  = "";
                if (_appRepoNameBox  != null) _appRepoNameBox.Text   = "";
                if (_radAppPublic    != null) _radAppPublic.Checked  = true;
                _pollBox!.Value                = 300;
                _diagCheck!.Checked            = false;
                _manualCheck!.Checked          = false;
                _onlySubscribedCheck!.Checked  = false;
                _warnDaysBox!.Value            = 0;
                _urlBrowserCombo!.SelectedItem = "Default";
                _urlBrowserPathBox!.Text       = "";
                _browserCombo!.SelectedItem    = "Default";
                _browserPathBox!.Text          = "";

                Status("⏳ Restoring org defaults...");
                var orgResult = await ConfigLoader.ForceApplyOrgDefaultsAsync();
                _warnDaysBox.Value = Math.Max(1, ConfigLoader.AppConfig.TokenExpiryWarnDays);
                Status($"{orgResult} - click 'Save & Apply' to keep changes.");
            }
        };
        p.Controls.AddRange(new Control[] { btnClearCache, btnLapsSettings, btnRestoreDefault });

        return p;
    }

    //######################################
    //Section builder helpers
    //######################################
    private static Panel MakeSection() =>
        new() { BackColor = Color.FromArgb(245, 245, 245), Padding = new Padding(0), AutoScroll = true };

    private static void AddSectionHeader(Panel p, string text, ref int y)
    {
        p.Controls.Add(new Label
        {
            Text      = text,
            Left = Pad, Top = y, Width = ContentW - Pad * 2, Height = 24,
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 28, 28)
        });
        y += 30;
        p.Controls.Add(new Panel
        {
            Left = Pad, Top = y, Width = ContentW - Pad * 2, Height = 1,
            BackColor = Color.FromArgb(220, 220, 220)
        });
        y += 10;
    }

    private static void AddField(Panel p, string label, ref int y)
    {
        p.Controls.Add(MakeLbl(label, Pad, y));
        y += 28;
    }

    private static void AddDivider(Panel p, ref int y)
    {
        p.Controls.Add(new Panel
        {
            Left = Pad, Top = y, Width = ContentW - Pad * 2, Height = 1,
            BackColor = Color.FromArgb(220, 220, 220)
        });
        y += 10;
    }

    //######################################
    //Control factories
    //######################################
    private static Label MakeLbl(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y + 3, Width = LblW, Height = 20, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f) };

    private static Label MakeHint(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y, Width = ContentW - x - Pad, Height = 16, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f) };

    private static TextBox MakeTxt(string text, int x, int y, int width, bool password = false) =>
        new() { Text = text, Left = x, Top = y, Width = width, UseSystemPasswordChar = password, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };

    private static RadioButton MakeRadio(string text, int x, int y, bool check) =>
        new() { Text = text, Left = x, Top = y + 3, Width = 70, Height = 20, Checked = check, Font = new Font("Segoe UI", 9f) };

    private static Button MakeBtn(string text, int x, int y, int width) =>
        new() { Text = text, Left = x, Top = y, Width = width, Height = 27, Font = new Font("Segoe UI", 9f) };

    //######################################
    //Data helpers
    //######################################
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
        var profile = _runasProfiles[_selectedProfileIndex];
        if (_runasNameBox != null) _runasNameBox.Text = profile.Name;
        if (_runasUserBox != null) _runasUserBox.Text = profile.Username;
        // Never show decrypted password - show placeholder if one is saved
        if (_runasPassBox != null)
        {
            _runasPassBox.Clear();
            _runasPassBox.PlaceholderText = profile.HasSavedPassword ? "••••••••" : "Leave blank - Windows prompts at launch";
        }
    }

    private void SaveSettings()
    {
        try
        {
            var cfg = ConfigLoader.AppConfig;
            //Config Server
            cfg.CfgPlatform             = (_radCfgGitHub?.Checked == true) ? "github" : "gitlab";
            cfg.UrlServer               = _urlBox?.Text.Trim()                  ?? cfg.UrlServer;
            cfg.CfgRepoOwner            = _cfgRepoOwnerBox?.Text.Trim()         ?? cfg.CfgRepoOwner;
            cfg.CfgRepoName             = _cfgRepoNameBox?.Text.Trim()          ?? cfg.CfgRepoName;
            cfg.CfgPublic               = _radCfgPublic?.Checked                ?? cfg.CfgPublic;
            cfg.FlavourProjectId        = (int?)_projectIdBox?.Value            ?? cfg.FlavourProjectId;
            //App Updates
            cfg.AppPlatform             = (_radAppGitHub?.Checked == true) ? "github" : "gitlab";
            cfg.AppUrlServer            = (_chkAppUrlSame?.Checked == true)
                ? (_urlBox?.Text.Trim() ?? cfg.UrlServer)
                : (_appUrlBox?.Text.Trim() ?? cfg.AppUrlServer);
            cfg.AppRepoOwner            = _appRepoOwnerBox?.Text.Trim()         ?? cfg.AppRepoOwner;
            cfg.AppRepoName             = _appRepoNameBox?.Text.Trim()          ?? cfg.AppRepoName;
            cfg.AppPublic               = _radAppPublic?.Checked                ?? cfg.AppPublic;
            cfg.AppProjectId            = (int?)_appProjectIdBox?.Value         ?? cfg.AppProjectId;
            cfg.AppRepoPath             = _appRepoPathBox?.Text.Trim()          ?? cfg.AppRepoPath;
            //Shared
            cfg.Flavour                 = (string?)_flavourCombo?.SelectedItem  ?? cfg.Flavour;
            cfg.Diagnostics             = _diagCheck?.Checked                   ?? cfg.Diagnostics;
            cfg.ManualMode              = _manualCheck?.Checked                 ?? cfg.ManualMode;
            cfg.ShowOnlySubscribedFlavour = _onlySubscribedCheck?.Checked       ?? cfg.ShowOnlySubscribedFlavour;
            cfg.FlavourPollSeconds      = (int?)_pollBox?.Value                 ?? cfg.FlavourPollSeconds;
            cfg.TokenExpiryWarnDays     = (int?)_warnDaysBox?.Value             ?? cfg.TokenExpiryWarnDays;
            cfg.UrlBrowserName          = (_urlBrowserCombo?.SelectedItem?.ToString() ?? "default").ToLowerInvariant();
            cfg.UrlBrowserPath          = _urlBrowserPathBox?.Text.Trim()       ?? cfg.UrlBrowserPath;
            cfg.BrowserName             = (_browserCombo?.SelectedItem?.ToString() ?? "default").ToLowerInvariant();
            cfg.BrowserPath             = _browserPathBox?.Text.Trim()          ?? cfg.BrowserPath;
            cfg.RunAsProfiles           = _runasProfiles;

            ConfigLoader.Save(cfg);
            TokenManager.Reset();
            Status("✅ Settings saved and applied");
        }
        
        catch (Exception ex) { Status($"❌ Save failed: {ex.Message}"); }
    }

    private void CheckFlavourUpdates()
    {
        Status("⏳ Checking for flavour updates...");
        _ = Task.Run(async () =>
        {
            await ConfigLoader.CheckFlavourUpdateAsync();
            Invoke(() => Status("✅ Flavour check complete"));
        });
    }

    private void Status(string msg)
    {
        if (_statusLabel == null) return;
        if (InvokeRequired) { Invoke(() => Status(msg)); return; }
        _statusLabel.Text      = msg;
        _statusLabel.ForeColor = msg.StartsWith("❌") ? Color.DarkRed : Color.DarkBlue;
    }
}
