using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System;

public class AppConfig
{
    [JsonPropertyName("url_server")]
    public string UrlServer { get; set; } = "https://gitlab.leedsbeckett.ac.uk";

    [JsonPropertyName("flavour")]
    public string Flavour { get; set; } = "LBU-DS-ServiceDesk";

    [JsonPropertyName("keyFile")]
    public string KeyFile { get; set; } = @"C:\Windows\Build\Sync-Gitlab\K_Sync-Gitlab.txt";

    [JsonPropertyName("tokenFile")]
    public string TokenFile { get; set; } = @"C:\Windows\Build\Sync-Gitlab\C_Sync-Gitlab.txt";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

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

    [JsonPropertyName("app_project_id")]
    public int AppProjectId { get; set; } = 526;

    [JsonPropertyName("app_repo_path")]
    public string AppRepoPath { get; set; } = "service-delivery/pandatools";

    [JsonPropertyName("token_expiry_warn_days")]
    public int TokenExpiryWarnDays { get; set; } = 14;

    [JsonPropertyName("url_browser_name")]
    public string UrlBrowserName { get; set; } = "default";

    [JsonPropertyName("url_browser_path")]
    public string UrlBrowserPath { get; set; } = "";

    [JsonPropertyName("browser_name")]
    public string BrowserName { get; set; } = "default";

    [JsonPropertyName("browser_path")]
    public string BrowserPath { get; set; } = "";

    [JsonPropertyName("runas_profiles")]
    public List<RunAsProfile> RunAsProfiles { get; set; } = new()
    {
        new() { Name = "Workstation Admin", Username = @"", Password = "" }
    };

    //######################################
    // Global Universal LAPS Configuration 
    //######################################
    [JsonPropertyName("laps")]
    public LapsConfig Laps { get; set; } = new();

    //######################################
    // PandaShell Bookmarks
    //######################################
    [JsonPropertyName("pandashell_bookmarks")]
    public List<PandaShellBookmark> PandaShellBookmarks { get; set; } = new();

    //######################################
    // PandaPassGen Configuration
    //######################################
    [JsonPropertyName("pandapassgen")]
    public PandaPassGenConfig PandaPassGen { get; set; } = new();
}

public class PandaPassGenConfig
{
    [JsonPropertyName("default_length")]
    public int DefaultLength { get; set; } = 16;

    [JsonPropertyName("default_upper")]
    public int DefaultUpper { get; set; } = 2;

    [JsonPropertyName("default_numbers")]
    public int DefaultNumbers { get; set; } = 2;

    [JsonPropertyName("default_symbols")]
    public int DefaultSymbols { get; set; } = 2;

    [JsonPropertyName("default_speakeasy")]
    public bool DefaultSpeakEasy { get; set; } = true;
}

public class RunAsProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonIgnore]
    public string Password { get; set; } = "";

    [JsonPropertyName("password_encrypted")]
    public string PasswordEncrypted { get; set; } = "";

    [JsonPropertyName("password")]
    public string LegacyPassword { get; set; } = "";

    public void DecryptPassword()
    {
        if (!string.IsNullOrEmpty(PasswordEncrypted))
        {
            try
            {
                var cipher = Convert.FromBase64String(PasswordEncrypted);
                var plain  = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                Password   = Encoding.UTF8.GetString(plain);
            }
            catch { Password = ""; }
        }
        else if (!string.IsNullOrEmpty(LegacyPassword))
        {
            Password = LegacyPassword;
        }
    }

    public void EncryptPassword()
    {
        LegacyPassword = ""; 

        if (string.IsNullOrEmpty(Password))
        {
            PasswordEncrypted = "";
            return;
        }

        var plain  = Encoding.UTF8.GetBytes(Password);
        var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        PasswordEncrypted = Convert.ToBase64String(cipher);
    }

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

    [JsonPropertyName("show_pandashell")]
    public bool ShowPandaShell { get; set; } = false;

    [JsonPropertyName("show_pandapassgen")]
    public bool ShowPandaPassGen { get; set; } = false;
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
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("values")]
    public List<string> Values { get; set; } = new();

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