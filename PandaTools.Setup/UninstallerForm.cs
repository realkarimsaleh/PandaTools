using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

public class UninstallerForm : Form
{
    private const string AppName = "PandaTools";

    public UninstallerForm() => BuildUI();

    void BuildUI()
    {
        Text            = $"Uninstall {AppName}";
        Size            = new Size(420, 210);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = Color.White;
        Font            = new Font("Segoe UI", 9f);

        Controls.Add(new Label
        {
            Text   = $"Are you sure you want to uninstall {AppName}?\n\nThis will remove all installed files, shortcuts, and registry entries.",
            Bounds = new Rectangle(20, 20, 372, 60),
            Font   = new Font("Segoe UI", 9f)
        });

        var btnUninstall = new Button
        {
            Text      = "Uninstall",
            Bounds    = new Rectangle(216, 124, 90, 32),
            BackColor = Color.FromArgb(196, 43, 28),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnUninstall.FlatAppearance.BorderSize = 0;
        btnUninstall.Click += OnUninstall;

        var btnCancel = new Button
        {
            Text   = "Cancel",
            Bounds = new Rectangle(314, 124, 80, 32)
        };
        btnCancel.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { btnUninstall, btnCancel });
    }

    private void OnUninstall(object? sender, EventArgs e)
    {
        try
        {
            //Read install location from registry
            string installDir;
            using (var key = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}"))
            {
                installDir = key?.GetValue("InstallLocation") as string
                    ?? $@"C:\Program Files\{AppName}";
            }

            //Remove Start Menu shortcut
            TryDelete($@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\{AppName}.lnk");

            //Remove Desktop shortcut
            TryDelete(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                $"{AppName}.lnk"));

            //Remove startup registry entry
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
                runKey?.DeleteValue(AppName, throwOnMissingValue: false);
            }
            catch { /* not set */ }

            //Remove Add/Remove Programs registry entry
            try
            {
                Registry.LocalMachine.DeleteSubKey(
                    $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}");
            }

            catch { /* already removed */ }

            //Delete install folder via deferred cmd script (handles the
            //case where this setup exe lives inside the install folder)
            var cleanup = Path.Combine(Path.GetTempPath(), "PandaToolsCleanup.cmd");
            File.WriteAllText(cleanup,
                $"""
                @echo off
                timeout /t 3 /nobreak >nul
                rmdir /s /q "{installDir}"
                del "%~f0"
                """);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = cleanup,
                UseShellExecute = false,
                CreateNoWindow  = true,
                WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden
            });

            MessageBox.Show(
                $"{AppName} has been uninstalled successfully.",
                $"Uninstall {AppName}",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Close();
        }

        catch (Exception ex)
        {
            MessageBox.Show(
                $"Uninstall failed:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort */ }
    }
}
