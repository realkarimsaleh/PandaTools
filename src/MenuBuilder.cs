using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public static class MenuBuilder
{
    public static ContextMenuStrip Build(Action onExit)
    {
        var menu    = new ContextMenuStrip();
        var flavour = ConfigLoader.FlavourConfig;
        var cfg     = ConfigLoader.AppConfig;

        // Flavour name at top (non-clickable)
        menu.Items.Add(new ToolStripMenuItem(cfg.Flavour) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        // Dynamic menu from flavour JSON
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

        // Footer
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("⚙️ Settings", null, (_, _) =>
        {
            try
            {
                using var w = new SettingsWindow();
                w.ShowDialog();
            }
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

    private static EventHandler ResolveHandler(FlavourItem item) =>
        item.Type.ToLowerInvariant() switch
        {
            "url" => (_, _) =>
                Process.Start(new ProcessStartInfo(item.Value) { UseShellExecute = true }),

            // 🕶️ Incognito — uses configured browser OR PowerShell fallback
            "incognito" => (_, _) =>
            {
                var cfg = ConfigLoader.AppConfig;
                OpenIncognito(item.Value, cfg.BrowserName, cfg.BrowserPath);
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
                Process.Start(new ProcessStartInfo(item.Value) { UseShellExecute = true }),

            "script" => async (_, _) =>
            {
                try 
                { 
                    await GitLabScriptRunner.RunAsync(item.ProjectId, item.FilePath, item.Branch); 
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Script error:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            _ => (_, _) => { }
        };

    private static void OpenIncognito(string url, string browserName, string customPath)
    {
        try
        {
            string exePath;
            string args;

            switch (browserName.ToLowerInvariant())
            {
                case "chrome":
                    exePath = FindChrome();
                    args = $"--incognito \"{url}\"";
                    break;
                case "edge":
                    exePath = FindEdge();
                    args = $"--inprivate \"{url}\"";
                    break;
                case "firefox":
                    exePath = FindFirefox();
                    args = $"-private-window \"{url}\"";
                    break;
                case "brave":
                    exePath = FindBrave();
                    args = $"--incognito \"{url}\"";
                    break;
                case "custom":
                    exePath = customPath;
                    args = $"--incognito \"{url}\"";
                    break;
                default:
                    exePath = "";
                    args = "";
                    break;
            }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = exePath,
                    Arguments       = args,
                    UseShellExecute = true
                });
                return;
            }

            // Universal fallback — PowerShell private window
            Process.Start(new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -Command \"Start-Process '{url}' -WindowStyle Private\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // Final fallback
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private static string FindChrome() =>
        new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
        }.FirstOrDefault(File.Exists) ?? "";

    private static string FindEdge() =>
        new[]
        {
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
        }.FirstOrDefault(File.Exists) ?? "";

    private static string FindFirefox() =>
        new[]
        {
            @"C:\Program Files\Mozilla Firefox\firefox.exe",
            @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"
        }.FirstOrDefault(File.Exists) ?? "";

    private static string FindBrave() =>
        new[]
        {
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
            @"C:\Program Files (x86)\BraveSoftware\Brave-Browser\Application\brave.exe"
        }.FirstOrDefault(File.Exists) ?? "";
}
