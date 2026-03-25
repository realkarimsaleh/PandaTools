using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
            "url" or "incognito" => (_, _) =>
            {
                var cfg = ConfigLoader.AppConfig;
                var urls = item.Values.Count > 0 ? item.Values : new List<string> { item.Value };
                bool isIncognito = item.Type.Equals("incognito", StringComparison.OrdinalIgnoreCase);

                string browserName = isIncognito ? cfg.BrowserName : cfg.UrlBrowserName;
                string browserPath = isIncognito ? cfg.BrowserPath : cfg.UrlBrowserPath;

                RunAsProfile? resolvedProfile = null;
                if (!string.IsNullOrWhiteSpace(item.RunAsProfile))
                {
                    var profile = cfg.RunAsProfiles.FirstOrDefault(p =>
                        p.Name.Equals(item.RunAsProfile, StringComparison.OrdinalIgnoreCase));
                    
                    resolvedProfile = ResolveProfilePassword(profile);
                    //User cancelled password prompt
                    if (resolvedProfile == null) return;
                }

                LaunchURL.Open(urls, browserName, browserPath, isIncognito, resolvedProfile);
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

        var (ok, entered) = CredentialPrompt.Show(profile.Username, $"Enter password for \"{profile.Username}\"");
        if (!ok) return null;

        return new RunAsProfile
        {
            Name     = profile.Name,
            Username = profile.Username,
            Password = entered
        };
    }

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
        var (resolvedExe, resolvedArgs) = ResolveToExecutable(target, arguments);
        CredentialPrompt.LaunchWithRunAs(resolvedExe, resolvedArgs, profile, appName);
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
}