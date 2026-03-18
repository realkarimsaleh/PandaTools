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
        var menu         = new ContextMenuStrip();
        var flavour      = ConfigLoader.FlavourConfig;
        var localFlavour = ConfigLoader.LocalFlavourConfig;
        var cfg          = ConfigLoader.AppConfig;

        //######################################
        //Subscribed Flavour (Managed)
        //######################################
        menu.Items.Add(new ToolStripMenuItem(cfg.Flavour) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        RenderMenuSections(menu, flavour.Menu);

        //######################################
        //Local Flavour (Personal)
        //######################################
        if (!cfg.ShowOnlySubscribedFlavour)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem($"👤 {Environment.UserName}'s Menu") { Enabled = false });
            
            if (localFlavour.Menu != null && localFlavour.Menu.Count > 0)
            {
                RenderMenuSections(menu, localFlavour.Menu);
            }
            else
            {
                //Show a placeholder so the user knows it's empty but working
                menu.Items.Add(new ToolStripMenuItem("   (Empty)") { Enabled = false });
            }

            //A convenient button to launch our GUI Editor!
            menu.Items.Add("✏️ Edit My Menu", null, (_, _) =>
            {
                try { FlavourEditorWindow.ShowWindow(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open Menu Editor:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }

        menu.Items.Add(new ToolStripSeparator());

        //######################################
        //PandaShell
        //######################################
        if (flavour.ShowPandaShell || localFlavour.ShowPandaShell)
        {
            menu.Items.Add("🖥 PandaShell", null, (_, _) =>
            {
                try { PandaShellWindow.ShowWindow(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open PandaShell:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }

        //######################################
        //PandaPassGen
        //######################################
        if (flavour.ShowPandaPassGen || localFlavour.ShowPandaPassGen)
        {
            menu.Items.Add("🔑 PandaPassGen", null, (_, _) =>
            {
                try { PandaPassGenWindow.ShowWindow(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open PandaPassGen:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }

        //######################################
        //PandaLAPS
        //######################################
        if (flavour.ShowPandaLaps || localFlavour.ShowPandaLaps)
        {
            menu.Items.Add("🛡️ PandaLAPS", null, (_, _) =>
            {
                try { PandaLapsWindow.ShowWindow(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open PandaLAPS:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }

        menu.Items.Add("⚙️ Settings", null, (_, _) =>
        {
            try { SettingsWindow.ShowWindow(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open settings:\n{ex.Message}",
                    "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => onExit());

        return menu;
    }

    //Helper method to draw menus so we don't repeat logic for Subscribed vs Local!
    private static void RenderMenuSections(ContextMenuStrip menu, List<FlavourSection> sections)
    {
        foreach (var section in sections)
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
    }

    //######################################
    //Handler router
    //######################################
    private static EventHandler ResolveHandler(FlavourItem item) =>
        item.Type.ToLowerInvariant() switch
        {
            "url" => (_, _) =>
            {
                var cfg  = ConfigLoader.AppConfig;
                var urls = item.Values.Count > 0 ? item.Values : new List<string> { item.Value };

                if (!string.IsNullOrWhiteSpace(item.RunAsProfile))
                {
                    var profile = cfg.RunAsProfiles.FirstOrDefault(p =>
                        p.Name.Equals(item.RunAsProfile, StringComparison.OrdinalIgnoreCase));
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

            "incognito" => (_, _) =>
            {
                var cfg  = ConfigLoader.AppConfig;
                var urls = item.Values.Count > 0 ? item.Values : new List<string> { item.Value };

                if (!string.IsNullOrWhiteSpace(item.RunAsProfile))
                {
                    var profile = cfg.RunAsProfiles.FirstOrDefault(p =>
                        p.Name.Equals(item.RunAsProfile, StringComparison.OrdinalIgnoreCase));
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
                LaunchAsUser(parsedExe, parsedArgs, profile, item.Label);
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

            "sshpanda" or "pandashell" => (_, _) =>
            {
                try { PandaShellWindow.ShowWindow(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open PandaShell:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            "pandapassgen" => (_, _) =>
            {
                try { PandaPassGenWindow.ShowWindow(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open PandaPassGen:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            "pandalaps" => (_, _) =>
            {
                try { PandaLapsWindow.ShowWindow(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open PandaLAPS:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            _ => (_, _) => { }
        };

    //######################################
    //Resolve password for multi-URL launches
    //######################################
    private static RunAsProfile? ResolveProfilePassword(RunAsProfile? profile)
    {
        if (profile == null) return null;

        if (!string.IsNullOrEmpty(profile.Password)) return profile;

        var (ok, entered) = PromptForPassword(profile.Username);
        if (!ok) return null;

        return new RunAsProfile
        {
            Name     = profile.Name,
            Username = profile.Username,
            Password = entered
        };
    }

    //######################################
    //URL - Standard
    //######################################
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

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    //######################################
    //URL as a different user
    //######################################
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
                    "PandaTools – Browser Not Found",
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

    //######################################
    //Incognito - standard
    //######################################
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
        catch
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    //######################################
    //Incognito - as a different user
    //######################################
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

    //######################################
    //Browser resolution
    //######################################
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

    //######################################
    //ParseCommandLine
    //######################################
    private static (string exe, string args) ParseCommandLine(string value, string? extraArgs)
    {
        value = value?.Trim() ?? "";
        string exe;
        string inlineArgs;

        if (value.StartsWith("\""))
        {
            int close = value.IndexOf('"', 1);
            if (close < 0) { exe = value.Trim('"'); inlineArgs = ""; }
            else
            {
                exe = value.Substring(1, close - 1); inlineArgs = value.Substring(close + 1).Trim();
            }
        }
        else
        {
            exe = value; inlineArgs = "";
            int searchFrom = 0;
            while (true)
            {
                int space = value.IndexOf(' ', searchFrom);
                if (space < 0) break;
                var candidate = value.Substring(0, space);

                if (File.Exists(candidate))
                {
                    exe = candidate; inlineArgs = value.Substring(space + 1).Trim();
                    break;
                }

                searchFrom = space + 1;
            }
        }

        var combined = string.IsNullOrWhiteSpace(extraArgs)
            ? inlineArgs
            : string.IsNullOrWhiteSpace(inlineArgs) ? extraArgs : $"{inlineArgs} {extraArgs}";

        return (exe, combined);
    }

    //######################################
    //Main RunAs launcher
    //######################################
    private static void LaunchAsUser(string target, string arguments, RunAsProfile? profile, string appName = "")
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

            var (domain, user) = SplitDomainUser(profile.Username);

            if (string.IsNullOrEmpty(profile.Password))
            {
                HandlePasswordLoop(resolvedExe, resolvedArgs, user, domain, profile,
                    firstAttempt: true, offerSave: false, appName: appName);
                return;
            }

            TryLaunch(resolvedExe, resolvedArgs, user, domain,
                ToSecureString(profile.Password), profile, appName);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex)
        {
            MessageBox.Show($"RunAs failed:\n{ex.Message}",
                "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    //######################################
    //Launch with saved password, hand off to loop on 1326
    //######################################
    private static void TryLaunch(
        string resolvedExe, string resolvedArgs,
        string user, string domain,
        SecureString password, RunAsProfile? profile, string appName = "")
    {
        try
        {
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
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1326)
        {
            HandlePasswordLoop(resolvedExe, resolvedArgs, user, domain, profile,
                firstAttempt: false, offerSave: true, appName: appName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"RunAs failed:\n{ex.Message}",
                "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    //######################################
    //Retry loop - prompts until correct password or cancel
    //######################################
    private static void HandlePasswordLoop(
        string resolvedExe, string resolvedArgs,
        string user, string domain,
        RunAsProfile? profile, bool firstAttempt, bool offerSave, string appName = "")
    {
        var profileName = profile?.Name ?? user;
        var username    = profile?.Username ?? user;
        bool isFirst    = firstAttempt;

        while (true)
        {
            var promptMsg = isFirst
                ? $"Enter password for \"{profileName}\""
                : $"Incorrect password for \"{profileName}\".\n\nPlease try again:";

            isFirst = false;

            var (ok, plainText) = PromptForPassword(username, promptMsg, appName);
            if (!ok) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = resolvedExe,
                    Arguments       = resolvedArgs,
                    UseShellExecute = false,
                    UserName        = user,
                    Domain          = domain,
                    Password        = ToSecureString(plainText),
                    LoadUserProfile = true
                });

                if (offerSave && profile != null)
                {
                    if (MessageBox.Show(
                            $"Password updated successfully for \"{profileName}\".\n\nSave the new password?",
                            "Save Password?",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        SaveProfilePassword(profile.Name, plainText);
                }
                return;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return; }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1326) { }
            catch (Exception ex)
            {
                MessageBox.Show($"RunAs failed:\n{ex.Message}",
                    "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }

    //######################################
    //Save updated password back to config via DPAPI
    //######################################
    private static void SaveProfilePassword(string profileName, string plainPassword)
    {
        var cfg   = ConfigLoader.AppConfig;
        var match = cfg.RunAsProfiles.FirstOrDefault(p =>
            p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;
        match.Password = plainPassword;
        ConfigLoader.Save(cfg);
    }

    //######################################
    //Split DOMAIN\user or user@domain
    //######################################
    private static (string domain, string user) SplitDomainUser(string username)
    {
        if (username.Contains('\\'))
        {
            var parts = username.Split('\\', 2);
            return (parts[0], parts[1]);
        }

        if (username.Contains('@'))
            return (".", username);

        return (Environment.MachineName, username);
    }

    //######################################
    //Resolve any file type to a launchable exe + args
    //######################################
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

    //######################################
    //Resolve .lnk shortcut
    //######################################
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
        catch
        {
            return ("", "");
        }
    }

    //######################################
    //Password prompt with show/hide toggle
    //######################################
    private static (bool ok, string password) PromptForPassword(string username, string? message = null, string appName = "")
    {
        var title = string.IsNullOrWhiteSpace(appName)
            ? "Password Prompt"
            : $"{appName}";

        const int pad     = 16;
        const int formW   = 400;
        const int innerW  = formW - pad * 2 - 16;
        const int toggleW = 32;
        const int btnW    = 80;
        const int btnGap  = 8;

        const int msgTop  = 16;
        const int msgH    = 36;
        const int userTop = msgTop + msgH + 8;
        const int passTop = userTop + 26;
        const int btnTop  = passTop + 34;
        const int btnH    = 28;
        const int formH   = btnTop + btnH + 48;

        //Right-align both buttons with consistent gap between them
        const int btnCancelLeft = formW - pad - 16 - btnW;
        const int btnOkLeft     = btnCancelLeft - btnGap - btnW;

        var frm = new Form
        {
            Text            = title,
            Size            = new System.Drawing.Size(formW, formH),
            StartPosition   = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox     = false,
            MinimizeBox     = false,
            TopMost         = true,
            Font            = new System.Drawing.Font("Segoe UI", 9f),
            Icon            = AppIcon.Get()
        };

        frm.Controls.Add(new Label
        {
            Text     = message ?? $"Enter password for \"{username}\"",
            Left     = pad, Top = msgTop,
            Width    = innerW, Height = msgH,
            Font     = new System.Drawing.Font("Segoe UI", 9f),
            AutoSize = false
        });

        frm.Controls.Add(new Label
        {
            Text      = "Username:",
            Left      = pad, Top = userTop,
            Width     = 74, Height = 20,
            ForeColor = System.Drawing.Color.DimGray,
            Font      = new System.Drawing.Font("Segoe UI", 9f),
            AutoSize  = false
        });

        frm.Controls.Add(new Label
        {
            Text     = username,
            Left     = pad + 76, Top = userTop,
            Width    = innerW - 76, Height = 20,
            Font     = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
            AutoSize = false
        });

        var txtPass = new TextBox
        {
            Left                  = pad, Top = passTop,
            Width                 = innerW - toggleW - 4,
            UseSystemPasswordChar = true,
            Font                  = new System.Drawing.Font("Segoe UI", 9f)
        };
        frm.Controls.Add(txtPass);

        var btnToggle = new Button
        {
            Text      = "👁",
            Left      = pad + innerW - toggleW, Top = passTop - 1,
            Width     = toggleW, Height = txtPass.Height + 2,
            FlatStyle = FlatStyle.Flat,
            Font      = new System.Drawing.Font("Segoe UI", 9f),
            TabStop   = false,
            Cursor    = Cursors.Hand
        };
        btnToggle.FlatAppearance.BorderSize = 1;
        btnToggle.Click += (_, _) =>
        {
            txtPass.UseSystemPasswordChar = !txtPass.UseSystemPasswordChar;
            txtPass.Focus();
            txtPass.SelectionStart = txtPass.Text.Length;
        };
        frm.Controls.Add(btnToggle);

        var btnOk = new Button
        {
            Text         = "OK",
            Left         = btnOkLeft, Top = btnTop,
            Width        = btnW, Height = btnH,
            DialogResult = DialogResult.OK,
            Font         = new System.Drawing.Font("Segoe UI", 9f)
        };

        var btnCancel = new Button
        {
            Text         = "Cancel",
            Left         = btnCancelLeft, Top = btnTop,
            Width        = btnW, Height = btnH,
            DialogResult = DialogResult.Cancel,
            Font         = new System.Drawing.Font("Segoe UI", 9f)
        };

        frm.Controls.AddRange(new System.Windows.Forms.Control[] { btnOk, btnCancel });
        frm.AcceptButton = btnOk;
        frm.CancelButton = btnCancel;
        frm.Shown += (_, _) => txtPass.Focus();

        var dlg = frm.ShowDialog();
        var pwd = txtPass.Text;
        frm.Dispose();
        return (dlg == DialogResult.OK, pwd);
    }

    //######################################
    //SecureString helper
    //######################################
    private static SecureString ToSecureString(string input)
    {
        var s = new SecureString();
        foreach (var c in input) s.AppendChar(c);
        s.MakeReadOnly();
        return s;
    }
}