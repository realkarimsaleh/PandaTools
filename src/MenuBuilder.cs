using System.Windows.Forms;
using System.Diagnostics;

public static class MenuBuilder
{
    public static ContextMenuStrip Build(Action onExit)
    {
        var menu = new ContextMenuStrip();

        //  Build submenus dynamically from MenuConfig - edit MenuConfig.cs to add shortcuts
        foreach (var section in MenuConfig.Sections)
        {
            var submenu = new ToolStripMenuItem(section.Label);

            foreach (var item in section.Items)
            {
                submenu.DropDownItems.Add(item.Label, null, ResolveAction(item));
            }

            menu.Items.Add(submenu);
        }

        //  Bottom fixed items - always present regardless of config
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("🔄 Check for Updates", null, async (s, e) =>
            await Updater.CheckAsync(silent: false));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("❌ Exit", null, (s, e) => onExit());

        return menu;
    }

    //  ResolveAction
    //  Maps each MenuItemType to its corresponding action at click time
    private static EventHandler ResolveAction(MenuItem item) => item.Type switch
    {
        //  Url — opens in default browser
        MenuItemType.Url => (s, e) =>
            Process.Start(new ProcessStartInfo(item.Action) { UseShellExecute = true }),

        //  PowerShell — runs command silently with no window
        MenuItemType.PowerShell => (s, e) =>
            Process.Start(new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -Command \"{item.Action}\"",
                UseShellExecute = false,
                CreateNoWindow  = true
            }),

        //  Exe - launches app or opens folder via shell
        MenuItemType.Exe => (s, e) =>
            Process.Start(new ProcessStartInfo(item.Action) { UseShellExecute = true }),

        //  GitLabScript - fetches latest .ps1 from GitLab and runs it
        //MenuItemType.GitLabScript => (s, e) =>
        //    _ = GitLabScriptRunner.RunAsync(item.ProjectId, item.ScriptPath, item.Branch),

        MenuItemType.GitLabScript => async (s, e) =>
        {
            try
            {
                await GitLabScriptRunner.RunAsync(item.ProjectId, item.ScriptPath, item.Branch);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Script runner error:\n{ex.Message}\n\n{ex.StackTrace}",
                    "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        },


        //  Fallback - do nothing
        _ => (s, e) => { }
    };
}
