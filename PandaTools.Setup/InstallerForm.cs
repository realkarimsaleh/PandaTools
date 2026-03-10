using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

public class InstallerForm : Form
{
    private const string AppName   = "PandaTools";
    private const string Publisher = "LBU-ITSRK";

    private static readonly string AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private TextBox     _txtPath      = null!;
    private CheckBox    _chkDesktop   = null!, _chkStartMenu = null!, _chkStartup = null!;
    private Button      _btnInstall   = null!, _btnBrowse    = null!, _btnCancel  = null!;
    private ProgressBar _progress     = null!;
    private Label       _lblStatus    = null!;

    public InstallerForm() => BuildUI();

    void BuildUI()
    {
        Text            = $"{AppName} Setup";
        Size            = new Size(520, 390);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = Color.White;
        Font            = new Font("Segoe UI", 9f);

        // ── Header ────────────────────────────────────────────────────
        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(24, 24, 24) };
        header.Controls.Add(new Label
        {
            Text = $"🐼  {AppName}", ForeColor = Color.White,
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            Bounds = new Rectangle(18, 12, 380, 32), AutoSize = false
        });
        header.Controls.Add(new Label
        {
            Text = $"Version {AppVersion}   ·   {Publisher}",
            ForeColor = Color.FromArgb(160, 160, 160),
            Font = new Font("Segoe UI", 8.5f),
            Bounds = new Rectangle(20, 46, 400, 18), AutoSize = false
        });

        // ── Install path ──────────────────────────────────────────────
        var lblPath = new Label { Text = "Install location:", Bounds = new Rectangle(20, 90, 200, 20) };
        _txtPath = new TextBox
        {
            Text = $@"C:\Program Files\{AppName}",
            Bounds = new Rectangle(20, 110, 382, 24)
        };
        _btnBrowse = new Button { Text = "Browse", Bounds = new Rectangle(408, 109, 84, 26) };
        _btnBrowse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog
            {
                Description = "Select install folder",
                SelectedPath = _txtPath.Text,
                UseDescriptionForTitle = true
            };
            if (d.ShowDialog() == DialogResult.OK) _txtPath.Text = d.SelectedPath;
        };

        // ── Options ───────────────────────────────────────────────────
        _chkDesktop   = new CheckBox { Text = "Create Desktop shortcut",            Bounds = new Rectangle(20, 150, 300, 22), Checked = true  };
        _chkStartMenu = new CheckBox { Text = "Create Start Menu shortcut",         Bounds = new Rectangle(20, 172, 300, 22), Checked = true  };
        _chkStartup   = new CheckBox { Text = "Run PandaTools at Windows startup",  Bounds = new Rectangle(20, 194, 300, 22), Checked = false };

        // ── Progress ──────────────────────────────────────────────────
        _progress = new ProgressBar
        {
            Bounds = new Rectangle(20, 234, 472, 18),
            Minimum = 0, Maximum = 100, Value = 0,
            Style = ProgressBarStyle.Continuous
        };
        _lblStatus = new Label
        {
            Text = "Ready to install.",
            Bounds = new Rectangle(20, 256, 472, 20),
            ForeColor = Color.Gray
        };

        // ── Buttons ───────────────────────────────────────────────────
        _btnInstall = new Button
        {
            Text = "Install", Bounds = new Rectangle(308, 298, 90, 32),
            BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += OnInstall;

        _btnCancel = new Button { Text = "Cancel", Bounds = new Rectangle(406, 298, 86, 32) };
        _btnCancel.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            header, lblPath, _txtPath, _btnBrowse,
            _chkDesktop, _chkStartMenu, _chkStartup,
            _progress, _lblStatus, _btnInstall, _btnCancel
        });
    }

    private async void OnInstall(object? sender, EventArgs e)
    {
        // Capture UI values on the UI thread before Task.Run
        var installDir  = _txtPath.Text.Trim();
        var mkDesktop   = _chkDesktop.Checked;
        var mkStartMenu = _chkStartMenu.Checked;
        var mkStartup   = _chkStartup.Checked;

        SetControls(false);
        try
        {
            await Task.Run(() => RunInstall(installDir, mkDesktop, mkStartMenu, mkStartup));
            MessageBox.Show($"{AppName} installed successfully.\n\nLocation: {installDir}",
                $"{AppName} Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Installation failed:\n\n{ex.Message}",
                "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetControls(true);
            SetStatus("Installation failed.", 0);
        }
    }

    private void RunInstall(string installDir, bool mkDesktop, bool mkStartMenu, bool mkStartup)
    {
        // 1. Create directory
        SetStatus("Creating install directory...", 5);
        Directory.CreateDirectory(installDir);

        // 2. Find and extract embedded payload.zip
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

            using var zip   = ZipFile.OpenRead(tempZip);
            var entries     = zip.Entries;
            int total       = entries.Count;
            int done        = 0;

            foreach (var entry in entries)
            {
                var dest = Path.GetFullPath(
                    Path.Combine(installDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

                if (entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(dest);
                }
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

        // 3. Copy this setup exe into install dir so it can act as uninstaller
        SetStatus("Copying uninstaller...", 62);
        var setupTarget = Path.Combine(installDir, "PandaToolsSetup.exe");
        File.Copy(Environment.ProcessPath!, setupTarget, overwrite: true);

        // 4. Shortcuts
        var mainExe  = Path.Combine(installDir, $"{AppName}.exe");
        var iconPath = Path.Combine(installDir, @"assets\PandaTools.ico");

        if (mkStartMenu)
        {
            SetStatus("Creating Start Menu shortcut...", 70);
            CreateShortcut(mainExe, iconPath, installDir,
                $@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\{AppName}.lnk");
        }
        if (mkDesktop)
        {
            SetStatus("Creating Desktop shortcut...", 76);
            CreateShortcut(mainExe, iconPath, installDir,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    $"{AppName}.lnk"));
        }

        // 5. Startup
        if (mkStartup)
        {
            SetStatus("Configuring startup...", 82);
            using var runKey = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            runKey?.SetValue(AppName, $"\"{mainExe}\"");
        }

        // 6. Register in Add/Remove Programs
        SetStatus("Registering application...", 90);
        using var uninstKey = Registry.LocalMachine.CreateSubKey(
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}");
        uninstKey.SetValue("DisplayName",     AppName);
        uninstKey.SetValue("DisplayVersion",  AppVersion);
        uninstKey.SetValue("Publisher",       Publisher);
        uninstKey.SetValue("InstallLocation", installDir);
        uninstKey.SetValue("DisplayIcon",     iconPath);
        uninstKey.SetValue("UninstallString", $"\"{setupTarget}\" --uninstall");
        uninstKey.SetValue("NoModify",        1, RegistryValueKind.DWord);
        uninstKey.SetValue("NoRepair",        1, RegistryValueKind.DWord);

        SetStatus("Installation complete!", 100);
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
        _lblStatus.Text = msg;
        _progress.Value = Math.Clamp(pct, 0, 100);
    }

    private void SetControls(bool on)
    {
        if (InvokeRequired) { Invoke(() => SetControls(on)); return; }
        _btnInstall.Enabled = on; _btnCancel.Enabled = on;
        _btnBrowse.Enabled  = on; _txtPath.Enabled   = on;
        _chkDesktop.Enabled = on; _chkStartMenu.Enabled = on; _chkStartup.Enabled = on;
    }
}
