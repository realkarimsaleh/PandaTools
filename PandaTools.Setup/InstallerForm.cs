using System.Runtime.Versioning;
using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;

[SupportedOSPlatform("windows")]
public class InstallerForm : Form
{
    private const string AppName   = "PandaTools";
    private const string Publisher = "Karim Saleh";
    private const string RegKey    = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PandaTools";

    private static readonly string AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    //######################################
    //Update detection
    //If PandaTools is already registered in Add/Remove Programs this is an update.
    //Update mode locks the install path, skips provision entirely, and changes the UI.
    //######################################
    private static readonly string? ExistingInstallDir = DetectExistingInstall();

    private static string? DetectExistingInstall()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegKey);
        return key?.GetValue("InstallLocation") as string is { Length: > 0 } v ? v : null;
    }

    private bool IsUpdate => ExistingInstallDir != null;

    private TextBox?     _txtPath;
    private TextBox?     _txtProvision;
    private CheckBox?    _chkDesktop, _chkStartMenu, _chkStartup;
    private Button?      _btnInstall, _btnBrowse, _btnBrowseProvision, _btnCancel;
    private ProgressBar? _progress;
    private Label?       _lblStatus, _lblProvisionNote;
    private Panel?       _pnlProvision;

    public InstallerForm() => BuildUI();

    void BuildUI()
    {
        var isUpdate = IsUpdate;

        Text            = isUpdate ? $"{AppName} Update" : $"{AppName} Setup";
        Size            = new Size(520, isUpdate ? 370 : 430);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = Color.White;
        Font            = new Font("Segoe UI", 9f);

        //######################################
        //Header
        //######################################
        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(24, 24, 24) };
        header.Controls.Add(new Label
        {
            Text      = $"🐼  {AppName}", ForeColor = Color.White,
            Font      = new Font("Segoe UI", 15f, FontStyle.Bold),
            Bounds    = new Rectangle(18, 12, 380, 32), AutoSize = false
        });
        header.Controls.Add(new Label
        {
            Text      = isUpdate
                ? $"Updating to v{AppVersion}   ·   {Publisher}"
                : $"Version {AppVersion}   ·   {Publisher}",
            ForeColor = Color.FromArgb(160, 160, 160),
            Font      = new Font("Segoe UI", 8.5f),
            Bounds    = new Rectangle(20, 46, 400, 18), AutoSize = false
        });

        //######################################
        //Install path
        //In update mode the path is read from the registry and locked.
        //######################################
        var lblPath = new Label { Text = "Install location:", Bounds = new Rectangle(20, 90, 200, 20) };
        _txtPath = new TextBox
        {
            Text     = isUpdate ? ExistingInstallDir! : $@"C:\Program Files\{AppName}",
            Bounds   = new Rectangle(20, 110, 382, 24),
            ReadOnly = isUpdate,
            BackColor = isUpdate ? Color.FromArgb(240, 240, 240) : Color.White
        };

        _btnBrowse = new Button
        {
            Text    = "Browse",
            Bounds  = new Rectangle(408, 109, 84, 26),
            Enabled = !isUpdate
        };
        _btnBrowse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog
            {
                Description            = "Select install folder",
                SelectedPath           = _txtPath.Text,
                UseDescriptionForTitle = true
            };
            if (d.ShowDialog() == DialogResult.OK) _txtPath.Text = d.SelectedPath;
        };

        //######################################
        //Options
        //######################################
        _chkDesktop   = new CheckBox { Text = "Create Desktop shortcut",           Bounds = new Rectangle(20, 150, 300, 22), Checked = true  };
        _chkStartMenu = new CheckBox { Text = "Create Start Menu shortcut",        Bounds = new Rectangle(20, 172, 300, 22), Checked = true  };
        _chkStartup   = new CheckBox { Text = "Run PandaTools at Windows startup", Bounds = new Rectangle(20, 194, 300, 22), Checked = false };

        //######################################
        //Provision panel — fresh install only
        //Hidden entirely during updates. No provision is ever applied on update
        //because config.json already exists from the original install.
        //######################################
        _pnlProvision = new Panel
        {
            Bounds  = new Rectangle(0, 222, 510, 62),
            Visible = !isUpdate
        };

        var autoDetected = FindProvisionFile();

        _pnlProvision.Controls.Add(new Label
        {
            Text   = "Provision file:",
            Bounds = new Rectangle(20, 4, 100, 20)
        });

        _txtProvision = new TextBox
        {
            Text        = autoDetected ?? "",
            PlaceholderText = "Optional — leave blank to configure on first launch",
            Bounds      = new Rectangle(20, 22, 356, 24)
        };
        _pnlProvision.Controls.Add(_txtProvision);

        _btnBrowseProvision = new Button { Text = "Browse", Bounds = new Rectangle(382, 21, 84, 26) };
        _btnBrowseProvision.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title  = "Select provision.json"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
                _txtProvision.Text = ofd.FileName;
        };
        _pnlProvision.Controls.Add(_btnBrowseProvision);

        _lblProvisionNote = new Label
        {
            Bounds    = new Rectangle(20, 48, 460, 16),
            Font      = new Font("Segoe UI", 7.5f),
            ForeColor = autoDetected != null ? Color.FromArgb(0, 100, 0) : Color.DimGray,
            Text      = autoDetected != null
                ? $"✅ Auto-detected: {Path.GetFileName(autoDetected)}"
                : "ℹ️ No provision.json found alongside installer — you can browse for one or skip"
        };

        _txtProvision.TextChanged += (_, _) =>
        {
            var path = _txtProvision.Text.Trim();
            var exists = path.Length > 0 && File.Exists(path);
            _lblProvisionNote!.Text      = exists ? $"✅ {Path.GetFileName(path)}" : (path.Length > 0 ? "❌ File not found" : "ℹ️ No provision file selected — configure on first launch");
            _lblProvisionNote!.ForeColor = exists ? Color.FromArgb(0, 100, 0) : (path.Length > 0 ? Color.DarkRed : Color.DimGray);
        };

        _pnlProvision.Controls.Add(_lblProvisionNote);

        //######################################
        //Update notice — shown instead of provision panel during updates
        //######################################
        var lblUpdateNote = new Label
        {
            Bounds    = new Rectangle(20, 224, 472, 18),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(0, 100, 150),
            Text      = "ℹ️ Updating existing installation — your config and flavours will not be changed",
            Visible   = isUpdate
        };

        //######################################
        //Progress
        //######################################
        int progressTop = isUpdate ? 248 : 290;

        _progress = new ProgressBar
        {
            Bounds  = new Rectangle(20, progressTop, 472, 18),
            Minimum = 0, Maximum = 100, Value = 0,
            Style   = ProgressBarStyle.Continuous
        };
        _lblStatus = new Label
        {
            Text      = isUpdate ? "Ready to update." : "Ready to install.",
            Bounds    = new Rectangle(20, progressTop + 22, 472, 20),
            ForeColor = Color.Gray
        };

        //######################################
        //Buttons
        //######################################
        int btnTop = isUpdate ? 298 : 340;

        _btnInstall = new Button
        {
            Text      = isUpdate ? "Update" : "Install",
            Bounds    = new Rectangle(308, btnTop, 90, 32),
            BackColor = isUpdate ? Color.FromArgb(0, 100, 150) : Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += OnInstall;

        _btnCancel = new Button { Text = "Cancel", Bounds = new Rectangle(406, btnTop, 86, 32) };
        _btnCancel.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            header, lblPath, _txtPath, _btnBrowse,
            _chkDesktop, _chkStartMenu, _chkStartup,
            _pnlProvision, lblUpdateNote,
            _progress, _lblStatus, _btnInstall, _btnCancel
        });
    }

    //######################################
    //Look for provision.json alongside the installer exe (auto-detect convenience)
    //######################################
    private static string? FindProvisionFile()
    {
        var dir  = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var path = Path.Combine(dir, "provision.json");
        return File.Exists(path) ? path : null;
    }

    private async void OnInstall(object? sender, EventArgs e)
    {
        var installDir    = _txtPath!.Text.Trim();
        var mkDesktop     = _chkDesktop!.Checked;
        var mkStartMenu   = _chkStartMenu!.Checked;
        var mkStartup     = _chkStartup!.Checked;
        var provisionPath = (!IsUpdate && _txtProvision != null)
            ? _txtProvision.Text.Trim()
            : null;

        //Validate provision path if one was entered
        if (!string.IsNullOrEmpty(provisionPath) && !File.Exists(provisionPath))
        {
            MessageBox.Show(
                $"The provision file was not found:\n{provisionPath}\n\nPlease browse for a valid file or clear the field.",
                "Provision File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetControls(false);
        try
        {
            await Task.Run(() => RunInstall(installDir, mkDesktop, mkStartMenu, mkStartup, provisionPath));

            var msg = IsUpdate
                ? $"{AppName} has been updated to v{AppVersion}.\n\nLocation: {installDir}\n\nWould you like to launch {AppName} now?"
                : $"{AppName} installed successfully.\n\nLocation: {installDir}\n\nWould you like to launch {AppName} now?";

            var launch = MessageBox.Show(msg, $"{AppName} Setup",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (launch == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = Path.Combine(installDir, $"{AppName}.exe"),
                    UseShellExecute = true
                });
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{(IsUpdate ? "Update" : "Installation")} failed:\n\n{ex.Message}",
                "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetControls(true);
            SetStatus($"{(IsUpdate ? "Update" : "Installation")} failed.", 0);
        }
    }

    private void RunInstall(string installDir, bool mkDesktop, bool mkStartMenu, bool mkStartup, string? provisionPath)
    {
        //######################################
        //STEP 0 Close any running PandaTools instance
        //######################################
        SetStatus("Checking for a running instance...", 2);
        var procs = Process.GetProcessesByName("PandaTools");
        foreach (var p in procs)
        {
            try
            {
                p.CloseMainWindow();
                if (!p.WaitForExit(4000)) p.Kill();
                p.Dispose();
            }
            catch { /* already gone */ }
        }
        if (procs.Length > 0) Thread.Sleep(1200);

        //######################################
        //STEP 1 Create directory
        //######################################
        SetStatus("Creating install directory...", 5);
        Directory.CreateDirectory(installDir);

        //######################################
        //STEP 2 Extract payload.zip
        //######################################
        SetStatus("Extracting files...", 10);
        var asm          = Assembly.GetExecutingAssembly();
        var resourceName = Array.Find(
            asm.GetManifestResourceNames(),
            n => n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded payload not found.");

        var tempZip = Path.Combine(Path.GetTempPath(), "PandaToolsPayload.zip");
        try
        {
            using (var res = asm.GetManifestResourceStream(resourceName)!)
            using (var fs  = File.Create(tempZip))
                res.CopyTo(fs);

            using var zip = ZipFile.OpenRead(tempZip);
            int total = zip.Entries.Count, done = 0;

            foreach (var entry in zip.Entries)
            {
                var dest = Path.GetFullPath(
                    Path.Combine(installDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

                if (entry.FullName.EndsWith('/'))
                    Directory.CreateDirectory(dest);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, overwrite: true);
                }
                done++;
                SetStatus($"Extracting: {entry.Name}", 10 + (int)(done / (double)total * 50));
            }
        }
        finally { File.Delete(tempZip); }

        //######################################
        //STEP 3 Apply provision.json — fresh install only, never on update
        //config.json is only written if one does not already exist.
        //provision_flavours\ folder alongside the installer is also seeded if present.
        //######################################
        if (!IsUpdate && !string.IsNullOrEmpty(provisionPath) && File.Exists(provisionPath))
        {
            SetStatus("Applying organisation configuration...", 62);
            try
            {
                var appDataDir  = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PandaTools");
                var configDest  = Path.Combine(appDataDir, "config.json");
                var flavourDest = Path.Combine(appDataDir, "flavours");

                Directory.CreateDirectory(appDataDir);
                Directory.CreateDirectory(flavourDest);

                if (!File.Exists(configDest))
                    File.Copy(provisionPath, configDest, overwrite: false);

                //Also seed flavour files from provision_flavours\ next to the provision file
                var provisionFlavoursDir = Path.Combine(
                    Path.GetDirectoryName(provisionPath)!, "provision_flavours");

                if (Directory.Exists(provisionFlavoursDir))
                {
                    foreach (var f in Directory.GetFiles(provisionFlavoursDir, "*.json"))
                    {
                        var dest = Path.Combine(flavourDest, Path.GetFileName(f));
                        if (!File.Exists(dest))
                            File.Copy(f, dest, overwrite: false);
                    }
                }
            }
            catch (Exception ex)
            {
                //Non-fatal — app will fall back to first-run setup wizard
                SetStatus($"Warning: provision failed — {ex.Message}", 62);
                Thread.Sleep(2000);
            }
        }

        //######################################
        //STEP 4 Copy setup exe as uninstaller
        //######################################
        SetStatus("Copying uninstaller...", 68);
        File.Copy(Environment.ProcessPath!,
            Path.Combine(installDir, "PandaToolsSetup.exe"), overwrite: true);

        //######################################
        //STEP 5 Shortcuts
        //######################################
        var mainExe  = Path.Combine(installDir, $"{AppName}.exe");
        var iconPath = Path.Combine(installDir, @"assets\PandaTools.ico");

        if (mkStartMenu)
        {
            SetStatus("Creating Start Menu shortcut...", 76);
            CreateShortcut(mainExe, iconPath, installDir,
                $@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\{AppName}.lnk");
        }
        if (mkDesktop)
        {
            SetStatus("Creating Desktop shortcut...", 82);
            CreateShortcut(mainExe, iconPath, installDir,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    $"{AppName}.lnk"));
        }

        //######################################
        //STEP 6 Startup registry
        //######################################
        if (mkStartup)
        {
            SetStatus("Configuring startup...", 88);
            using var runKey = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            runKey?.SetValue(AppName, $"\"{mainExe}\"");
        }

        //######################################
        //STEP 7 Register in Add/Remove Programs
        //######################################
        SetStatus("Registering application...", 94);
        using var uninstKey = Registry.LocalMachine.CreateSubKey(RegKey);
        uninstKey.SetValue("DisplayName",     AppName);
        uninstKey.SetValue("DisplayVersion",  AppVersion);
        uninstKey.SetValue("Publisher",       Publisher);
        uninstKey.SetValue("InstallLocation", installDir);
        uninstKey.SetValue("DisplayIcon",     iconPath);
        uninstKey.SetValue("UninstallString", $"\"{Path.Combine(installDir, "PandaToolsSetup.exe")}\" --uninstall");
        uninstKey.SetValue("NoModify",        1, RegistryValueKind.DWord);
        uninstKey.SetValue("NoRepair",        1, RegistryValueKind.DWord);

        SetStatus(IsUpdate ? "Update complete!" : "Installation complete!", 100);
    }

    private static void CreateShortcut(string target, string icon, string workDir, string path)
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic lnk   = shell.CreateShortcut(path);
        lnk.TargetPath       = target;
        lnk.WorkingDirectory = workDir;
        lnk.IconLocation     = icon;
        lnk.Save();
        Marshal.ReleaseComObject(lnk);
        Marshal.ReleaseComObject(shell);
    }

    private void SetStatus(string msg, int pct)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(msg, pct)); return; }
        _lblStatus!.Text    = msg;
        _progress!.Value    = Math.Clamp(pct, 0, 100);
    }

    private void SetControls(bool on)
    {
        if (InvokeRequired) { Invoke(() => SetControls(on)); return; }
        _btnInstall!.Enabled  = on; _btnCancel!.Enabled    = on;
        _btnBrowse!.Enabled   = on && !IsUpdate;
        _txtPath!.ReadOnly    = !on || IsUpdate;
        _chkDesktop!.Enabled  = on; _chkStartMenu!.Enabled = on; _chkStartup!.Enabled = on;
        if (_pnlProvision != null) _pnlProvision.Enabled   = on;
    }
}
