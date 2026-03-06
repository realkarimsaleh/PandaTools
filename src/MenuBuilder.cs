using System.Windows.Forms;
using System.Diagnostics;

public static class MenuBuilder
{
    public static ContextMenuStrip Build(Action onExit)
    {
        var menu    = new ContextMenuStrip();
        var flavour = ConfigLoader.FlavourConfig;
        var cfg     = ConfigLoader.AppConfig;

        // ── Flavour name at top (non-clickable) ───────────────────────
        menu.Items.Add(new ToolStripMenuItem(cfg.Flavour) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

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

        // ── Footer ────────────────────────────────────────────────────
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
                try { await GitLabScriptRunner.RunAsync(item.ProjectId, item.FilePath, item.Branch); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Script error:\n{ex.Message}",
                        "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            },

            _ => (_, _) => { }
        };
}
