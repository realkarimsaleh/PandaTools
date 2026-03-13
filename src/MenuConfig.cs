public enum MenuItemType { Url, PowerShell, Exe, GitLabScript }

//  MenuItem
//  Label      : text shown in the menu
//  Type       : what happens when clicked (see MenuItemType above)
//  Action     : URL, PowerShell command, or .exe path
//  ProjectId  : GitLab project ID - only required for GitLabScript items
//  ScriptPath : path to .ps1 inside the GitLab repo - only required for GitLabScript items
//  Branch     : which branch to pull from - defaults to main
public record MenuItem(
    string       Label,
    MenuItemType Type,
    string       Action     = "",
    int          ProjectId  = 0,
    string       ScriptPath = "",
    string       Branch     = "main"
);

public record MenuSection(string Label, List<MenuItem> Items);

public static class MenuConfig
{
    public static readonly List<MenuSection> Sections = new()
    {
        //Web Tools, internal dashboards and portals
        new("🌐 Web Tools", new()
        {
            new("UniFi Controller", MenuItemType.Url, "https://unifi.ui.com"),
        }),

        //Local Scripts. PowerShell commands run locally
        new("⚡ Local Scripts", new()
        {
            new("Flush DNS",        MenuItemType.PowerShell, "ipconfig /flushdns"),
            new("Restart Wi-Fi",    MenuItemType.PowerShell, "Restart-NetAdapter -Name 'Wi-Fi' -Confirm:$false"),
        }),

        //GitLab Scripts - fetched fresh from GitLab on click
        //ProjectId  : found in each repo under Settings > General
        //ScriptPath : path to the .ps1 relative to the repo root
        new("☁️ GitLab Scripts", new()
        {
            new("Panopto Delta Informant",
                MenuItemType.GitLabScript,
                ProjectId:  522,
                ScriptPath: "PowershePanoptoDeltaInformant/PanoptoDeltaInformant.ps1"),

            new("SCCM Collection Tool",
                MenuItemType.GitLabScript,
                ProjectId:  525,
                ScriptPath: "SCCMCollectionMembership-Utility.ps1"),
        }),

        //Applications - local app shortcuts
        new("🗂️ Applications", new()
        {
            new("Notepad++",        MenuItemType.Exe, @"C:\Program Files\Notepad++\notepad++.exe"),
        }),
    };
}
