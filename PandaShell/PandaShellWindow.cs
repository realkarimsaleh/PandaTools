using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public class PandaShellWindow : Form
{
    //######################################
    // Win32 API for Embedding Console & Input Focus
    //######################################
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    private const int GWL_STYLE      = -16;
    private const int WS_VISIBLE     = 0x10000000;
    private const int WS_CHILD       = 0x40000000;
    private const int WS_POPUP       = unchecked((int)0x80000000);
    private const int WS_CAPTION     = 0x00C00000; // Title bar
    private const int WS_THICKFRAME  = 0x00040000; // Sizable border
    private const int SW_RESTORE     = 9;

    //######################################
    // Sidebar & UI Controls
    //######################################
    private ListBox?       bookmarkList;
    private TextBox?       hostBox;
    private NumericUpDown? portBox;
    private ComboBox?      accountCombo;
    private ComboBox?      queryCombo;
    private Label?         queryLabel;
    private TextBox?       userBox;
    private CheckBox?      includeDomainCheck; 
    private TextBox?       lapsPassBox;        
    private Button?        fetchLapsBtn;
    private Button?        copyLapsBtn;
    private Label?         statusLabel;
    private Button?        connectBtn;
    private TabControl?    tabs;

    private readonly List<PandaShellBookmark> bookmarks = new();
    private string? fetchedLapsPassword;

    private const int SideW  = 270;
    private const int GrpW   = SideW - 16;  
    private const int LblW   = 82;
    private const int CtrlX  = 92;
    private const int CtrlW  = GrpW - CtrlX - 10;  

    private bool IsLaps => string.Equals(accountCombo?.SelectedItem?.ToString(), "LAPS", StringComparison.OrdinalIgnoreCase);
    private bool IsRunAs => accountCombo?.SelectedItem != null && !IsLaps && !string.Equals(accountCombo.SelectedItem.ToString(), "Manual", StringComparison.OrdinalIgnoreCase);

    public PandaShellWindow()
    {
        Text            = "PandaShell";
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable; 
        MaximizeBox     = true;
        Icon            = AppIcon.Get();
        
        Size = new Size(1100, 680);
        MinimumSize = new Size(700, 500);

        BuildLayout();
        LoadBookmarks();

        ConfigLoader.OnConfigReloaded += OnConfigReloaded;
    }

    private void BuildLayout()
    {
        var appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        var sidebar = new Panel
        {
            Width = SideW, Dock = DockStyle.Left, BackColor = Color.White
        };

        var header = new Panel { Left = 0, Top = 0, Width = SideW, Height = 34, BackColor = Color.FromArgb(28, 28, 28) };
        header.Controls.Add(new Label { Text = $"🐼 PandaShell v{appVersion}", Left = 10, Top = 7, Width = SideW - 50, Height = 20, ForeColor = Color.White, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        
        var gear = new Button { Text = "⚙", Left = SideW - 38, Top = 4, Width = 28, Height = 26, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(28, 28, 28), Font = new Font("Segoe UI", 10f), Cursor = Cursors.Hand, TabStop = false };
        gear.FlatAppearance.BorderSize = 0; gear.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
        
        gear.Click += (_, _) => { using var w = new LapsSettingsWindow(); w.ShowDialog(this); };
        
        header.Controls.Add(gear); sidebar.Controls.Add(header);

        var y = 42;
        sidebar.Controls.Add(new Label { Text = "Bookmarks", Left = 10, Top = y, Width = SideW - 20, Height = 18, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        y += 20;
        bookmarkList = new ListBox { Left = 10, Top = y, Width = SideW - 20, Height = 110, Font = new Font("Segoe UI", 9f) };
        bookmarkList.SelectedIndexChanged += (_, _) => LoadSelectedBookmark();
        sidebar.Controls.Add(bookmarkList); y += 114;

        var bAdd  = MkBtn("+ Add", 10, y, 70); var bEdit = MkBtn("✎ Edit", 86, y, 70); var bDel  = MkBtn("− Remove", 162, y, SideW - 172);
        bAdd.Click += (_, _) => AddBookmark(); bEdit.Click += (_, _) => EditBookmark(); bDel.Click += (_, _) => RemoveBookmark();
        sidebar.Controls.AddRange(new Control[] { bAdd, bEdit, bDel }); y += 32;

        var grp = new GroupBox { Text = "Connection", Left = 8, Top = y, Width = GrpW, Height = 268, Font = new Font("Segoe UI", 9f) };
        grp.Controls.Add(MkLbl("Host:", 8, 22)); hostBox = MkTxt("", CtrlX, 20, CtrlW); grp.Controls.Add(hostBox);
        grp.Controls.Add(MkLbl("Port:", 8, 50)); portBox = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 22, Left = CtrlX, Top = 48, Width = 80, Font = new Font("Segoe UI", 9f) }; grp.Controls.Add(portBox);
        grp.Controls.Add(MkLbl("Account:", 8, 78)); accountCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = CtrlX, Top = 76, Width = CtrlW, Font = new Font("Segoe UI", 9f), DropDownWidth = 260 };
        accountCombo.SelectedIndexChanged += (_, _) => RefreshState(); grp.Controls.Add(accountCombo);
        queryLabel = MkLbl("Query as:", 8, 106); grp.Controls.Add(queryLabel);
        queryCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = CtrlX, Top = 104, Width = CtrlW, Font = new Font("Segoe UI", 9f), DropDownWidth = 260 }; grp.Controls.Add(queryCombo);
        grp.Controls.Add(MkLbl("Username:", 8, 134)); userBox = MkTxt("", CtrlX, 132, CtrlW); grp.Controls.Add(userBox);

        includeDomainCheck = new CheckBox { Text = "Include Domain in SSH", Left = CtrlX, Top = 158, Width = CtrlW, Height = 18, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.DimGray };
        grp.Controls.Add(includeDomainCheck);

        grp.Controls.Add(MkLbl("LAPS Pwd:", 8, 180)); 
        lapsPassBox = MkTxt("", CtrlX, 178, CtrlW - 64);
        lapsPassBox.ReadOnly = true;
        grp.Controls.Add(lapsPassBox);

        fetchLapsBtn = MkBtn("Fetch", CtrlX + CtrlW - 60, 177, 60);
        fetchLapsBtn.Height = 25;
        fetchLapsBtn.Click += async (_, _) => await FetchLaps();
        grp.Controls.Add(fetchLapsBtn);

        copyLapsBtn = MkBtn("📋 Copy LAPS Password", CtrlX, 206, CtrlW);
        copyLapsBtn.Click += (_, _) => { if (!string.IsNullOrEmpty(fetchedLapsPassword)) { Clipboard.SetText(fetchedLapsPassword); St("✅ LAPS copied to clipboard."); } };
        grp.Controls.Add(copyLapsBtn);

        statusLabel = new Label { Text = "Ready", Left = 8, Top = 242, Width = GrpW - 16, Height = 18, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8.5f) };
        grp.Controls.Add(statusLabel); sidebar.Controls.Add(grp);

        PopulateAccountCombo(null); PopulateQueryCombo(null); accountCombo.SelectedIndex = 0; y += 268 + 4;

        connectBtn = MkBtn("🚀 Connect Session", 8, y, SideW - 16);
        connectBtn.Height = 36; connectBtn.BackColor = Color.FromArgb(40, 167, 69); connectBtn.ForeColor = Color.White; connectBtn.FlatStyle = FlatStyle.Flat; connectBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        connectBtn.Click += (_, _) => StartConnect(); sidebar.Controls.Add(connectBtn);
        Controls.Add(sidebar);

        tabs = new TabControl
        {
            Dock = DockStyle.Fill, Alignment = TabAlignment.Top,
            Font = new Font("Segoe UI", 9f), DrawMode = TabDrawMode.OwnerDrawFixed, Padding = new Point(14, 4)
        };
        tabs.DrawItem += Tabs_DrawItem;
        tabs.MouseDown += Tabs_MouseDown;
        Controls.Add(tabs);
        tabs.BringToFront(); 

        var welcomeTab = new TabPage { Text = "PandaShell", Padding = new Padding(0) };
        var welcomePanel = new Panel { Width = 640, Height = 280, BackColor = Color.Black };
        
        var asciiArt = 
@" ____                 _       ____  _          _ _ 
|  _ \ __ _ _ __   __| | __ _/ ___|| |__   ___| | |
| |_) / _` | '_ \ / _` |/ _` \___ \| '_ \ / _ \ | |
|  __/ (_| | | | | (_| | (_| |___) | | | |  __/ | |
|_|   \__,_|_| |_|\__,_|\__,_|____/|_| |_|\___|_|_|";

        welcomePanel.Controls.Add(new Label { Text = asciiArt, Font = new Font("Consolas", 12f, FontStyle.Bold), ForeColor = Color.LimeGreen, AutoSize = false, Width = 640, Height = 120, TextAlign = ContentAlignment.BottomCenter, Top = 20 });
        welcomePanel.Controls.Add(new Label { Text = "Select a bookmark from the left or enter connection details to launch a native shell session.", Font = new Font("Segoe UI", 10f), ForeColor = Color.LightGray, AutoSize = false, Width = 640, Height = 40, TextAlign = ContentAlignment.MiddleCenter, Top = 160 });
        
        var containerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        containerPanel.Controls.Add(welcomePanel);
        containerPanel.SizeChanged += (s, e) => { welcomePanel.Left = (containerPanel.Width - welcomePanel.Width) / 2; welcomePanel.Top = (containerPanel.Height - welcomePanel.Height) / 2; };
        welcomeTab.Controls.Add(containerPanel);
        tabs.TabPages.Add(welcomeTab);
    }

    private void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (tabs == null) return;
        var tabRect = tabs.GetTabRect(e.Index);
        var isSelected = e.State == DrawItemState.Selected;
        using var bgBrush = new SolidBrush(isSelected ? SystemColors.Window : SystemColors.Control);
        e.Graphics.FillRectangle(bgBrush, tabRect);
        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, new Rectangle(tabRect.X + 4, tabRect.Y + 4, tabRect.Width - 20, tabRect.Height - 4), SystemColors.ControlText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        if (e.Index > 0)
        {
            var closeRect = new Rectangle(tabRect.Right - 18, tabRect.Top + 6, 12, 12);
            using var crossPen = new Pen(Color.DimGray, 1.5f);
            e.Graphics.DrawLine(crossPen, closeRect.X, closeRect.Y, closeRect.Right, closeRect.Bottom);
            e.Graphics.DrawLine(crossPen, closeRect.Right, closeRect.Y, closeRect.X, closeRect.Bottom);
        }
    }

    private void Tabs_MouseDown(object? sender, MouseEventArgs e)
    {
        if (tabs == null) return;
        for (var i = 1; i < tabs.TabPages.Count; i++)
        {
            if (new Rectangle(tabs.GetTabRect(i).Right - 18, tabs.GetTabRect(i).Top + 6, 12, 12).Contains(e.Location))
            {
                var tabToClose = tabs.TabPages[i];
                
                if (tabToClose.Tag is Process p && !p.HasExited)
                {
                    try { p.Kill(); } catch { }
                    p.Dispose();
                }

                tabs.TabPages.RemoveAt(i);
                tabToClose.Dispose();
                break;
            }
        }
    }

    private void RefreshState()
    {
        if (accountCombo == null) return;
        var isLaps = IsLaps; var isRunAs = IsRunAs;
        
        if (queryCombo != null) { queryCombo.Enabled = isLaps; queryCombo.BackColor = isLaps ? SystemColors.Window : SystemColors.Control; }
        if (queryLabel != null) queryLabel.ForeColor = isLaps ? SystemColors.ControlText : SystemColors.GrayText;
        
        if (fetchLapsBtn != null) fetchLapsBtn.Enabled = isLaps;
        if (lapsPassBox != null) lapsPassBox.Enabled = isLaps;
        if (copyLapsBtn != null) copyLapsBtn.Enabled = isLaps && !string.IsNullOrEmpty(fetchedLapsPassword);

        if (includeDomainCheck != null) includeDomainCheck.Enabled = isRunAs;

        if (userBox != null)
        {
            if (isRunAs)
            {
                var profile = ConfigLoader.AppConfig.RunAsProfiles.FirstOrDefault(p => string.Equals(p.Name, accountCombo.SelectedItem?.ToString(), StringComparison.OrdinalIgnoreCase));
                userBox.Text = profile?.Username ?? userBox.Text; userBox.ReadOnly = true; userBox.BackColor = SystemColors.Control;
            }
            else if (isLaps)
            {
                userBox.ReadOnly = false; userBox.BackColor = SystemColors.Window;
                if (string.IsNullOrWhiteSpace(userBox.Text)) userBox.Text = "";
            }
            else { userBox.ReadOnly = false; userBox.BackColor = SystemColors.Window; }
        }
        St("Ready");
    }

    private void PopulateAccountCombo(string? restore) { if (accountCombo == null) return; var cur = restore ?? accountCombo.SelectedItem?.ToString(); accountCombo.Items.Clear(); accountCombo.Items.Add("LAPS"); accountCombo.Items.Add("Manual"); foreach (var p in ConfigLoader.AppConfig.RunAsProfiles) accountCombo.Items.Add(p.Name); Restore(accountCombo, cur, 0); }
    private void PopulateQueryCombo(string? restore) { if (queryCombo == null) return; var cur = restore ?? queryCombo.SelectedItem?.ToString(); queryCombo.Items.Clear(); queryCombo.Items.Add("Current User"); foreach (var p in ConfigLoader.AppConfig.RunAsProfiles) queryCombo.Items.Add(p.Name); queryCombo.Items.Add("Custom…"); Restore(queryCombo, cur, 0); }
    private static void Restore(ComboBox cb, string? name, int fallback) { if (name != null) for (var i = 0; i < cb.Items.Count; i++) if (string.Equals(cb.Items[i]?.ToString(), name, StringComparison.OrdinalIgnoreCase)) { cb.SelectedIndex = i; return; } if (cb.Items.Count > 0) cb.SelectedIndex = Math.Min(fallback, cb.Items.Count - 1); }
    private void OnConfigReloaded() { if (accountCombo == null) return; if (accountCombo.InvokeRequired) { accountCombo.Invoke(OnConfigReloaded); return; } PopulateAccountCombo(null); PopulateQueryCombo(null); RefreshState(); }
    private void LoadBookmarks() { bookmarks.Clear(); bookmarks.AddRange(PandaShellBookmarkStore.Load()); bookmarkList!.Items.Clear(); foreach (var b in bookmarks) bookmarkList.Items.Add(b.Name); if (bookmarkList.Items.Count > 0) bookmarkList.SelectedIndex = 0; RefreshState(); }
    
    private void LoadSelectedBookmark()
    {
        if (bookmarkList == null || bookmarkList.SelectedIndex < 0) return;
        var b = bookmarks[bookmarkList.SelectedIndex];
        hostBox!.Text = b.Host; portBox!.Value = b.Port; userBox!.Text = b.Username;
        
        if (includeDomainCheck != null) includeDomainCheck.Checked = b.IncludeDomain;
        
        Restore(accountCombo!, b.AccountMode?.ToLowerInvariant() == "laps" ? "LAPS" : b.AccountMode?.ToLowerInvariant() == "manual" ? "Manual" : b.RunAsName ?? "Manual", 0);
        
        fetchedLapsPassword = null; 
        if (lapsPassBox != null) lapsPassBox.Text = "";
        if (copyLapsBtn != null) copyLapsBtn.Enabled = false; 
        St("Loaded bookmark.");
    }

    private (string mode, string runAs) GetAccount() { var sel = accountCombo?.SelectedItem?.ToString() ?? "Manual"; return sel.Equals("LAPS", StringComparison.OrdinalIgnoreCase) ? ("laps", "") : sel.Equals("Manual", StringComparison.OrdinalIgnoreCase) ? ("manual", "") : ("runas", sel); }
    private void AddBookmark() { var name = Ask("Bookmark Name", "New SSH bookmark name:"); if (string.IsNullOrWhiteSpace(name)) return; var (mode, runAs) = GetAccount(); bookmarks.Add(new PandaShellBookmark { Name = name.Trim(), Host = hostBox?.Text.Trim() ?? "", Port = (int)(portBox?.Value ?? 22), Username = userBox?.Text.Trim() ?? "", AccountMode = mode, RunAsName = runAs, IncludeDomain = includeDomainCheck?.Checked ?? false }); PandaShellBookmarkStore.Save(bookmarks); LoadBookmarks(); SelectByName(name.Trim()); St("Bookmark added."); }
    private void EditBookmark() { if (bookmarkList == null || bookmarkList.SelectedIndex < 0) return; var b = bookmarks[bookmarkList.SelectedIndex]; var name = Ask("Edit Bookmark", "Bookmark name:", b.Name); if (string.IsNullOrWhiteSpace(name)) return; var (mode, runAs) = GetAccount(); b.Name = name.Trim(); b.Host = hostBox?.Text.Trim() ?? b.Host; b.Port = (int)(portBox?.Value ?? b.Port); b.Username = userBox?.Text.Trim() ?? b.Username; b.AccountMode = mode; b.RunAsName = runAs; b.IncludeDomain = includeDomainCheck?.Checked ?? false; PandaShellBookmarkStore.Save(bookmarks); LoadBookmarks(); SelectByName(b.Name); St("Bookmark updated."); }
    private void RemoveBookmark() { if (bookmarkList == null || bookmarkList.SelectedIndex < 0) return; if (MessageBox.Show($"Delete bookmark '{bookmarks[bookmarkList.SelectedIndex].Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; bookmarks.RemoveAt(bookmarkList.SelectedIndex); PandaShellBookmarkStore.Save(bookmarks); LoadBookmarks(); St("Bookmark removed."); }
    private void SelectByName(string name) { for (var i = 0; i < bookmarkList!.Items.Count; i++) if (string.Equals(bookmarkList.Items[i]?.ToString(), name, StringComparison.OrdinalIgnoreCase)) { bookmarkList.SelectedIndex = i; return; } }

    private async System.Threading.Tasks.Task FetchLaps()
    {
        var host = hostBox?.Text.Trim(); if (string.IsNullOrWhiteSpace(host)) { St("❌ Host is required for LAPS."); return; }
        RunAsProfile? queryProfile = null; var qs = queryCombo?.SelectedItem?.ToString() ?? "";
        
        if (qs == "Current User") { queryProfile = null; }
        else if (qs == "Custom…") { var u = Ask("Custom Account", "Enter DOMAIN\\username for LAPS query:"); if (string.IsNullOrWhiteSpace(u)) return; var (ok, pwd) = PwdPrompt(u, "Password for LAPS query account:"); if (!ok) return; queryProfile = new RunAsProfile { Username = u, Password = pwd }; }
        else if (!string.IsNullOrWhiteSpace(qs)) { queryProfile = ConfigLoader.AppConfig.RunAsProfiles.FirstOrDefault(p => p.Name.Equals(qs, StringComparison.OrdinalIgnoreCase)); if (queryProfile != null && string.IsNullOrEmpty(queryProfile.Password)) { var (ok, pwd) = PwdPrompt(queryProfile.Username, $"Password for \"{queryProfile.Name}\":"); if (!ok) return; queryProfile = new RunAsProfile { Name = queryProfile.Name, Username = queryProfile.Username, Password = pwd }; } }
        
        St("⏳ Fetching LAPS password...");
        var pw = await LapsClient.GetLapsPasswordAsync(host, LapsConfig.Load(), queryProfile);
        if (string.IsNullOrEmpty(pw)) { St("❌ LAPS password not found."); return; }
        
        fetchedLapsPassword = pw; 
        if (lapsPassBox != null) lapsPassBox.Text = pw;
        if (copyLapsBtn != null) copyLapsBtn.Enabled = true; 
        St("✅ LAPS fetched — click Connect.");
    }

    private void StartConnect()
    {
        var host = hostBox?.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) { St("❌ Host is required."); return; }

        var port = (int)(portBox?.Value ?? 22);
        var user = userBox?.Text.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(user)) { St("❌ Username is required."); return; }

        var (mode, runAs) = GetAccount();

        if (mode == "runas" && includeDomainCheck != null && !includeDomainCheck.Checked)
        {
            if (user.Contains("\\")) user = user.Split('\\').Last();
            else if (user.Contains("@")) user = user.Split('@').First();
        }

        bool hasCopiedPassword = false;

        if (mode == "laps")
        {
            if (string.IsNullOrEmpty(fetchedLapsPassword)) { St("❌ Fetch LAPS password first."); return; }
            Clipboard.SetText(fetchedLapsPassword);
            hasCopiedPassword = true;
        }
        else if (mode == "runas")
        {
            var profile = ConfigLoader.AppConfig.RunAsProfiles.FirstOrDefault(p => string.Equals(p.Name, runAs, StringComparison.OrdinalIgnoreCase));
            if (profile != null && !string.IsNullOrEmpty(profile.Password))
            {
                Clipboard.SetText(profile.Password);
                hasCopiedPassword = true;
            }
        }

        LaunchConnectedSession(host, port, user, hasCopiedPassword);
    }

    private void LaunchConnectedSession(string host, int port, string user, bool hasCopiedPassword)
    {
        var uniqueId = Guid.NewGuid().ToString("N");
        var uniqueTitle = $"PandaShell_{uniqueId}";
        var psScriptPath = Path.Combine(Path.GetTempPath(), $"{uniqueTitle}.ps1");

        var psCode = $@"
$Host.UI.RawUI.WindowTitle = '{uniqueTitle}'
Clear-Host
$ascii = @'
 ____                 _       ____  _          _ _ 
|  _ \ __ _ _ __   __| | __ _/ ___|| |__   ___| | |
| |_) / _` | '_ \ / _` |/ _` \___ \| '_ \ / _ \ | |
|  __/ (_| | | | | (_| | (_| |___) | | | |  __/ | |
|_|   \__,_|_| |_|\__,_|\__,_|____/|_| |_|\___|_|_|
'@

Write-Host $ascii -ForegroundColor Green
Write-Host ''
Write-Host 'Host: {host}' -ForegroundColor White
Write-Host 'User: {user}' -ForegroundColor White
Write-Host ''

if ('{hasCopiedPassword}' -eq 'True') {{
    Write-Host '💡 Password copied to clipboard! RIGHT-CLICK to paste.' -ForegroundColor Green
    Write-Host ''
}}

ssh.exe -o ConnectTimeout=10 -o StrictHostKeyChecking=accept-new -o PubkeyAuthentication=no -o PasswordAuthentication=yes -p {port} {user}@{host}

Write-Host ''
Write-Host 'Session ended. Press any key to close...' -ForegroundColor DarkGray
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') 
Remove-Item -Path $MyInvocation.MyCommand.Path -ErrorAction SilentlyContinue
";
        File.WriteAllText(psScriptPath, psCode, Encoding.UTF8);

        if (tabs == null) return;

        var tab = new TabPage { Text = host, Padding = new Padding(0) };
        var container = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        tab.Controls.Add(container);
        tabs.TabPages.Add(tab);
        tabs.SelectedTab = tab;

        var proc = new Process();
        proc.StartInfo.FileName = "conhost.exe";
        proc.StartInfo.Arguments = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{psScriptPath}\"";
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = false;
        proc.StartInfo.WindowStyle = ProcessWindowStyle.Minimized; 
        
        proc.EnableRaisingEvents = true;
        proc.Exited += (s, e) =>
        {
            if (tabs != null && !tabs.IsDisposed)
            {
                tabs.Invoke(new Action(() =>
                {
                    if (tabs.TabPages.Contains(tab))
                    {
                        tabs.TabPages.Remove(tab);
                        tab.Dispose();
                    }
                }));
            }
        };

        proc.Start();
        tab.Tag = proc; 

        Task.Run(() =>
        {
            IntPtr hwnd = IntPtr.Zero;
            for (int i = 0; i < 50; i++)
            {
                hwnd = FindWindow("ConsoleWindowClass", uniqueTitle);
                if (hwnd != IntPtr.Zero) break;
                Thread.Sleep(100);
            }

            if (hwnd != IntPtr.Zero && !container.IsDisposed)
            {
                container.Invoke(new Action(() =>
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    SetParent(hwnd, container.Handle);

                    int style = GetWindowLong(hwnd, GWL_STYLE);
                    style = style & ~WS_CAPTION & ~WS_THICKFRAME;
                    SetWindowLong(hwnd, GWL_STYLE, style);

                    MoveWindow(hwnd, 0, 0, container.Width, container.Height, true);
                    container.Resize += (s, e) => MoveWindow(hwnd, 0, 0, container.Width, container.Height, true);

                    uint consoleThreadId = GetWindowThreadProcessId(hwnd, out uint _);
                    uint appThreadId = GetCurrentThreadId();
                    AttachThreadInput(appThreadId, consoleThreadId, true);

                    container.MouseClick += (s, e) => { SetForegroundWindow(hwnd); SetFocus(hwnd); };
                    container.GotFocus += (s, e) => { SetForegroundWindow(hwnd); SetFocus(hwnd); };
                    
                    tabs.SelectedIndexChanged += (s, e) => {
                        if (tabs.SelectedTab == tab) { SetForegroundWindow(hwnd); SetFocus(hwnd); }
                    };

                    SetForegroundWindow(hwnd);
                    SetFocus(hwnd);
                }));
            }
        });

        St("🚀 Embedded session launched.");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ConfigLoader.OnConfigReloaded -= OnConfigReloaded;
        base.OnFormClosing(e);
    }

    //######################################
    // UI Helpers
    //######################################
    private static Label   MkLbl(string t, int x, int y)          => new() { Text = t, Left = x, Top = y + 2, Width = LblW, Height = 18, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f) };
    private static TextBox MkTxt(string t, int x, int y, int w)   => new() { Text = t, Left = x, Top = y,     Width = w, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };
    private static Button  MkBtn(string t, int x, int y, int w)   => new() { Text = t, Left = x, Top = y,     Width = w, Height = 26, Font = new Font("Segoe UI", 9f) };
    private void St(string msg) { if (statusLabel == null) return; statusLabel.Text = msg; statusLabel.ForeColor = msg.StartsWith("❌") ? Color.DarkRed : msg.StartsWith("🚀") || msg.StartsWith("✅") ? Color.DarkGreen : Color.DimGray; }

    private static (bool ok, string pwd) PwdPrompt(string username, string message)
    {
        const int pad = 16, fw = 400, iw = fw - pad * 2 - 16, tw = 32, bw = 80, bg = 8;
        const int mt = 16, mh = 40, ut = mt + mh + 8, pt = ut + 26, bnt = pt + 34, bh = 28, fh = bnt + bh + 48;
        const int bcl = fw - pad - 16 - bw, bol = bcl - bg - bw;

        using var frm = new Form { Text = "Password", Size = new Size(fw, fh), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, TopMost = true, Font = new Font("Segoe UI", 9f), Icon = AppIcon.Get() };
        frm.Controls.Add(new Label { Text = message, Left = pad, Top = mt, Width = iw, Height = mh, Font = new Font("Segoe UI", 9f), AutoSize = false });
        frm.Controls.Add(new Label { Text = "Username:", Left = pad, Top = ut, Width = 74, Height = 20, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 9f), AutoSize = false });
        frm.Controls.Add(new Label { Text = username, Left = pad + 76, Top = ut, Width = iw - 76, Height = 20, Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = false });
        var txt = new TextBox { Left = pad, Top = pt, Width = iw - tw - 4, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 9f) }; frm.Controls.Add(txt);
        var tog = new Button { Text = "👁", Left = pad + iw - tw, Top = pt - 1, Width = tw, Height = txt.Height + 2, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f), TabStop = false, Cursor = Cursors.Hand };
        tog.FlatAppearance.BorderSize = 1; tog.Click += (_, _) => { txt.UseSystemPasswordChar = !txt.UseSystemPasswordChar; txt.Focus(); txt.SelectionStart = txt.Text.Length; }; frm.Controls.Add(tog);
        var ok = new Button { Text = "OK", Left = bol, Top = bnt, Width = bw, Height = bh, DialogResult = DialogResult.OK, Font = new Font("Segoe UI", 9f) };
        var can = new Button { Text = "Cancel", Left = bcl, Top = bnt, Width = bw, Height = bh, DialogResult = DialogResult.Cancel, Font = new Font("Segoe UI", 9f) };
        frm.Controls.AddRange(new Control[] { ok, can }); frm.AcceptButton = ok; frm.CancelButton = can; frm.Shown += (_, _) => txt.Focus();
        var r = frm.ShowDialog(); return (r == DialogResult.OK, txt.Text);
    }

    private static string? Ask(string title, string prompt, string def = "")
    {
        using var frm = new Form { Text = title, Size = new Size(420, 160), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, TopMost = true, Icon = AppIcon.Get() };
        frm.Controls.Add(new Label { Text = prompt, Left = 12, Top = 12, Width = 380, Height = 20, Font = new Font("Segoe UI", 9f) });
        var box = new TextBox { Left = 12, Top = 40, Width = 380, Text = def, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle }; frm.Controls.Add(box);
        var ok = new Button { Text = "OK", Left = 226, Top = 78, Width = 80, Height = 28, DialogResult = DialogResult.OK, Font = new Font("Segoe UI", 9f) };
        var can = new Button { Text = "Cancel", Left = 312, Top = 78, Width = 80, Height = 28, DialogResult = DialogResult.Cancel, Font = new Font("Segoe UI", 9f) };
        frm.Controls.Add(ok); frm.Controls.Add(can); frm.AcceptButton = ok; frm.CancelButton = can;
        return frm.ShowDialog() == DialogResult.OK ? box.Text : null;
    }
}