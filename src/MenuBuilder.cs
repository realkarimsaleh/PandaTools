using System.Windows.Forms;
using System.Diagnostics;

public static class MenuBuilder
{
    public static ContextMenuStrip Build(Action onExit)
    {
        var menu    = new ContextMenuStrip();
        var flavour = ConfigLoader.FlavourConfig;
        var cfg     = ConfigLoader.AppConfig;

        // ── Dynamic menu from flavour JSON ────────────────────────────
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

        // ── Fixed footer ──────────────────────────────────────────────
        menu.Items.Add(new ToolStripSeparator());

        // Switch Flavour submenu
        var flavourMenu = new ToolStripMenuItem("🎨 Switch Flavour");
        foreach (var name in ConfigLoader.GetAvailableFlavours())
        {
            var n  = name;
            var fi = new ToolStripMenuItem(n)
            {
                Checked = n.Equals(cfg.Flavour, StringComparison.OrdinalIgnoreCase)
            };
            fi.Click += (_, _) => ConfigLoader.SetFlavour(n);
            flavourMenu.DropDownItems.Add(fi);
        }
        menu.Items.Add(flavourMenu);

        menu.Items.Add("🔄 Reload Configs", null,
            (_, _) => ConfigLoader.Reload());

        // ⚙️ SETTINGS BUTTON — launches the full settings window
        menu.Items.Add("⚙️ Settings", null, (_, _) =>
        {
            using var settings = new SettingsWindow();
            var ctx = Application.OpenForms.OfType<TrayContext>().FirstOrDefault();
            if (ctx != null)
            {
                settings.ShowDialog();
                ctx.RebuildMenu();  // Refresh after settings changes
            }
        });

        menu.Items.Add("⬆️ Check for Updates", null,
            async (_, _) => await Updater.CheckAsync(silent: false));

        var diagItem = new ToolStripMenuItem("🔍 Diagnostics")
            { Checked = cfg.Diagnostics };
        diagItem.Click += (_, _) => ConfigLoader.ToggleDiagnostics();
        menu.Items.Add(diagItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("❌ Exit", null, (_, _) => onExit());

        return menu;
    }

    private static EventHandler ResolveHandler(FlavourItem item) =>
        item.Type.ToLowerInvariant() switch
        {
            "url" => (_, _) =>
                Process.Start(new ProcessStartInfo(item.Value) { UseShellExecute = true }),

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
                    MessageBox.Show($"Script runner error:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            _ => (_, _) => { }
        };
}
