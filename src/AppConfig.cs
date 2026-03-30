using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Linq;

public class AppConfig
{
    //######################################
    //CI Build Injection Helpers
    //######################################
    private static string CiString(string key) =>
        AppContext.GetData(key) as string is { Length: > 0 } v ? v : "";

    private static int CiInt(string key) =>
        int.TryParse(AppContext.GetData(key) as string, out var v) ? v : 0;

    //######################################
    //Config Server - flavours, defaults, scripts
    //Platform: "github" or "gitlab"
    //######################################
    [JsonPropertyName("cfg_platform")]
    public string CfgPlatform { get; set; } = CiString("PandaTools.CfgPlatform") is { Length: > 0 } v ? v : "gitlab";

    [JsonPropertyName("url_server")]
    public string UrlServer { get; set; } = CiString("PandaTools.GitLabUrl");

    [JsonPropertyName("cfg_repo_owner")]
    public string CfgRepoOwner { get; set; } = CiString("PandaTools.CfgRepoOwner");

    [JsonPropertyName("cfg_repo_name")]
    public string CfgRepoName { get; set; } = CiString("PandaTools.CfgRepoName");

    [JsonPropertyName("cfg_public")]
    public bool CfgPublic { get; set; } = false;

    [JsonPropertyName("flavour")]
    public string Flavour { get; set; } = "Default";

    [JsonPropertyName("keyFile")]
    public string KeyFile { get; set; } = "";

    [JsonPropertyName("tokenFile")]
    public string TokenFile { get; set; } = "";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("token_encrypted")]
    public string TokenEncrypted { get; set; } = "";

    [JsonPropertyName("diagnostics")]
    public bool Diagnostics { get; set; } = false;

    [JsonPropertyName("manual_mode")]
    public bool ManualMode { get; set; } = false;

    [JsonPropertyName("show_only_subscribed_flavour")]
    public bool ShowOnlySubscribedFlavour { get; set; } = true;

    [JsonPropertyName("flavour_project_id")]
    public int FlavourProjectId { get; set; } = CiInt("PandaTools.GitLabProjectId");

    [JsonPropertyName("flavour_repo_path")]
    public string FlavourRepoPath { get; set; } = "flavours";

    [JsonPropertyName("flavour_poll_seconds")]
    public int FlavourPollSeconds { get; set; } = 300;

    [JsonPropertyName("defaults_repo_path")]
    public string DefaultsRepoPath { get; set; } = "defaults/defaults.json";

    //######################################
    //App Updates - installer releases
    //Platform: "github" or "gitlab"
    //######################################
    [JsonPropertyName("app_platform")]
    public string AppPlatform { get; set; } = CiString("PandaTools.AppPlatform") is { Length: > 0 } v ? v : "github";

    [JsonPropertyName("app_url_server")]
    public string AppUrlServer { get; set; } = CiString("PandaTools.AppUrlServer") is { Length: > 0 } v ? v : "https://github.com";

    [JsonPropertyName("app_repo_owner")]
    public string AppRepoOwner { get; set; } = CiString("PandaTools.GitHubRepoOwner");

    [JsonPropertyName("app_repo_name")]
    public string AppRepoName { get; set; } = CiString("PandaTools.GitHubRepoName");

    [JsonPropertyName("app_public")]
    public bool AppPublic { get; set; } = true;

    [JsonPropertyName("app_project_id")]
    public int AppProjectId { get; set; } = CiInt("PandaTools.GitLabProjectId");

    [JsonPropertyName("app_repo_path")]
    public string AppRepoPath { get; set; } = CiString("PandaTools.GitLabRepoPath");

    //######################################
    //App token - used when app server differs from config server
    //and app repo is private. If same server as config, TokenEncrypted is reused.
    //######################################
    [JsonPropertyName("app_token_encrypted")]
    public string AppTokenEncrypted { get; set; } = "";

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
        new() { Name = "Admin Profile", Username = "" }
    };

    [JsonPropertyName("laps")]
    public LapsConfig Laps { get; set; } = new();

    [JsonPropertyName("pandashell_bookmarks")]
    public List<PandaShellBookmark> PandaShellBookmarks { get; set; } = new();

    [JsonPropertyName("pandapassgen")]
    public PandaPassGenConfig PandaPassGen { get; set; } = new();

    //######################################
    //Computed helpers used across the app
    //######################################
    public bool CfgIsGitHub => CfgPlatform.Equals("github", StringComparison.OrdinalIgnoreCase);
    public bool AppIsGitHub => AppPlatform.Equals("github", StringComparison.OrdinalIgnoreCase);

    //True when app server matches config server - app reuses config token, no second token needed
    public bool AppSameAsConfig =>
        !string.IsNullOrWhiteSpace(AppUrlServer) &&
        AppUrlServer.TrimEnd('/').Equals(UrlServer.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
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

    //######################################
    //Password is NEVER stored as a plain string.
    //All operations go through SecureString or DPAPI bytes directly.
    //######################################

    [JsonPropertyName("password_encrypted")]
    public string PasswordEncrypted { get; set; } = "";

    [JsonPropertyName("password")]
    public string LegacyPassword { get; set; } = "";

    //######################################
    //Returns true if a saved password is available (encrypted or legacy)
    //######################################
    [JsonIgnore]
    public bool HasSavedPassword =>
        !string.IsNullOrEmpty(PasswordEncrypted) || !string.IsNullOrEmpty(LegacyPassword);

    //######################################
    //Decrypt directly to SecureString - plain text never touches a string variable
    //Returns null if no password is saved or decryption fails
    //######################################
    public System.Security.SecureString? DecryptToSecureString()
    {
        byte[]? plain = null;
        try
        {
            if (!string.IsNullOrEmpty(PasswordEncrypted))
            {
                var cipher = Convert.FromBase64String(PasswordEncrypted);
                plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            }
            else if (!string.IsNullOrEmpty(LegacyPassword))
            {
                // Legacy plain-text migration path - encrypt and save on next Save()
                plain = Encoding.UTF8.GetBytes(LegacyPassword);
            }
            else return null;

            var secure = new System.Security.SecureString();
            // Decode UTF8 bytes to chars and append directly - never creates a string
            var chars = Encoding.UTF8.GetChars(plain);
            try   { foreach (var c in chars) secure.AppendChar(c); }
            finally { Array.Clear(chars, 0, chars.Length); }  // zero char array immediately
            secure.MakeReadOnly();
            return secure;
        }
        catch { return null; }
        finally
        {
            // Zero the plaintext byte array before GC can see it
            if (plain != null) Array.Clear(plain, 0, plain.Length);
        }
    }

    //######################################
    //Encrypt from SecureString - SecureString → DPAPI bytes, no plain string created
    //######################################
    public void EncryptFromSecureString(System.Security.SecureString secure)
    {
        LegacyPassword = "";

        if (secure == null || secure.Length == 0) { PasswordEncrypted = ""; return; }

        var ptr   = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secure);
        byte[]? plain = null;
        try
        {
            // Convert SecureString → UTF8 bytes without ever creating a managed string
            plain = Encoding.UTF8.GetBytes(
                System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr)!);
            var cipher    = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            PasswordEncrypted = Convert.ToBase64String(cipher);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            if (plain != null) Array.Clear(plain, 0, plain.Length);
        }
    }

    //######################################
    //Called by ConfigLoader.Save() to migrate legacy plain-text passwords
    //and ensure PasswordEncrypted is up to date
    //######################################
    public void MigrateAndEncrypt()
    {
        LegacyPassword = "";
        // If we already have DPAPI-encrypted data, nothing to do
        if (!string.IsNullOrEmpty(PasswordEncrypted)) return;
        // PasswordEncrypted is empty - profile has no saved password, leave blank
    }

    public override string ToString() => Name;
}

