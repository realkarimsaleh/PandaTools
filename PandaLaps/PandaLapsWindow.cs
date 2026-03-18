using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public class PandaLapsWindow : Form
{
    private static PandaLapsWindow? _instance;
    public static void ShowWindow()
    {
        if (_instance == null || _instance.IsDisposed) { _instance = new PandaLapsWindow(); _instance.Show(); }
        else { if (_instance.WindowState == FormWindowState.Minimized) _instance.WindowState = FormWindowState.Normal; _instance.BringToFront(); _instance.Activate(); }
    }

    private ComboBox? _accountCombo;
    private CheckBox? _readOnlyCheck;
    private CheckBox? _expireNowCheck;
    private DateTimePicker? _datePicker;
    private DateTimePicker? _timePicker;
    private Button? _templateBtn, _importBtn, _clearBtn, _runBtn, _exportBtn;
    private Label? _statusLabel;
    private TabControl? _tabs;
    private DataGridView? _grid;
    private TextBox? _singleHostBox, _singlePwdBox, _singleExpiryBox;
    private DataGridView? _phoneticGrid;

    private const int SideW = 270;
    private const int GrpW = SideW - 16;
    private const int CtrlX = 92;
    private const int CtrlW = GrpW - CtrlX - 10;
    private bool IsProcessing = false;

    private PandaLapsWindow()
    {
        Text = "PandaLAPS";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Icon = AppIcon.Get();
        BackColor = Color.White;
        Size = new Size(1100, 640);
        BuildLayout();
    }

    private void BuildLayout()
    {
        var sidebar = new Panel { Width = SideW, Dock = DockStyle.Left, BackColor = Color.White };
        var appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var header = new Panel { Left = 0, Top = 0, Width = SideW, Height = 34, BackColor = Color.FromArgb(28, 28, 28) };
        header.Controls.Add(new Label { Text = $"🛡️ PandaLAPS v{appVersion}", Left = 10, Top = 7, Width = SideW - 50, Height = 20, ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });

        var gear = new Button { Text = "⚙", Left = SideW - 38, Top = 4, Width = 28, Height = 26, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(28, 28, 28), Font = new Font("Segoe UI", 10f), Cursor = Cursors.Hand, TabStop = false };
        gear.FlatAppearance.BorderSize = 0; gear.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
        gear.Click += (_, _) => { using var w = new LapsSettingsWindow(); w.ShowDialog(this); };
        header.Controls.Add(gear);
        sidebar.Controls.Add(header);

        var y = 50;
        var grp = new GroupBox { Text = "LAPS Parameters", Left = 8, Top = y, Width = GrpW, Height = 210, Font = new Font("Segoe UI", 9f) };
        grp.Controls.Add(MkLbl("Run As:", 8, 22));
        _accountCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = CtrlX, Top = 20, Width = CtrlW, Font = new Font("Segoe UI", 9f) };
        foreach (var p in ConfigLoader.AppConfig.RunAsProfiles) _accountCombo.Items.Add(p.Name);
        if (_accountCombo.Items.Count > 0) _accountCombo.SelectedIndex = 0;
        grp.Controls.Add(_accountCombo);

        _readOnlyCheck = new CheckBox { Text = "Read-Only Mode", Left = CtrlX, Top = 52, Width = CtrlW, Font = new Font("Segoe UI", 9f) };
        grp.Controls.Add(_readOnlyCheck);
        _expireNowCheck = new CheckBox { Text = "Expire Immediately", Checked = true, Left = CtrlX, Top = 78, Width = CtrlW, Font = new Font("Segoe UI", 9f) };
        grp.Controls.Add(_expireNowCheck);

        grp.Controls.Add(MkLbl("Custom Date:", 8, 106));
        _datePicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Left = CtrlX, Top = 104, Width = CtrlW, Font = new Font("Segoe UI", 9f), Enabled = false };
        grp.Controls.Add(_datePicker);
        grp.Controls.Add(MkLbl("Custom Time:", 8, 136));
        _timePicker = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Left = CtrlX, Top = 134, Width = CtrlW, Font = new Font("Segoe UI", 9f), Enabled = false };
        grp.Controls.Add(_timePicker);

        _readOnlyCheck.CheckedChanged += (_, _) => { _expireNowCheck.Enabled = !_readOnlyCheck.Checked; _datePicker.Enabled = !_readOnlyCheck.Checked && !_expireNowCheck.Checked; _timePicker.Enabled = !_readOnlyCheck.Checked && !_expireNowCheck.Checked; };
        _expireNowCheck.CheckedChanged += (_, _) => { _datePicker.Enabled = !_readOnlyCheck!.Checked && !_expireNowCheck.Checked; _timePicker.Enabled = !_readOnlyCheck!.Checked && !_expireNowCheck.Checked; };

        sidebar.Controls.Add(grp);
        y += 222;

        int btnSpacing = 6;
        int rowBtnW = (GrpW - btnSpacing) / 2;

        _templateBtn = new Button { Text = "📄 Template", Left = 8, Top = y, Width = rowBtnW, Height = 28, Font = new Font("Segoe UI", 8.5f) };
        _templateBtn.Click += (_, _) => DownloadTemplate();

        _importBtn = new Button { Text = "📂 Import", Left = 8 + rowBtnW + btnSpacing, Top = y, Width = rowBtnW, Height = 28, Font = new Font("Segoe UI", 8.5f) };
        _importBtn.Click += (_, _) => ImportCsv();

        y += 34;

        _clearBtn = new Button { Text = "🗑️ Clear", Left = 8, Top = y, Width = GrpW, Height = 28, Font = new Font("Segoe UI", 8.5f) };
        _clearBtn.Click += (_, _) => {
            if (_tabs?.SelectedIndex == 0) {
                _singleHostBox!.Clear();
                _singlePwdBox!.Clear();
                _singleExpiryBox!.Clear();
                _phoneticGrid!.Rows.Clear();
                St("🗑️ Single query fields cleared.");
            } else {
                _grid?.Rows.Clear();
                St("🗑️ Batch grid cleared.");
            }
        };

        sidebar.Controls.AddRange(new Control[] { _templateBtn, _importBtn, _clearBtn });

        int bottomY = this.ClientSize.Height - 30;
        _statusLabel = new Label { Text = "Ready.", Left = 8, Top = bottomY, Width = GrpW, Height = 20, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8.5f) };
        sidebar.Controls.Add(_statusLabel);

        bottomY -= 36;
        _exportBtn = new Button { Text = "💾 Export Results", Left = 8, Top = bottomY, Width = GrpW, Height = 30, Font = new Font("Segoe UI", 9f), Enabled = false };
        _exportBtn.Click += (_, _) => ExportCsv();
        sidebar.Controls.Add(_exportBtn);

        bottomY -= 42;
        _runBtn = new Button { Text = "🚀 LAPS Query", Left = 8, Top = bottomY, Width = GrpW, Height = 36, BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        _runBtn.FlatAppearance.BorderSize = 0;
        _runBtn.Click += async (_, _) => { if (_tabs?.SelectedIndex == 0) await RunSingleAsync(); else await RunBatchAsync(); };
        sidebar.Controls.Add(_runBtn);

        Controls.Add(sidebar);

        _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), Padding = new Point(20, 6) };
        var tabSingle = new TabPage { Text = "🔍 Single Query", BackColor = Color.FromArgb(245, 245, 245) };
        var tabBatch = new TabPage { Text = "🚀 Batch Mode", BackColor = Color.FromArgb(245, 245, 245) };
        BuildSingleTab(tabSingle);
        BuildBatchTab(tabBatch);
        _tabs.TabPages.Add(tabSingle);
        _tabs.TabPages.Add(tabBatch);

        _tabs.SelectedIndexChanged += (_, _) => {
            bool isBatch = _tabs.SelectedIndex == 1;
            //Template and Import disable (gray out) in Single Mode
            _templateBtn.Enabled = isBatch;
            _importBtn.Enabled = isBatch;
            _exportBtn.Enabled = isBatch;
        };
        
        //Initial state: Single tab is active, so gray out batch buttons
        _templateBtn.Enabled = _importBtn.Enabled = _exportBtn.Enabled = false;

        Controls.Add(_tabs);
        _tabs.BringToFront();
    }

    private void BuildSingleTab(TabPage page)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30), BackColor = Color.Transparent };
        int leftX = 40, rightX = 390;
        var hostLbl = new Label { Text = "Target Hostname:", Left = leftX, Top = 40, Width = 300, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        _singleHostBox = new TextBox { Left = leftX, Top = 65, Width = 300, Font = new Font("Segoe UI", 12f), BorderStyle = BorderStyle.FixedSingle, CharacterCasing = CharacterCasing.Upper };
        _singleHostBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _runBtn!.PerformClick(); } };

        var pwdLbl = new Label { Text = "LAPS Password:", Left = leftX, Top = 130, Width = 300, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        _singlePwdBox = new TextBox { Left = leftX, Top = 155, Width = 260, Font = new Font("Consolas", 16f, FontStyle.Bold), BorderStyle = BorderStyle.FixedSingle, ReadOnly = true, BackColor = Color.White };
        var copyBtn = new Button { Text = "📋", Left = leftX + 266, Top = 154, Width = 34, Height = 34, Font = new Font("Segoe UI", 12f), Cursor = Cursors.Hand, BackColor = Color.White, FlatStyle = FlatStyle.Flat };
        copyBtn.FlatAppearance.BorderColor = Color.Silver;
        copyBtn.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(_singlePwdBox.Text)) { Clipboard.SetText(_singlePwdBox.Text); St("✅ Password copied to clipboard."); } };

        var expLbl = new Label { Text = "Current Expiry:", Left = leftX, Top = 220, Width = 300, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        _singleExpiryBox = new TextBox { Left = leftX, Top = 245, Width = 300, Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle, ReadOnly = true, BackColor = Color.White };

        var phoneticLbl = new Label { Text = "Phonetic Breakdown:", Left = rightX, Top = 40, Width = 200, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        _phoneticGrid = new DataGridView { Left = rightX, Top = 65, Width = 380, Height = 470, Anchor = AnchorStyles.Top | AnchorStyles.Left, BorderStyle = BorderStyle.FixedSingle, BackgroundColor = Color.White, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, RowHeadersVisible = false, AllowUserToResizeColumns = false, AllowUserToResizeRows = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, SelectionMode = DataGridViewSelectionMode.FullRowSelect, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, Font = new Font("Segoe UI", 10.5f), EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(220, 220, 220), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ScrollBars = ScrollBars.None };
        _phoneticGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 28, 28); _phoneticGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; _phoneticGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold); _phoneticGrid.ColumnHeadersHeight = 34;
        _phoneticGrid.Columns.Add("ColChar", "Char"); _phoneticGrid.Columns[0].FillWeight = 18; _phoneticGrid.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _phoneticGrid.Columns.Add("ColType", "Type"); _phoneticGrid.Columns[1].FillWeight = 32; _phoneticGrid.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _phoneticGrid.Columns.Add("ColWord", "Phonetic"); _phoneticGrid.Columns[2].FillWeight = 50;
        foreach (DataGridViewColumn col in _phoneticGrid.Columns) col.SortMode = DataGridViewColumnSortMode.NotSortable;
        _phoneticGrid.DataBindingComplete += (_, _) => _phoneticGrid.ClearSelection();
        panel.Controls.AddRange(new Control[] { hostLbl, _singleHostBox, pwdLbl, _singlePwdBox, copyBtn, expLbl, _singleExpiryBox, phoneticLbl, _phoneticGrid });
        page.Controls.Add(panel);
    }

    private void BuildBatchTab(TabPage page)
    {
        _grid = new DataGridView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackgroundColor = Color.FromArgb(245, 245, 245), AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersWidth = 30, Font = new Font("Segoe UI", 9.5f), EnableHeadersVisualStyles = false, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single, GridColor = Color.FromArgb(220, 220, 220) };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 28, 28); _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold); _grid.ColumnHeadersHeight = 34;
        
        _grid.Columns.Add("ColHost", "Hostname");
        _grid.Columns[0].Width = 110;
        
        _grid.Columns.Add("ColPwd", "LAPS Password");
        _grid.Columns[1].Width = 160;
        
        _grid.Columns.Add("ColOldExp", "Current Expiry");
        _grid.Columns[2].Width = 160;
        
        _grid.Columns.Add("ColNewExp", "New Expiry");
        _grid.Columns[3].Width = 160;
        
        _grid.Columns.Add("ColStat", "Status");
        _grid.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        foreach (DataGridViewColumn col in _grid.Columns) if (col.Index > 0) col.ReadOnly = true;
        _grid.KeyDown += Grid_KeyDown;
        page.Controls.Add(_grid);
    }

    private static Label MkLbl(string t, int x, int y) => new() { Text = t, Left = x, Top = y + 2, Width = 82, Height = 18, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f) };
    private void St(string msg) { if (_statusLabel != null) _statusLabel.Text = msg; }

    private async Task RunSingleAsync()
    {
        var host = _singleHostBox?.Text.Trim();
        if (string.IsNullOrWhiteSpace(host)) { St("❌ Please enter a hostname."); return; }
        if (IsProcessing) return;
        var profile = EnsureProfileReady(); if (profile == null) return;
        bool isReadOnly = _readOnlyCheck!.Checked;
        DateTime selectedTime = _expireNowCheck!.Checked ? DateTime.Now : _datePicker!.Value.Date.Add(_timePicker!.Value.TimeOfDay);
        var fileTime = selectedTime.ToUniversalTime().ToFileTimeUtc();
        IsProcessing = true; _runBtn!.Enabled = false; _runBtn.Text = "⏳ Processing...";
        _singlePwdBox!.Text = ""; _singleExpiryBox!.Text = "Fetching..."; _phoneticGrid!.Rows.Clear();
        var cfg = LapsConfig.Load();
        try {
            St($"⏳ Querying {host}...");
            string oldExpiry = await LapsClient.GetLapsExpiryAsync(host, cfg, profile);
            if (oldExpiry == "NotFound") { _singleExpiryBox.Text = "NOT IN AD"; St($"❌ {host} not in AD."); return; }
            string actualNewExpiry = "";
            if (!isReadOnly) {
                St($"⏳ Rotating password on {host}...");
                bool setSuccess = await LapsClient.SetLapsExpiryAsync(host, fileTime, cfg, profile);
                if (!setSuccess) throw new Exception("Failed to set new AD expiry time.");
                await Task.Delay(cfg.ValidationDelayMs);
                actualNewExpiry = await LapsClient.GetLapsExpiryAsync(host, cfg, profile);
            }
            var newPwd = await LapsClient.GetLapsPasswordAsync(host, cfg, profile);
            if (!string.IsNullOrEmpty(newPwd)) {
                _singlePwdBox.Text = newPwd; _singleExpiryBox.Text = isReadOnly ? oldExpiry : actualNewExpiry;
                foreach (char c in newPwd) {
                    string phonetic = PhoneticSpeller.GetPhonetic(c); string type = char.IsDigit(c) ? "Number" : char.IsLetter(c) ? (char.IsUpper(c) ? "Uppercase" : "Lowercase") : "Symbol";
                    int rowIndex = _phoneticGrid.Rows.Add(c.ToString(), type, phonetic); _phoneticGrid.Rows[rowIndex].Height = 26;
                    if (char.IsUpper(c)) _phoneticGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.DarkBlue;
                    else if (char.IsDigit(c) || !char.IsLetterOrDigit(c)) _phoneticGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.DarkGreen;
                }
                St($"✅ Success: LAPS retrieved for {host}.");
            } else { _singlePwdBox.Text = "NOT FOUND"; St($"❌ Password not found for {host}."); _singleExpiryBox.Text = "NOT FOUND"; }
        } catch (Exception ex) { _singlePwdBox.Text = "ERROR"; _singleExpiryBox.Text = "ERROR"; St($"❌ Error: {ex.Message}"); }
        finally { IsProcessing = false; _runBtn.Enabled = true; _runBtn.Text = "🚀 LAPS Query"; }
    }

    private async Task RunBatchAsync()
    {
        if (_grid == null || _grid.Rows.Count <= 1 || IsProcessing) return;
        var profile = EnsureProfileReady(); if (profile == null) return;
        bool isReadOnly = _readOnlyCheck!.Checked;
        DateTime selectedLocalTime = _expireNowCheck!.Checked ? DateTime.Now : _datePicker!.Value.Date.Add(_timePicker!.Value.TimeOfDay);
        string displayTargetDate = selectedLocalTime.ToString("g"); var fileTime = selectedLocalTime.ToUniversalTime().ToFileTimeUtc();
        IsProcessing = true; _runBtn!.Enabled = false; _runBtn.Text = "⏳ Processing...";
        var cfg = LapsConfig.Load();
        for (int i = 0; i < _grid.Rows.Count - 1; i++) {
            var row = _grid.Rows[i]; var host = row.Cells[0].Value?.ToString()?.Trim(); if (string.IsNullOrWhiteSpace(host)) continue;
            _grid.FirstDisplayedScrollingRowIndex = i;
            try {
                row.Cells[4].Value = "⏳ Fetching current...";
                string oldExpiry = await LapsClient.GetLapsExpiryAsync(host, cfg, profile);
                if (oldExpiry == "NotFound") { row.Cells[4].Value = "❌ NOT IN AD"; row.DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230); continue; }
                row.Cells[2].Value = oldExpiry;
                if (!isReadOnly) {
                    row.Cells[3].Value = displayTargetDate; row.Cells[4].Value = "⏳ Setting expiry...";
                    bool setSuccess = await LapsClient.SetLapsExpiryAsync(host, fileTime, cfg, profile);
                    if (!setSuccess) { row.Cells[4].Value = "❌ Failed - Set AD Error"; row.DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230); continue; }
                    await Task.Delay(cfg.ValidationDelayMs);
                    row.Cells[4].Value = "⏳ Validating...";
                    string validatedExpiry = await LapsClient.GetLapsExpiryAsync(host, cfg, profile);
                    bool isApplied = !string.Equals(oldExpiry, validatedExpiry, StringComparison.OrdinalIgnoreCase);
                    row.Cells[4].Value = isApplied ? "✅ Success - Validated" : "✅ Success - Pending Apply";
                }
                var newPwd = await LapsClient.GetLapsPasswordAsync(host, cfg, profile);
                if (!string.IsNullOrEmpty(newPwd)) { row.Cells[1].Value = newPwd; if (isReadOnly) row.Cells[4].Value = "✅ Success - ReadOnly"; row.DefaultCellStyle.BackColor = Color.FromArgb(235, 255, 235); }
                else { row.Cells[4].Value = isReadOnly ? "❌ Failed - Not Found" : "⚠️ Expiry set, but password read failed"; row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 200); }
            } catch (Exception ex) { row.Cells[4].Value = $"❌ Failed - {ex.Message}"; row.DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230); }
        }
        IsProcessing = false; _runBtn.Enabled = true; _runBtn.Text = "🚀 LAPS Query"; St("✅ Batch complete.");
    }

    private RunAsProfile? EnsureProfileReady()
    {
        var runAsName = _accountCombo?.SelectedItem?.ToString();
        var profile = ConfigLoader.AppConfig.RunAsProfiles.FirstOrDefault(p => p.Name == runAsName);
        if (profile == null) { MessageBox.Show("Please select a RunAs profile.", "Error"); return null; }
        if (string.IsNullOrEmpty(profile.Password)) { var (ok, pwd) = CredentialPrompt.Show(profile.Username, $"Please enter the password for \"{profile.Name}\" to execute LAPS query:"); if (!ok) return null; return new RunAsProfile { Name = profile.Name, Username = profile.Username, Password = pwd }; }
        return profile;
    }

    private void DownloadTemplate()
    {
        using var sfd = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "PandaLAPS_Template.csv" };
        if (sfd.ShowDialog() == DialogResult.OK) { try { File.WriteAllText(sfd.FileName, "Hostname\n12345PC\n67890LT", Encoding.UTF8); St("✅ Template saved."); } catch (Exception ex) { MessageBox.Show($"Failed to save template: {ex.Message}", "Error"); } }
    }

    private void ImportCsv()
    {
        if (_grid == null) return;
        using var ofd = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv", Title = "Import Hostnames" };
        if (ofd.ShowDialog() == DialogResult.OK) { try { var lines = File.ReadAllLines(ofd.FileName); int added = 0; bool isFirstLine = true; foreach (var line in lines) { var val = line.Trim(); if (string.IsNullOrWhiteSpace(val)) continue; var host = val.Split(',')[0].Trim().Trim('"', '\''); if (isFirstLine && host.Equals("Hostname", StringComparison.OrdinalIgnoreCase)) { isFirstLine = false; continue; } if (!string.IsNullOrWhiteSpace(host)) { _grid.Rows.Add(host, "", "", "", ""); added++; } isFirstLine = false; } St($"✅ Imported {added} hostnames."); } catch (Exception ex) { MessageBox.Show($"Failed to import CSV: {ex.Message}", "Error"); } }
    }

    private void ExportCsv()
    {
        if (_grid == null || _grid.Rows.Count <= 1) return;
        using var sfd = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"PandaLAPS_Batch_{DateTime.Now:yyyyMMdd_HHmm}.csv" };
        if (sfd.ShowDialog() != DialogResult.OK) return;
        try { var sb = new StringBuilder(); sb.AppendLine("Hostname,LAPS_Password,Current_Expiry,New_Expiry,Status"); for (int i = 0; i < _grid.Rows.Count - 1; i++) { var r = _grid.Rows[i]; sb.AppendLine($"\"{r.Cells[0].Value}\",\"{r.Cells[1].Value}\",\"{r.Cells[2].Value}\",\"{r.Cells[3].Value}\",\"{r.Cells[4].Value}\""); } File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8); MessageBox.Show("Export successful!", "PandaLAPS"); } catch (Exception ex) { MessageBox.Show($"Export failed: {ex.Message}", "Error"); }
    }

    private void Grid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V) { try { string[] lines = Clipboard.GetText().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries); foreach (var line in lines) if (!string.IsNullOrWhiteSpace(line.Trim())) _grid?.Rows.Add(line.Trim(), "", "", "", ""); St($"Pasted {lines.Length} hostnames."); } catch { } }
    }
}