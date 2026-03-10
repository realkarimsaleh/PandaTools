using System.Text.Json.Serialization;
using System.Collections.Generic;

public class AppConfig
{
    [JsonPropertyName("url_server")]
    public string UrlServer { get; set; } = "https://gitlab.leedsbeckett.ac.uk";

    [JsonPropertyName("flavour")]
    public string Flavour { get; set; } = "LBU-DS-ServiceDesk";

    // Legacy key/token file fields retained for backwards compat
    [JsonPropertyName("keyFile")]
    public string KeyFile { get; set; } = "";

    [JsonPropertyName("tokenFile")]
    public string TokenFile { get; set; } = "";

    // DPAPI-encrypted token — this is what TokenManager stores/reads
    // PLATFORM NOTE: DPAPI is Windows-only. For macOS port replace with Keychain.
    [JsonPropertyName("token_encrypted")]
    public string TokenEncrypted { get; set; } = "";

    [JsonPropertyName("diagnostics")]
    public bool Diagnostics { get; set; } = false;

    [JsonPropertyName("manual_mode")]
    public bool ManualMode { get; set; } = false;

    [JsonPropertyName("flavour_project_id")]
    public int FlavourProjectId { get; set; } = 0;

    [JsonPropertyName("flavour_repo_path")]
    public string FlavourRepoPath { get; set; } = "flavours";

    [JsonPropertyName("flavour_poll_seconds")]
    public int FlavourPollSeconds { get; set; } = 300;

    [JsonPropertyName("browser_name")]
    public string BrowserName { get; set; } = "default";

    [JsonPropertyName("browser_path")]
    public string BrowserPath { get; set; } = "";

    // ── Default RunAs profiles — edit these before compiling ──────────
    [JsonPropertyName("runas_profiles")]
    public List<RunAsProfile> RunAsProfiles { get; set; } = new()
    {
        new()
        {
            Name = "Workstation Admin",
            Username = @"DOMAIN\Username",
            Password = ""
        }
    };
}

public class RunAsProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    public override string ToString() => Name;
}

public class FlavourConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; } = false;

    [JsonPropertyName("menu")]
    public List<FlavourSection> Menu { get; set; } = new();
}

public class FlavourSection
{
    [JsonPropertyName("section")]
    public string Section { get; set; } = "";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "";

    [JsonPropertyName("items")]
    public List<FlavourItem> Items { get; set; } = new();
}

public class FlavourItem
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    // url | incognito | app | runas | script | powershell | exe

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";

    [JsonPropertyName("runas_profile")]
    public string RunAsProfile { get; set; } = "";

    [JsonPropertyName("admin")]
    public bool Admin { get; set; } = false;

    [JsonPropertyName("projectId")]
    public int ProjectId { get; set; } = 0;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = "";

    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "main";
}