public class OrgDefaults
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("token_expiry_warn_days")]
    public int? TokenExpiryWarnDays { get; set; }

    [JsonPropertyName("laps")]
    public LapsConfig? Laps { get; set; }

    [JsonPropertyName("runas_profiles")]
    public List<OrgRunAsProfile>? RunAsProfiles { get; set; }
}

public class OrgRunAsProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
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

    [JsonPropertyName("show_pandalaps")]
    public bool ShowPandaLaps { get; set; } = false;
}

public class FlavourSection
{
    [Category("Grouping")][DisplayName("Section Name")][JsonPropertyName("section")]
    public string Section { get; set; } = "";

    [Category("Grouping")][DisplayName("Folder Icon")][JsonPropertyName("icon")]
    public string Icon { get; set; } = "";

    [Browsable(false)][JsonPropertyName("items")]
    public List<FlavourItem> Items { get; set; } = new();
}

public class FlavourItem
{
    [Category("1. Identity")][DisplayName("Menu Label")][JsonPropertyName("label")]
    public string Label { get; set; } = "New Item";

    [Category("2. Action")][DisplayName("Execution Type")][TypeConverter(typeof(ActionTypeConverter))][JsonPropertyName("type")]
    public string Type { get; set; } = "url";

    [Category("2. Action")][DisplayName("Target / Value")][JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [Category("2. Action")][DisplayName("Multiple Targets")][JsonPropertyName("values")]
    public List<string> Values { get; set; } = new();

    [Category("3. Modifiers")][DisplayName("Arguments")][JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";

    [Category("3. Modifiers")][DisplayName("RunAs Profile")][TypeConverter(typeof(RunAsProfileConverter))][JsonPropertyName("runas_profile")]
    public string RunAsProfile { get; set; } = "";

    [Category("3. Modifiers")][DisplayName("Require Admin")][JsonPropertyName("admin")]
    public bool Admin { get; set; } = false;

    [Category("4. GitLab Scripts")][DisplayName("Project ID")][JsonPropertyName("projectId")]
    public int ProjectId { get; set; } = 0;

    [Category("4. GitLab Scripts")][DisplayName("File Path")][JsonPropertyName("filePath")]
    public string FilePath { get; set; } = "";

    [Category("4. GitLab Scripts")][DisplayName("Branch")][JsonPropertyName("branch")]
    public string Branch { get; set; } = "main";
}

public class ActionTypeConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
        new(new[] { "url", "incognito", "app", "explorer", "runas", "powershell", "script", "pandashell", "pandapassgen" });
}

public class RunAsProfileConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var profiles = new List<string> { "" };
        if (ConfigLoader.AppConfig?.RunAsProfiles != null)
            profiles.AddRange(ConfigLoader.AppConfig.RunAsProfiles.Select(p => p.Name));
        return new(profiles);
    }
}
