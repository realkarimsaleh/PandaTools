using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;

partial class SettingsWindow : Form
{
    private AppConfig _config;
    private ComboBox _flavourCombo;
    private TextBox _urlBox;
    private TextBox _keyBox;
    private TextBox _tokenBox;
    private CheckBox _diagCheck;
    private NumericUpDown _pollBox;
    private Button _updateFlavourBtn;
    private Button _updateAppBtn;
    private Button _loadLocalBtn;
    private Label _statusLabel;

    public SettingsWindow()
    {
        _config = ConfigLoader.AppConfig;
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        Text           = "PandaTools Settings";
        Size           = new(500, 400);
        StartPosition  = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox    = false;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 12,
            ColumnCount = 2
        };

        // GitLab Server
        mainPanel.Controls.Add(new Label { Text = "GitLab Server:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
        _urlBox = new TextBox { Text = _config.UrlServer, Dock = DockStyle.Fill };
        mainPanel.Controls.Add(_urlBox, 1, 0);

        // Flavour selector
        mainPanel.Controls.Add(new Label { Text = "Flavour:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
        _flavourCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        mainPanel.Controls.Add(_flavourCombo, 1, 1);

        // Key file
        mainPanel.Controls.Add(new Label { Text = "Key File:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
        _keyBox = new TextBox { Text = _config.KeyFile, Dock = DockStyle.Fill };
        mainPanel.Controls.Add(_keyBox, 1, 2);

        // Token file
        mainPanel.Controls.Add(new Label { Text = "Token File:", TextAlign = ContentAlignment.MiddleRight }, 0, 3);
        _tokenBox = new TextBox { Text = _config.TokenFile, Dock = DockStyle.Fill };
        mainPanel.Controls.Add(_tokenBox, 1, 3);

        // Poll interval
        mainPanel.Controls.Add(new Label { Text = "Poll Seconds:", TextAlign = ContentAlignment.MiddleRight }, 0, 4);
        _pollBox = new NumericUpDown { Minimum = 30, Maximum = 3600, Value = _config.FlavourPollSeconds, Dock = DockStyle.Fill };
        mainPanel.Controls.Add(_pollBox, 1, 4);

        // Diagnostics
        _diagCheck = new CheckBox { Text = "Enable Diagnostics", Checked = _config.Diagnostics, Dock = DockStyle.Fill, AutoSize = true };
        mainPanel.Controls.Add(new Label(), 0, 5);
        mainPanel.Controls.Add(_diagCheck, 1, 5);

        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Buttons
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50 };
        _updateFlavourBtn = new Button { Text = "🔄 Check Flavour Updates", Width = 150 };
        _updateAppBtn    = new Button { Text = "⬆️ Check App Updates", Width = 150 };
        _loadLocalBtn    = new Button { Text = "📁 Load Local Flavour", Width = 150 };
        var saveBtn      = new Button { Text = "💾 Save & Reload", Width = 120, DialogResult = DialogResult.OK };

        _updateFlavourBtn.Click += (_, _) => CheckFlavourUpdates();
        _updateAppBtn.Click    += (_, _) => _ = Updater.CheckAsync(false);
        _loadLocalBtn.Click    += (_, _) => LoadLocalFlavour();
        saveBtn.Click          += (_, _) => SaveSettings();

        btnPanel.Controls.AddRange(new Control[] { _updateFlavourBtn, _updateAppBtn, _loadLocalBtn, saveBtn });

        Controls.Add(mainPanel);
        Controls.Add(btnPanel);

        _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 20, Text = "Ready", ForeColor = System.Drawing.Color.Blue };
        Controls.Add(_statusLabel);
    }

    private void LoadSettings()
    {
        _flavourCombo.Items.Clear();
        foreach (var name in ConfigLoader.GetAvailableFlavours())
            _flavourCombo.Items.Add(name);
        _flavourCombo.SelectedItem = _config.Flavour;
        Status("Settings loaded.");
    }

    private void SaveSettings()
    {
        _config.UrlServer          = _urlBox.Text;
        _config.Flavour            = (string)_flavourCombo.SelectedItem!;
        _config.KeyFile            = _keyBox.Text;
        _config.TokenFile          = _tokenBox.Text;
        _config.Diagnostics        = _diagCheck.Checked;
        _config.FlavourPollSeconds = (int)_pollBox.Value;

        File.WriteAllText(ConfigLoader.ConfigPath,
            System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            }));
        ConfigLoader.Reload();
        Status("Settings saved and reloaded.");
    }

    private void CheckFlavourUpdates()
    {
        Status("Checking for flavour updates...");
        _ = Task.Run(async () =>
        {
            await ConfigLoader.CheckFlavourUpdateAsync();
            Invoke(() => Status("Flavour check complete."));
        });
    }

    private void LoadLocalFlavour()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title  = "Select flavour.json"
        };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var name = Path.GetFileNameWithoutExtension(ofd.FileName);
            var path = Path.Combine(ConfigLoader.FlavourDir, $"{name}.json");
            File.Copy(ofd.FileName, path, true);
            _flavourCombo.Items.Add(name);
            _flavourCombo.SelectedItem = name;
            Status($"Loaded {name}.json");
        }
    }

    private void Status(string msg)
    {
        _statusLabel.Text = msg;
        _statusLabel.ForeColor = msg.Contains("Error") ? System.Drawing.Color.Red : System.Drawing.Color.Blue;
    }
}
