using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

//Bookmarks are stored inside config.json under the "pandashell_bookmarks" key via AppConfig.

public class PandaShellBookmark
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 22;

    [JsonPropertyName("account_mode")]
    //LAPS | Manual | RunAs
    public string AccountMode { get; set; } = "manual";

    [JsonPropertyName("runas_name")]
    public string RunAsName { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("include_domain")]
    public bool IncludeDomain { get; set; } = false;
}

public static class PandaShellBookmarkStore
{
    //######################################
    //Load from AppConfig (which reads config.json)
    //######################################
    public static List<PandaShellBookmark> Load() =>
        ConfigLoader.AppConfig.PandaShellBookmarks;

    //######################################
    //Save back into AppConfig and persist to config.json
    //######################################
    public static void Save(List<PandaShellBookmark> items)
    {
        var cfg = ConfigLoader.AppConfig;
        cfg.PandaShellBookmarks = items;
        ConfigLoader.Save(cfg);
    }
}