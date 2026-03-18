using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class LapsSettingsWindow : Form
{
    private ListBox? _dcList;
    private TextBox? _dcEntry;
    private TextBox? _importBox;
    private TextBox? _cmdletBox;
    private TextBox? _delayBox;
    private Label?   _status;

    private const int FormW = 540;
    private const int GrpL  = 10;
    private const int GrpW  = FormW - GrpL * 2;
    private const int LblW  = 120;
    private const int FldL  = 132;
    private const int FldW  = GrpW - FldL - 12;

    public LapsSettingsWindow()
    {
        Text            = "LAPS Settings";
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        TopMost         = true;
        Icon            = AppIcon.Get();
        BackColor       = Color.White;
        Font            = new Font("Segoe UI", 9f);

        Build();
    }

    private void Build()
    {
        var hdr = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(28, 28, 28) };
        hdr.Controls.Add(new Label
        {
            Text = "🛡️ LAPS Configuration", Left = 12, Top = 8,
            Width = FormW - 20, Height = 20,
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        });
        Controls.Add(hdr);

        var cfg = LapsConfig.Load();

        var grp = new GroupBox
        {
            Text = "LAPS Parameters", Left = GrpL, Top = 46,
            Width = GrpW, Height = 315, Font = new Font("Segoe UI", 9f)
        };

        var y = 22;

        grp.Controls.Add(Lbl("Domain Controllers:", LblW, 8, y));

        const int btnW  = 26;
        var       listW = FldW - btnW - 4;

        _dcList = new ListBox
        {
            Left = FldL, Top = y, Width = listW, Height = 74,
            Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle
        };

        foreach (var dc in Split(cfg.DomainController))
            _dcList.Items.Add(dc);
        if (_dcList.Items.Count > 0) _dcList.SelectedIndex = 0;
        grp.Controls.Add(_dcList);

        var bAdd = Btn("+", FldL + listW + 4, y,      btnW);
        var bDel = Btn("−", FldL + listW + 4, y + 26, btnW);

        bAdd.Click += (_, _) =>
        {
            var v = _dcEntry?.Text.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(v)) return;
            if (!_dcList.Items.Contains(v)) _dcList.Items.Add(v);
            _dcList.SelectedItem = v;
            if (_dcEntry != null) _dcEntry.Clear();
        };

        bDel.Click += (_, _) =>
        {
            if (_dcList.SelectedIndex < 0) return;
            _dcList.Items.RemoveAt(_dcList.SelectedIndex);
            if (_dcList.Items.Count > 0) _dcList.SelectedIndex = 0;
        };

        grp.Controls.AddRange(new Control[] { bAdd, bDel });

        y += 80;

        _dcEntry = new TextBox
        {
            Left = FldL, Top = y, Width = listW,
            PlaceholderText = "Type DC hostname, then click  +",
            Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle
        };

        grp.Controls.Add(_dcEntry);
        grp.Controls.Add(Hint("First entry in list is used for LAPS queries", FldL, y + 24, FldW));

        y += 46;

        grp.Controls.Add(Lbl("Import Command:", LblW, 8, y));
        _importBox = Fld(cfg.ImportCommand, FldL, y, FldW, "e.g. Import-Module LAPS");
        grp.Controls.Add(_importBox);
        grp.Controls.Add(Hint("PowerShell command to import the LAPS module", FldL, y + 22, FldW));

        y += 44;

        grp.Controls.Add(Lbl("Cmdlet Name:", LblW, 8, y));
        _cmdletBox = Fld(cfg.CmdletName, FldL, y, FldW, "e.g. Get-LapsADPassword");
        grp.Controls.Add(_cmdletBox);
        grp.Controls.Add(Hint("Get-LapsADPassword (on-prem)  or  Get-LapsAADPassword (Azure AD)", FldL, y + 22, FldW));

        y += 44;

        //Validation Delay
        grp.Controls.Add(Lbl("Validation Delay (ms):", LblW, 8, y));
        _delayBox = Fld(cfg.ValidationDelayMs.ToString(), FldL, y, FldW, "e.g. 10000");
        grp.Controls.Add(_delayBox);
        grp.Controls.Add(Hint("Time to wait (ms) before validating AD changes", FldL, y + 22, FldW));

        Controls.Add(grp);

        var bRow = 46 + grp.Height + 8;

        var bSave = new Button
        {
            Text = "💾 Save", Left = FormW - 202, Top = bRow, Width = 90, Height = 28,
            BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        
        bSave.FlatAppearance.BorderSize = 0;
        bSave.Click += (_, _) => Save();
        Controls.Add(bSave);

        var bClose = new Button { Text = "Close", Left = FormW - 104, Top = bRow, Width = 90, Height = 28, Font = new Font("Segoe UI", 9f) };
        bClose.Click += (_, _) => Close();
        Controls.Add(bClose);

        _status = new Label { Text = "", Left = GrpL, Top = bRow + 34, Width = GrpW, Height = 20, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 9f) };
        Controls.Add(_status);

        ClientSize = new Size(FormW, bRow + 60);
    }

    private void Save()
    {
        try
        {
            var dcs = new List<string>();
            foreach (var item in _dcList!.Items) dcs.Add(item.ToString()!);

            //Parse the delay box, defaulting to 10000 if input is invalid
            if (!int.TryParse(_delayBox?.Text.Trim(), out int delayMs))
            {
                delayMs = 10000;
            }

            new LapsConfig
            {
                DomainController = string.Join(";", dcs),
                ImportCommand    = _importBox?.Text.Trim() ?? "",
                CmdletName       = _cmdletBox?.Text.Trim() ?? "",
                ValidationDelayMs = delayMs
            }.Save();

            St("✅ Settings saved.");
        }
        catch (Exception ex) { St($"❌ Save failed: {ex.Message}"); }
    }

    private void St(string msg)
    {
        if (_status == null) return;
        _status.Text      = msg;
        _status.ForeColor = msg.StartsWith("❌") ? Color.DarkRed
                          : msg.StartsWith("✅") ? Color.DarkGreen
                          : Color.DimGray;
    }

    private static string[] Split(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split(';', StringSplitOptions.RemoveEmptyEntries);

    private static Label   Lbl(string t, int w, int x, int y) => new() { Text = t, Left = x, Top = y + 3, Width = w, Height = 20, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f) };
    private static TextBox Fld(string t, int x, int y, int w, string ph) => new() { Text = t, Left = x, Top = y, Width = w, PlaceholderText = ph, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };
    private static Label   Hint(string t, int x, int y, int w) => new() { Text = t, Left = x, Top = y, Width = w, Height = 14, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f) };
    private static Button  Btn(string t, int x, int y, int w)  => new() { Text = t, Left = x, Top = y, Width = w, Height = 22, Font = new Font("Segoe UI", 9f), Padding = new Padding(0) };
}