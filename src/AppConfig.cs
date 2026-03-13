using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Collections.Generic;

public class AppConfig
{
    [JsonPropertyName("url_server")]
    public string UrlServer { get; set; } = "https://gitlab.leedsbeckett.ac.uk";

    [JsonPropertyName("flavour")]
    public string Flavour { get; set; } = "LBU-DS-ServiceDesk";

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
}

public class RunAsProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
    
    //######################################
    //In-memory plain text only, never written to disk
    //######################################
    [JsonIgnore]
    public string Password { get; set; } = "";

    //######################################
    //DPAPI-encrypted blob, what actually gets stored in JSON
    //######################################
    [JsonPropertyName("password_encrypted")]
    public string PasswordEncrypted { get; set; } = "";

    //######################################
    //Legacy plain-text field, read-only for one-time migration
    //######################################
    //Once migrated, ConfigLoader.Save() will clear this automatically.
    [JsonPropertyName("password")]
    public string LegacyPassword { get; set; } = "";

    ///<summary>
    ///Fills <see cref="Password"/> from <see cref="PasswordEncrypted"/>.
    ///Falls back to <see cref="LegacyPassword"/> for existing configs (auto-migrated on next save).
    ///</summary>
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
            // Migrate: promote plain text to in-memory; ConfigLoader.Save() will encrypt.
            Password = LegacyPassword;
        }
    }

    ///<summary>
    ///Encrypts <see cref="Password"/> into <see cref="PasswordEncrypted"/> and clears
    ///<see cref="LegacyPassword"/> so plain text is never written back.
    ///</summary>
    public void EncryptPassword()
    {
        LegacyPassword = ""; // always clear legacy on save

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
    // url | incognito | app | runas | script | powershell | exe | explorer

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
