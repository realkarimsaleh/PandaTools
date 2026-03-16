using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

public class FlavourEditorWindow : Form
{
    //######################################
    //Singleton Instance Tracker
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

    private TreeView _tree = null!;
    private PropertyGrid _grid = null!;
    private FlavourConfig _flavour = null!;

    private FlavourEditorWindow()
    {
        Text = "PandaTools - Menu Editor";
        Size = new Size(850, 650);
        StartPosition = FormStartPosition.CenterParent;
        Icon = AppIcon.Get();
        BackColor = Color.FromArgb(245, 245, 245);
        MinimumSize = new Size(600, 400);

        LoadData();
        BuildLayout();
    }

    private void LoadData()
    {
        if (File.Exists(ConfigLoader.LocalFlavourPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigLoader.LocalFlavourPath);
                _flavour = JsonSerializer.Deserialize<FlavourConfig>(json, ConfigLoader.JsonOpts) ?? new FlavourConfig();
            }
            catch { _flavour = new FlavourConfig(); }
        }
        else
        {
            _flavour = new FlavourConfig { Version = "1.0", Menu = new() };
        }
        
        if (_flavour.Menu == null) _flavour.Menu = new();
    }

    private void BuildLayout()
    {
        //######################################
        //HEADER PANEL (Matches PandaPassGen/Settings)
        //######################################
        var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(28, 28, 28) };
        header.Controls.Add(new Label 
        { 
            Text = $"✏️ Menu Editor - {Environment.UserName}'s Local Flavour", 
            Left = 20, Top = 15, Width = 500, Height = 30, 
            ForeColor = Color.White, Font = new Font("Segoe UI", 14f, FontStyle.Bold) 
        });
        Controls.Add(header);

        //######################################
        //FOOTER PANEL (Modern Flat Buttons)
        //######################################
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Color.White };
        
        //Top border line for footer
        footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(220, 220, 220) });

        var btnAddFolder = MakeButton("📁 Add Folder", 20, 18, 120);
        var btnAddItem   = MakeButton("🔗 Add Item", 150, 18, 120);
        var btnDelete    = MakeButton("🗑️ Delete", 280, 18, 100);
        
        var btnSave = MakeButton("💾 Save & Apply", 0, 18, 160);
        btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSave.Left = ClientSize.Width - 180;
        btnSave.BackColor = Color.FromArgb(40, 167, 69);
        btnSave.ForeColor = Color.White;
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

        btnAddFolder.Click += BtnAddFolder_Click;
        btnAddItem.Click   += BtnAddItem_Click;
        btnDelete.Click    += BtnDelete_Click;
        btnSave.Click      += BtnSave_Click;

        footer.Controls.AddRange(new Control[] { btnAddFolder, btnAddItem, btnDelete, btnSave });
        Controls.Add(footer);
        
        //######################################
        //SPLIT CONTAINER
        //######################################
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 280,
            FixedPanel = FixedPanel.Panel1,
            BackColor = Color.FromArgb(220, 220, 220), // Border color
            Padding = new Padding(10) // Padding around the split container
        };
        split.Panel1.BackColor = Color.White;
        split.Panel2.BackColor = Color.White;

        //######################################
        //TREE VIEW (Left)
        //######################################
        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10f),
            HideSelection = false, 
            FullRowSelect = true,
            ShowLines = false,
            ItemHeight = 28 // Taller, cleaner rows
        };
        _tree.AfterSelect += Tree_AfterSelect;
        split.Panel1.Controls.Add(_tree);

        //######################################
        //PROPERTY GRID (Right)
        //######################################
        _grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            ToolbarVisible = false,
            PropertySort = PropertySort.Categorized,
            LineColor = Color.FromArgb(240, 240, 240),
            HelpBackColor = Color.White,
            HelpBorderColor = Color.FromArgb(220, 220, 220)
        };
        
        _grid.PropertyValueChanged += Grid_PropertyValueChanged;
        split.Panel2.Controls.Add(_grid);

        Controls.Add(split);
        split.BringToFront();

        PopulateTree();
    }

    private Button MakeButton(string text, int x, int y, int width)
    {
        var btn = new Button
        {
            Text = text, Left = x, Top = y, Width = width, Height = 35,
            Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(245, 245, 245)
        };
        btn.FlatAppearance.BorderColor = Color.Silver;
        return btn;
    }

    private void PopulateTree()
    {
        _tree.Nodes.Clear();

        foreach (var sec in _flavour.Menu)
        {
            var secName = string.IsNullOrWhiteSpace(sec.Icon) ? sec.Section : $"{sec.Icon} {sec.Section}";
            var secNode = new TreeNode(secName) { Tag = sec };

            foreach (var item in sec.Items)
            {
                var itemNode = new TreeNode(item.Label) { Tag = item };
                secNode.Nodes.Add(itemNode);
            }

            _tree.Nodes.Add(secNode);
        }
        _tree.ExpandAll();
    }

    private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        _grid.SelectedObject = e.Node?.Tag;
    }

    private void Grid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
    {
        var node = _tree.SelectedNode;
        if (node == null) return;

        if (node.Tag is FlavourSection sec)
            node.Text = string.IsNullOrWhiteSpace(sec.Icon) ? sec.Section : $"{sec.Icon} {sec.Section}";
        else if (node.Tag is FlavourItem item)
            node.Text = item.Label;
    }

    private void BtnAddFolder_Click(object? sender, EventArgs e)
    {
        var newFolder = new FlavourSection { Section = "New Folder", Icon = "📂" };
        _flavour.Menu.Add(newFolder);
        
        var node = new TreeNode($"{newFolder.Icon} {newFolder.Section}") { Tag = newFolder };
        _tree.Nodes.Add(node);
        _tree.SelectedNode = node;
    }

    private void BtnAddItem_Click(object? sender, EventArgs e)
    {
        var node = _tree.SelectedNode;
        if (node == null)
        {
            MessageBox.Show("Please select a Folder first to add an item into it.", "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var parentNode = node.Tag is FlavourSection ? node : node.Parent;
        if (parentNode == null || !(parentNode.Tag is FlavourSection folder)) return;

        var newItem = new FlavourItem { Label = "New Item", Type = "url" };
        folder.Items.Add(newItem);

        var itemNode = new TreeNode(newItem.Label) { Tag = newItem };
        parentNode.Nodes.Add(itemNode);
        
        parentNode.Expand();
        _tree.SelectedNode = itemNode;
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var node = _tree.SelectedNode;
        if (node == null) return;

        if (MessageBox.Show($"Delete '{node.Text}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        if (node.Tag is FlavourSection folder)
        {
            _flavour.Menu.Remove(folder);
        }
        else if (node.Tag is FlavourItem item)
        {
            if (node.Parent?.Tag is FlavourSection parentFolder)
                parentFolder.Items.Remove(item);
        }

        _tree.Nodes.Remove(node);
        _grid.SelectedObject = null;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        try
        {
            var json = JsonSerializer.Serialize(_flavour, ConfigLoader.JsonOpts);
            File.WriteAllText(ConfigLoader.LocalFlavourPath, json);
            
            ConfigLoader.Reload(); 
            //Close cleanly on success
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save menu:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}