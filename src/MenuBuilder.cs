using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

public static class MenuBuilder
{
    public static ContextMenuStrip Build(Action onExit)
    {
        var menu    = new ContextMenuStrip();
        var flavour = ConfigLoader.FlavourConfig;
        var cfg     = ConfigLoader.AppConfig;

        menu.Items.Add(new ToolStripMenuItem(cfg.Flavour) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        foreach (var section in flavour.Menu)
        {
            if (string.IsNullOrWhiteSpace(section.Section)) continue;
            var label   = string.IsNullOrWhiteSpace(section.Icon)
                ? section.Section
                : $"{section.Icon} {section.Section}";
            var submenu = new ToolStripMenuItem(label);
            foreach (var item in section.Items)
            {
                var m = new ToolStripMenuItem(item.Label);
                m.Click += ResolveHandler(item);
                submenu.DropDownItems.Add(m);
            }
            menu.Items.Add(submenu);
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("⚙️ Settings", null, (_, _) =>
        {
            try { using var w = new SettingsWindow(); w.ShowDialog(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open settings:\n{ex.Message}",
                    "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("❌ Exit", null, (_, _) => onExit());

        return menu;
    }

    // ── Handler router ────────────────────────────────────────────────
    private static EventHandler ResolveHandler(FlavourItem item) =>
        item.Type.ToLowerInvariant() switch
        {
            // url - supports runas_profile, multi-value, configurable browser
            "url" => (_, _) =>
            {
                var cfg  = ConfigLoader.AppConfig;
                var urls = item.Values.Count > 0 ? item.Values : new List<string> { item.Value };

                if (!string.IsNullOrWhiteSpace(item.RunAsProfile))
                {
                    var profile = cfg.RunAsProfiles.FirstOrDefault(p =>
                        p.Name.Equals(item.RunAsProfile, StringComparison.OrdinalIgnoreCase));

                    // Prompt for password once, reuse for all URLs
                    var resolvedProfile = ResolveProfilePassword(profile);
                    if (resolvedProfile == null) return;

                    foreach (var url in urls)
                        OpenUrlAsUser(url, cfg.UrlBrowserName, cfg.UrlBrowserPath, resolvedProfile);
                }
                else
                {
                    foreach (var url in urls)
                        OpenUrl(url, cfg.UrlBrowserName, cfg.UrlBrowserPath);
                }
            },

            // incognito - supports runas_profile, multi-value, configurable browser
            "incognito" => (_, _) =>
            {
                var cfg  = ConfigLoader.AppConfig;
                var urls = item.Values.Count > 0 ? item.Values : new List<string> { item.Value };

                if (!string.IsNullOrWhiteSpace(item.RunAsProfile))
                {
                    var profile = cfg.RunAsProfiles.FirstOrDefault(p =>
                        p.Name.Equals(item.RunAsProfile, StringComparison.OrdinalIgnoreCase));

                    // Prompt for password once, reuse for all URLs
                    var resolvedProfile = ResolveProfilePassword(profile);
                    if (resolvedProfile == null) return;

                    foreach (var url in urls)
                        OpenIncognitoAsUser(url, cfg.BrowserName, cfg.BrowserPath, resolvedProfile);
                }
                else
                {
                    foreach (var url in urls)
                        OpenIncognito(url, cfg.BrowserName, cfg.BrowserPath);
                }
            },

            "app" => (_, _) =>
            {
                try
                {
                    var (exe, args) = ParseCommandLine(item.Value, item.Arguments);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = exe,
                        Arguments       = args,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not launch:\n{ex.Message}",
                        "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            "explorer" => (_, _) =>
            {
                try
                {
                    var path = item.Value.Trim();
                    if (!Directory.Exists(path) && !File.Exists(path))
                    {
                        MessageBox.Show($"Path not found or inaccessible:\n{path}",
                            "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "explorer.exe",
                        Arguments       = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open path:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            "runas" => (_, _) =>
            {
                var cfg     = ConfigLoader.AppConfig;
                var profile = cfg.RunAsProfiles.FirstOrDefault(p =>
                    p.Name.Equals(item.RunAsProfile, StringComparison.OrdinalIgnoreCase));
                var (parsedExe, parsedArgs) = ParseCommandLine(item.Value, item.Arguments);
                LaunchAsUser(parsedExe, parsedArgs, profile);
            },

            "powershell" => (_, _) =>
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "powershell.exe",
                    Arguments       = $"-NoProfile -Command \"{item.Value}\"",
                    UseShellExecute = false,
                    CreateNoWindow  = true
                }),

            "exe" => (_, _) =>
            {
                var (exe, args) = ParseCommandLine(item.Value, item.Arguments);
                Process.Start(new ProcessStartInfo
                {
                    FileName        = exe,
                    Arguments       = args,
                    UseShellExecute = true
                });
            },

            "script" => async (_, _) =>
            {
                try { await GitLabScriptRunner.RunAsync(item.ProjectId, item.FilePath, item.Branch); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Script error:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            _ => (_, _) => { }
        };

    // ── Resolve password once for multi-URL launches ──────────────────
    // Returns a profile copy with password pre-filled so LaunchAsUser
    // never prompts again per-URL. Returns null if user cancels.
    private static RunAsProfile? ResolveProfilePassword(RunAsProfile? profile)
    {
        if (profile == null) return null;

        if (!string.IsNullOrEmpty(profile.Password))
            return profile;

        var (ok, entered) = PromptForPassword(profile.Username);
        if (!ok) return null;

        return new RunAsProfile
        {
            Name     = profile.Name,
            Username = profile.Username,
            Password = entered
        };
    }

    // ── URL - standard ────────────────────────────────────────────────
    private static void OpenUrl(string url, string browserName, string customPath)
    {
        try
        {
            if (browserName.Equals("default", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(browserName))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }

            var exePath = ResolveBrowserExe(browserName, customPath);
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = exePath,
                    Arguments       = $"\"{url}\"",
                    UseShellExecute = true
                });
                return;
            }

            // Fallback - system default
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
    }

    // ── URL - as a different user ─────────────────────────────────────
    // UseShellExecute must be false to pass credentials, so we need an
    // explicit exe - "default" is resolved by trying common browsers in order.
    private static void OpenUrlAsUser(string url, string browserName, string customPath, RunAsProfile? profile)
    {
        string? exePath;

        if (browserName.Equals("default", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(browserName))
        {
            exePath = FindBrowser("edge");
            if (string.IsNullOrEmpty(exePath)) exePath = FindBrowser("chrome");
            if (string.IsNullOrEmpty(exePath)) exePath = FindBrowser("firefox");
            if (string.IsNullOrEmpty(exePath)) exePath = FindBrowser("brave");

            if (string.IsNullOrEmpty(exePath))
            {
                MessageBox.Show(
                    "Could not locate a browser for RunAs launch.\n\n" +
                    "Go to Settings → Browser and select a specific browser\n" +
                    "when using runas_profile with url items.",
                    "PandaTools - Browser Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        else
        {
            exePath = ResolveBrowserExe(browserName, customPath);
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                MessageBox.Show(
                    $"Could not locate browser: {browserName}\n" +
                    "Configure it in Settings → Browser.",
                    "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        LaunchAsUser(exePath, $"\"{url}\"", profile);
    }

    // ── Incognito - standard ──────────────────────────────────────────
    private static void OpenIncognito(string url, string browserName, string customPath)
    {
        try
        {
            var (exePath, flag) = ResolveIncognitoBrowser(browserName, customPath);

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = exePath,
                    Arguments       = $"{flag} \"{url}\"",
                    UseShellExecute = true
                });
                return;
            }
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
    }

    // ── Incognito - as a different user ───────────────────────────────
    private static void OpenIncognitoAsUser(string url, string browserName, string customPath, RunAsProfile? profile)
    {
        var (exePath, flag) = ResolveIncognitoBrowser(browserName, customPath);

        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            MessageBox.Show(
                $"Could not locate browser: {browserName}\n" +
                "Configure it in Settings → Browser.",
                "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        LaunchAsUser(exePath, $"{flag} \"{url}\"", profile);
    }

    // ── Browser resolution ────────────────────────────────────────────
    private static string ResolveBrowserExe(string browserName, string customPath) =>
        browserName.ToLowerInvariant() switch
        {
            "chrome"  => FindBrowser("chrome"),
            "edge"    => FindBrowser("edge"),
            "firefox" => FindBrowser("firefox"),
            "brave"   => FindBrowser("brave"),
            "custom"  => customPath,
            _         => ""
        };

    private static (string exePath, string flag) ResolveIncognitoBrowser(string browserName, string customPath) =>
        browserName.ToLowerInvariant() switch
        {
            "chrome"  => (FindBrowser("chrome"),  "--incognito"),
            "edge"    => (FindBrowser("edge"),    "--inprivate"),
            "firefox" => (FindBrowser("firefox"), "-private-window"),
            "brave"   => (FindBrowser("brave"),   "--incognito"),
            "custom"  => (customPath,             "--incognito"),
            _         => (FindBrowser("edge"),    "--inprivate")
        };

    private static string FindBrowser(string name) => name switch
    {
        "chrome"  => FirstExisting(
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"),
        "edge"    => FirstExisting(
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
        "firefox" => FirstExisting(
            @"C:\Program Files\Mozilla Firefox\firefox.exe",
            @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"),
        "brave"   => FirstExisting(
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
            @"C:\Program Files (x86)\BraveSoftware\Brave-Browser\Application\brave.exe"),
        _         => ""
    };

    private static string FirstExisting(params string[] paths) =>
        paths.FirstOrDefault(File.Exists) ?? "";

    // ── ParseCommandLine ──────────────────────────────────────────────
    private static (string exe, string args) ParseCommandLine(string value, string? extraArgs)
    {
        value = value?.Trim() ?? "";
        string exe;
        string inlineArgs;

        if (value.StartsWith("\""))
        {
            int close = value.IndexOf('"', 1);
            if (close < 0) { exe = value.Trim('"'); inlineArgs = ""; }
            else           { exe = value.Substring(1, close - 1); inlineArgs = value.Substring(close + 1).Trim(); }
        }
        else
        {
            exe = value; inlineArgs = "";
            int searchFrom = 0; bool found = false;
            while (true)
            {
                int space = value.IndexOf(' ', searchFrom);
                if (space < 0) break;
                var candidate = value.Substring(0, space);
                if (File.Exists(candidate))
                {
                    exe = candidate; inlineArgs = value.Substring(space + 1).Trim();
                    found = true; break;
                }
                searchFrom = space + 1;
            }
            if (!found) { exe = value; inlineArgs = ""; }
        }

        var combined = string.IsNullOrWhiteSpace(extraArgs)
            ? inlineArgs
            : string.IsNullOrWhiteSpace(inlineArgs) ? extraArgs : $"{inlineArgs} {extraArgs}";

        return (exe, combined);
    }

    // ── Main RunAs launcher ───────────────────────────────────────────
    private static void LaunchAsUser(string target, string arguments, RunAsProfile? profile)
    {
        try
        {
            var (resolvedExe, resolvedArgs) = ResolveToExecutable(target, arguments);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Username))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = resolvedExe,
                    Arguments       = resolvedArgs,
                    UseShellExecute = true,
                    Verb            = "runas"
                });
                return;
            }

            string domain, user;
            if (profile.Username.Contains('\\'))
            {
                var parts = profile.Username.Split('\\', 2);
                domain = parts[0];
                user   = parts[1];
            }
            else if (profile.Username.Contains('@'))
            { domain = "."; user = profile.Username; }
            else
            { domain = Environment.MachineName; user = profile.Username; }

            SecureString password;
            if (!string.IsNullOrEmpty(profile.Password))
                password = ToSecureString(profile.Password);
            else
            {
                var (ok, entered) = PromptForPassword(profile.Username);
                if (!ok) return;
                password = ToSecureString(entered);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName        = resolvedExe,
                Arguments       = resolvedArgs,
                UseShellExecute = false,
                UserName        = user,
                Domain          = domain,
                Password        = password,
                LoadUserProfile = true
            });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex)
        {
            MessageBox.Show($"RunAs failed:\n{ex.Message}",
                "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Resolve any file type to a launchable exe + args ─────────────
    private static (string exe, string args) ResolveToExecutable(string target, string extraArgs)
    {
        var ext = Path.GetExtension(target).ToLowerInvariant();
        switch (ext)
        {
            case ".lnk":
                var (lnkExe, lnkArgs) = ResolveLnk(target);
                if (!string.IsNullOrEmpty(lnkExe))
                {
                    var combined = string.IsNullOrEmpty(lnkArgs)
                        ? extraArgs
                        : $"{lnkArgs} {extraArgs}".Trim();
                    return ResolveToExecutable(lnkExe, combined);
                }
                goto default;
            case ".msc":
                var mmcPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "mmc.exe");
                return (mmcPath, string.IsNullOrWhiteSpace(extraArgs)
                    ? $"\"{target}\""
                    : $"\"{target}\" {extraArgs}");
            case ".cpl":
                var controlPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "control.exe");
                return (controlPath, $"\"{target}\"");
            case ".bat":
            case ".cmd":
                var cmdPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                return (cmdPath, string.IsNullOrWhiteSpace(extraArgs)
                    ? $"/c \"{target}\""
                    : $"/c \"{target}\" {extraArgs}");
            case ".ps1":
                var psPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    @"WindowsPowerShell\v1.0\powershell.exe");
                if (!File.Exists(psPath)) psPath = "powershell.exe";
                return (psPath, string.IsNullOrWhiteSpace(extraArgs)
                    ? $"-NoProfile -ExecutionPolicy Bypass -File \"{target}\""
                    : $"-NoProfile -ExecutionPolicy Bypass -File \"{target}\" {extraArgs}");
            case ".exe":
                return (target, extraArgs);
            default:
                return (target, extraArgs);
        }
    }

    // ── Resolve .lnk shortcut ─────────────────────────────────────────
    private static (string target, string args) ResolveLnk(string lnkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return ("", "");
            dynamic shell    = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string  tgt      = shortcut.TargetPath ?? "";
            string  args2    = shortcut.Arguments  ?? "";
            Marshal.ReleaseComObject(shortcut);
            Marshal.ReleaseComObject(shell);
            return (tgt, args2);
        }
        catch { return ("", ""); }
    }

    // ── Password prompt ───────────────────────────────────────────────
    private static (bool ok, string password) PromptForPassword(string username)
    {
        var frm = new Form
        {
            Text            = "PandaTools - Enter Password",
            Size            = new System.Drawing.Size(360, 162),
            StartPosition   = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox     = false, MinimizeBox = false,
            TopMost         = true, Icon = AppIcon.Get()
        };
        frm.Controls.Add(new Label
        {
            Text = $"Password for {username}:",
            Left = 12, Top = 16, Width = 330, Height = 20,
            Font = new System.Drawing.Font("Segoe UI", 9f)
        });
        var txtPass = new TextBox
        {
            Left = 12, Top = 42, Width = 330,
            UseSystemPasswordChar = true,
            Font = new System.Drawing.Font("Segoe UI", 9f)
        };
        frm.Controls.Add(txtPass);
        var btnOk = new Button
        {
            Text = "OK", Left = 172, Top = 78, Width = 82, Height = 28,
            DialogResult = DialogResult.OK,
            Font = new System.Drawing.Font("Segoe UI", 9f)
        };
        var btnCancel = new Button
        {
            Text = "Cancel", Left = 260, Top = 78, Width = 82, Height = 28,
            DialogResult = DialogResult.Cancel,
            Font = new System.Drawing.Font("Segoe UI", 9f)
        };
        frm.Controls.AddRange(new System.Windows.Forms.Control[] { btnOk, btnCancel });
        frm.AcceptButton = btnOk;
        frm.CancelButton = btnCancel;
        var dlg = frm.ShowDialog();
        var pwd = txtPass.Text;
        frm.Dispose();
        return (dlg == DialogResult.OK, pwd);
    }

    // ── SecureString helper ───────────────────────────────────────────
    private static SecureString ToSecureString(string input)
    {
        var s = new SecureString();
        foreach (var c in input) s.AppendChar(c);
        s.MakeReadOnly();
        return s;
    }
}
