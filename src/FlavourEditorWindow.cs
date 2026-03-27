using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

public class FlavourEditorWindow : Form
{
    //######################################
    //Singleton
    //######################################
    private static FlavourEditorWindow? _instance;

    public static void ShowWindow()
    {
        if (_instance == null || _instance.IsDisposed)
        {
            _instance = new FlavourEditorWindow();
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
    //State
    //######################################
    private TreeView      _tree    = null!;
    private Panel         _detail  = null!;
    private FlavourConfig _flavour = null!;

    //######################################
    //Palette - matches app exactly
    //######################################
    private static readonly Color ColHeader  = Color.FromArgb(28,  28,  28);
    private static readonly Color ColBg      = Color.FromArgb(245, 245, 245);
    private static readonly Color ColSurface = Color.White;
    private static readonly Color ColBorder  = Color.FromArgb(220, 220, 220);
    private static readonly Color ColMuted   = Color.FromArgb(100, 100, 100);
    private static readonly Color ColSave    = Color.FromArgb(40,  167, 69);
    private static readonly Font  FontUI     = new Font("Segoe UI", 9f);
    private static readonly Font  FontBold   = new Font("Segoe UI", 9f,  FontStyle.Bold);
    private static readonly Font  FontSm     = new Font("Segoe UI", 8f);
    private const int LblW = 116;
    private const int FldL = 130;
    private const int PadL = 14;

    //######################################
    //Constructor
    //######################################
    private FlavourEditorWindow()
    {
        Text            = "PandaTools - Menu Editor";
        ClientSize      = new Size(860, 600);
        MinimumSize     = new Size(680, 460);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        Icon            = AppIcon.Get();
        BackColor       = ColBg;
        Font            = FontUI;

        LoadData();
        BuildLayout();
    }

    private void LoadData()
    {
        try
        {
            _flavour = File.Exists(ConfigLoader.LocalFlavourPath)
                ? JsonSerializer.Deserialize<FlavourConfig>(File.ReadAllText(ConfigLoader.LocalFlavourPath), ConfigLoader.JsonOpts) ?? new FlavourConfig()
                : new FlavourConfig { Version = "1.0", Menu = new() };
        }
        catch { _flavour = new FlavourConfig { Version = "1.0", Menu = new() }; }
        _flavour.Menu ??= new();
    }

    //######################################
    //Shell layout
    //######################################
    private void BuildLayout()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = ColHeader };
        header.Controls.Add(new Label
        {
            Text = $"✏️  Menu Editor  -  {Environment.UserName}'s Local Flavour",
            Left = 20, Top = 14, Width = 600, Height = 32,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = false
        });
        Controls.Add(header);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = ColSurface };
        footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColBorder });

        var btnAddFolder = MakeBtn("📁  Add Folder",  20,  14, 118);
        var btnAddItem   = MakeBtn("🔗  Add Item",    146, 14, 108);
        var btnDelete    = MakeBtn("🗑️  Delete",      262, 14, 88);
        var btnSave = new Button
        {
            Text = "💾  Save && Apply", Width = 148, Height = 30, Top = 13,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = ColSave, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.Left = footer.Width - btnSave.Width - 18;
        btnSave.FlatAppearance.BorderSize = 0;

        btnAddFolder.Click += BtnAddFolder_Click;
        btnAddItem.Click   += BtnAddItem_Click;
        btnDelete.Click    += BtnDelete_Click;
        btnSave.Click      += BtnSave_Click;
        footer.Controls.AddRange(new Control[] { btnAddFolder, btnAddItem, btnDelete, btnSave });
        Controls.Add(footer);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 240,
            FixedPanel = FixedPanel.Panel1,
            BackColor = ColBorder,
            Padding = new Padding(1)
        };
        split.Panel1.BackColor = ColSurface;
        split.Panel2.BackColor = ColBg;

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f),
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = false,
            ItemHeight = 26,
            BackColor = ColSurface,
            Indent = 16
        };
        _tree.AfterSelect += OnTreeSelect;
        split.Panel1.Controls.Add(_tree);

        _detail = new Panel { Dock = DockStyle.Fill, BackColor = ColBg, AutoScroll = true, Padding = new Padding(12) };
        split.Panel2.Controls.Add(_detail);

        Controls.Add(split);
        split.BringToFront();
        PopulateTree();
        RenderPlaceholder();
    }

    //######################################
    //Tree
    //######################################
    private void PopulateTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var sec in _flavour.Menu)
        {
            var sn = new TreeNode(SectionLabel(sec)) { Tag = sec };
            foreach (var item in sec.Items)
                sn.Nodes.Add(new TreeNode(item.Label) { Tag = item });
            _tree.Nodes.Add(sn);
        }
        _tree.ExpandAll();
        _tree.EndUpdate();
    }

    private static string SectionLabel(FlavourSection s) =>
        string.IsNullOrWhiteSpace(s.Icon) ? s.Section : $"{s.Icon}  {s.Section}";

    private void OnTreeSelect(object? sender, TreeViewEventArgs e)
    {
        if      (e.Node?.Tag is FlavourSection sec)  RenderSection(sec);
        else if (e.Node?.Tag is FlavourItem    item) RenderItem(item);
        else RenderPlaceholder();
    }

    //######################################
    //Placeholder
    //######################################
    private void RenderPlaceholder()
    {
        _detail.Controls.Clear();
        _detail.Controls.Add(new Label
        {
            Text = "Select a folder or item from the tree to edit it.",
            ForeColor = ColMuted, Font = FontUI,
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
        });
    }

    //######################################
    //Section card
    //######################################
    private void RenderSection(FlavourSection sec)
    {
        _detail.Controls.Clear();
        int y = 0;
        var card = Card("📁  Folder", ref y);

        var fName = Field(card, "Section Name", sec.Section, ref y);
        var fIcon = Field(card, "Icon",          sec.Icon,    ref y, "Paste an emoji  e.g. 🌐  📜  🖥️");

        fName.TextChanged += (_, _) => { sec.Section = fName.Text; UpdateNode(sec); };
        fIcon.TextChanged += (_, _) => { sec.Icon    = fIcon.Text; UpdateNode(sec); };

        SizeCard(card, y);
        _detail.Controls.Add(card);
    }

    //######################################
    //Item card - clears and rebuilds contextual fields when Type changes
    //######################################
    private void RenderItem(FlavourItem item)
    {
        _detail.Controls.Clear();
        int y = 0;
        var card = Card("🔗  Item", ref y);

        var fLabel = Field(card, "Label", item.Label, ref y);
        fLabel.TextChanged += (_, _) =>
        {
            item.Label = fLabel.Text;
            if (_tree.SelectedNode != null) _tree.SelectedNode.Text = item.Label;
        };

        var cboType = Combo(card, "Type", ref y,
            new[] { "url","incognito","app","explorer","runas","powershell","script","pandashell","pandapassgen","pandalaps" },
            item.Type);

        //Divider between fixed and contextual fields
        var divider = new Panel
        {
            Left = PadL, Top = y, Height = 1,
            Width = card.Width - PadL * 2,
            BackColor = ColBorder,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        card.Controls.Add(divider);
        y += 10;

        //Contextual sub-panel
        var ctx = new Panel
        {
            Left = 0, Top = y, Width = card.Width,
            AutoSize = false, BackColor = Color.Transparent,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        card.Controls.Add(ctx);

        void BuildCtx(string type)
        {
            ctx.Controls.Clear();
            int cy = 4;

            switch (type)
            {
                case "url":
                case "incognito":
                    CtxField(ctx, "Value",   item.Value, ref cy, "Single URL - or leave blank and use Values below",
                        v => item.Value = v);
                    CtxMulti(ctx, "Values",  string.Join(Environment.NewLine, item.Values ?? new()), ref cy, "One URL per line - opens all at once",
                        v => item.Values = ParseLines(v));
                    CtxRunAs(ctx, "RunAs Profile", item.RunAsProfile, ref cy,
                        v => item.RunAsProfile = v);
                    break;

                case "app":
                    CtxField(ctx, "Value",     item.Value,     ref cy, "Full path to .exe or .lnk", v => item.Value     = v);
                    CtxField(ctx, "Arguments", item.Arguments, ref cy, null,                         v => item.Arguments = v);
                    CtxCheck(ctx, "Require Admin (UAC)", item.Admin, ref cy, v => item.Admin = v);
                    break;

                case "runas":
                    CtxField(ctx, "Value",     item.Value,     ref cy, "Full path to .exe or .lnk", v => item.Value     = v);
                    CtxField(ctx, "Arguments", item.Arguments, ref cy, null,                         v => item.Arguments = v);
                    CtxRunAs(ctx, "RunAs Profile", item.RunAsProfile, ref cy, v => item.RunAsProfile = v);
                    break;

                case "explorer":
                    CtxField(ctx, "Value", item.Value, ref cy, "Folder or file path", v => item.Value = v);
                    break;

                case "powershell":
                    CtxField(ctx, "Value", item.Value, ref cy, "Inline PowerShell command", v => item.Value = v);
                    break;

                case "script":
                    CtxNumeric(ctx, "Project ID", item.ProjectId, ref cy, "GitLab project ID", v => item.ProjectId = v);
                    CtxField(ctx, "File Path", item.FilePath, ref cy, "e.g. Scripts/MyScript.ps1", v => item.FilePath = v);
                    CtxField(ctx, "Branch",    item.Branch,   ref cy, null,                         v => item.Branch   = v);
                    break;

                default:
                    CtxInfo(ctx, $"No additional settings for '{type}'.", ref cy);
                    break;
            }

            ctx.Height = cy + 4;
            SizeCard(card, y + ctx.Height);
        }

        cboType.SelectedIndexChanged += (_, _) =>
        {
            item.Type = cboType.SelectedItem?.ToString() ?? "url";
            BuildCtx(item.Type);
        };

        BuildCtx(item.Type);
        _detail.Controls.Add(card);
    }

    //######################################
    //Card helpers
    //######################################
    private Panel Card(string title, ref int y)
    {
        var card = new Panel
        {
            Left = 0, Top = 0,
            Width = _detail.ClientSize.Width - _detail.Padding.Horizontal,
            AutoSize = false, BackColor = ColSurface,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = ColBg };
        bar.Controls.Add(new Label { Text = title, Left = PadL, Top = 9, AutoSize = true, Font = FontBold, ForeColor = ColMuted });
        bar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ColBorder });
        card.Controls.Add(bar);

        card.Paint += (_, pe) =>
        {
            using var pen = new Pen(ColBorder);
            pe.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        y = 46;
        return card;
    }

    private static void SizeCard(Panel card, int contentBottom) =>
        card.Height = contentBottom + 14;

    //######################################
    //Card-level field builders (used inside the card at top level)
    //######################################
    private static TextBox Field(Panel parent, string label, string value, ref int y, string? hint = null)
    {
        parent.Controls.Add(new Label
        {
            Text = label + ":", Left = PadL, Top = y + 4,
            Width = LblW, Height = 18, Font = FontBold, ForeColor = ColMuted,
            TextAlign = ContentAlignment.MiddleRight, AutoSize = false
        });
        var txt = new TextBox
        {
            Text = value ?? "", Left = FldL, Top = y,
            Width = parent.Width - FldL - PadL, Height = 24,
            Font = FontUI, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        parent.Controls.Add(txt);
        y += 28;
        if (hint != null)
        {
            parent.Controls.Add(new Label
            {
                Text = hint, Left = FldL, Top = y,
                Width = parent.Width - FldL - PadL, Height = 14,
                Font = FontSm, ForeColor = ColMuted, AutoSize = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            });
            y += 16;
        }
        y += 8;
        return txt;
    }

    private static ComboBox Combo(Panel parent, string label, ref int y, string[] items, string selected)
    {
        parent.Controls.Add(new Label
        {
            Text = label + ":", Left = PadL, Top = y + 4,
            Width = LblW, Height = 18, Font = FontBold, ForeColor = ColMuted,
            TextAlign = ContentAlignment.MiddleRight, AutoSize = false
        });
        var cbo = new ComboBox
        {
            Left = FldL, Top = y, Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList, Font = FontUI
        };
        foreach (var i in items) cbo.Items.Add(i);
        var idx = Array.IndexOf(items, selected);
        cbo.SelectedIndex = idx >= 0 ? idx : 0;
        parent.Controls.Add(cbo);
        y += 36;
        return cbo;
    }

    //######################################
    //Contextual field builders (used inside ctx sub-panel)
    //######################################
    private static void CtxField(Panel ctx, string label, string value, ref int cy, string? hint, Action<string> onChange)
    {
        ctx.Controls.Add(new Label
        {
            Text = label + ":", Left = PadL, Top = cy + 4,
            Width = LblW, Height = 18, Font = FontBold, ForeColor = ColMuted,
            TextAlign = ContentAlignment.MiddleRight, AutoSize = false
        });
        var txt = new TextBox
        {
            Text = value ?? "", Left = FldL, Top = cy,
            Width = ctx.Width - FldL - PadL, Height = 24,
            Font = FontUI, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        txt.TextChanged += (_, _) => onChange(txt.Text);
        ctx.Controls.Add(txt);
        cy += 28;
        if (hint != null)
        {
            ctx.Controls.Add(new Label
            {
                Text = hint, Left = FldL, Top = cy,
                Width = ctx.Width - FldL - PadL, Height = 14,
                Font = FontSm, ForeColor = ColMuted, AutoSize = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            });
            cy += 16;
        }
        cy += 8;
    }

    private static void CtxMulti(Panel ctx, string label, string value, ref int cy, string? hint, Action<string> onChange)
    {
        ctx.Controls.Add(new Label
        {
            Text = label + ":", Left = PadL, Top = cy + 4,
            Width = LblW, Height = 18, Font = FontBold, ForeColor = ColMuted,
            TextAlign = ContentAlignment.MiddleRight, AutoSize = false
        });
        var txt = new TextBox
        {
            Text = value ?? "", Left = FldL, Top = cy,
            Width = ctx.Width - FldL - PadL, Height = 70,
            Font = FontUI, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White,
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        txt.TextChanged += (_, _) => onChange(txt.Text);
        ctx.Controls.Add(txt);
        cy += 76;
        if (hint != null)
        {
            ctx.Controls.Add(new Label
            {
                Text = hint, Left = FldL, Top = cy,
                Width = ctx.Width - FldL - PadL, Height = 14,
                Font = FontSm, ForeColor = ColMuted, AutoSize = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            });
            cy += 16;
        }
        cy += 8;
    }

    private void CtxRunAs(Panel ctx, string label, string selected, ref int cy, Action<string> onChange)
    {
        var profiles = new List<string> { "" };
        profiles.AddRange(ConfigLoader.AppConfig.RunAsProfiles.Select(p => p.Name));
        var arr = profiles.ToArray();

        ctx.Controls.Add(new Label
        {
            Text = label + ":", Left = PadL, Top = cy + 4,
            Width = LblW, Height = 18, Font = FontBold, ForeColor = ColMuted,
            TextAlign = ContentAlignment.MiddleRight, AutoSize = false
        });
        var cbo = new ComboBox
        {
            Left = FldL, Top = cy, Width = ctx.Width - FldL - PadL,
            DropDownStyle = ComboBoxStyle.DropDownList, Font = FontUI,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        foreach (var p in arr) cbo.Items.Add(p);
        var idx = Array.IndexOf(arr, selected ?? "");
        cbo.SelectedIndex = idx >= 0 ? idx : 0;
        cbo.SelectedIndexChanged += (_, _) => onChange(cbo.SelectedItem?.ToString() ?? "");
        ctx.Controls.Add(cbo);
        cy += 36;
    }

    private static void CtxCheck(Panel ctx, string label, bool value, ref int cy, Action<bool> onChange)
    {
        var chk = new CheckBox
        {
            Text = label, Left = FldL, Top = cy,
            Width = ctx.Width - FldL - PadL, Height = 22,
            Font = FontUI, Checked = value,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        chk.CheckedChanged += (_, _) => onChange(chk.Checked);
        ctx.Controls.Add(chk);
        cy += 30;
    }

    private static void CtxNumeric(Panel ctx, string label, int value, ref int cy, string? hint, Action<int> onChange)
    {
        ctx.Controls.Add(new Label
        {
            Text = label + ":", Left = PadL, Top = cy + 4,
            Width = LblW, Height = 18, Font = FontBold, ForeColor = ColMuted,
            TextAlign = ContentAlignment.MiddleRight, AutoSize = false
        });
        var num = new NumericUpDown
        {
            Left = FldL, Top = cy, Width = 140, Height = 24,
            Minimum = 0, Maximum = 999999, Value = Math.Max(0, value), Font = FontUI
        };
        num.ValueChanged += (_, _) => onChange((int)num.Value);
        ctx.Controls.Add(num);
        cy += 28;
        if (hint != null)
        {
            ctx.Controls.Add(new Label
            {
                Text = hint, Left = FldL, Top = cy,
                Width = ctx.Width - FldL - PadL, Height = 14,
                Font = FontSm, ForeColor = ColMuted, AutoSize = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            });
            cy += 16;
        }
        cy += 8;
    }

    private static void CtxInfo(Panel ctx, string text, ref int cy)
    {
        ctx.Controls.Add(new Label
        {
            Text = text, Left = FldL, Top = cy,
            Width = ctx.Width - FldL - PadL, Height = 22,
            Font = FontUI, ForeColor = ColMuted, AutoSize = false,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        });
        cy += 28;
    }

    //######################################
    //Helpers
    //######################################
    private void UpdateNode(FlavourSection sec)
    {
        if (_tree.SelectedNode != null) _tree.SelectedNode.Text = SectionLabel(sec);
    }

    private static List<string> ParseLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    private static Button MakeBtn(string text, int x, int y, int w)
    {
        var btn = new Button
        {
            Text = text, Left = x, Top = y, Width = w, Height = 28,
            Font = FontUI, Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(245, 245, 245)
        };
        btn.FlatAppearance.BorderColor = Color.Silver;
        return btn;
    }

    //######################################
    //Button handlers
    //######################################
    private void BtnAddFolder_Click(object? sender, EventArgs e)
    {
        var sec  = new FlavourSection { Section = "New Folder", Icon = "📂" };
        _flavour.Menu.Add(sec);
        var node = new TreeNode(SectionLabel(sec)) { Tag = sec };
        _tree.Nodes.Add(node);
        _tree.SelectedNode = node;
    }

    private void BtnAddItem_Click(object? sender, EventArgs e)
    {
        var node = _tree.SelectedNode;
        if (node == null)
        {
            MessageBox.Show("Select a folder first.", "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var parentNode = node.Tag is FlavourSection ? node : node.Parent;
        if (parentNode?.Tag is not FlavourSection folder) return;

        var item     = new FlavourItem { Label = "New Item", Type = "url" };
        folder.Items.Add(item);
        var itemNode = new TreeNode(item.Label) { Tag = item };
        parentNode.Nodes.Add(itemNode);
        parentNode.Expand();
        _tree.SelectedNode = itemNode;
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var node = _tree.SelectedNode;
        if (node == null) return;
        if (MessageBox.Show($"Delete '{node.Text}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        if      (node.Tag is FlavourSection sec && node.Parent == null)              _flavour.Menu.Remove(sec);
        else if (node.Tag is FlavourItem item && node.Parent?.Tag is FlavourSection p) p.Items.Remove(item);

        _tree.Nodes.Remove(node);
        RenderPlaceholder();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        try
        {
            File.WriteAllText(ConfigLoader.LocalFlavourPath, JsonSerializer.Serialize(_flavour, ConfigLoader.JsonOpts));
            ConfigLoader.Reload();
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
