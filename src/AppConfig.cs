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
    //Reads values baked in at compile time by the GitLab CI pipeline.
    //On a public or local build these return "" or 0 — no LBU values ship by default.
    //config.json always wins over these if it already exists — first-run defaults only.
    //######################################
    private static string CiString(string key) =>
        AppContext.GetData(key) as string is { Length: > 0 } v ? v : "";

    private static int CiInt(string key) =>
        int.TryParse(AppContext.GetData(key) as string, out var v) ? v : 0;

    [JsonPropertyName("url_server")]
    public string UrlServer { get; set; } = CiString("PandaTools.GitLabUrl");

    [JsonPropertyName("flavour")]
    public string Flavour { get; set; } = "";

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

    [JsonPropertyName("app_project_id")]
    public int AppProjectId { get; set; } = CiInt("PandaTools.GitLabProjectId");

    [JsonPropertyName("app_repo_path")]
    public string AppRepoPath { get; set; } = CiString("PandaTools.GitLabRepoPath");

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
        new() { Name = "Admin Profile", Username = "", Password = "" }
    };

    //######################################
    //Global Universal LAPS Configuration
    //######################################
    [JsonPropertyName("laps")]
    public LapsConfig Laps { get; set; } = new();

    //######################################
    //PandaShell Bookmarks
    //######################################
    [JsonPropertyName("pandashell_bookmarks")]
    public List<PandaShellBookmark> PandaShellBookmarks { get; set; } = new();

    //######################################
    //PandaPassGen Configuration
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

    [JsonPropertyName("show_pandalaps")]
    public bool ShowPandaLaps { get; set; } = false;
}

//######################################
//Decorated Models for Zero-Maintenance UI
//######################################

public class FlavourSection
{
    [Category("Grouping")]
    [DisplayName("Section Name")]
    [Description("The name of the folder in the menu.")]
    [JsonPropertyName("section")]
    public string Section { get; set; } = "";

    [Category("Grouping")]
    [DisplayName("Folder Icon")]
    [Description("An optional emoji or icon for the folder.")]
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "";

    [Browsable(false)]
    [JsonPropertyName("items")]
    public List<FlavourItem> Items { get; set; } = new();
}

public class FlavourItem
{
    [Category("1. Identity")]
    [DisplayName("Menu Label")]
    [Description("The text displayed in the menu.")]
    [JsonPropertyName("label")]
    public string Label { get; set; } = "New Item";

    [Category("2. Action")]
    [DisplayName("Execution Type")]
    [Description("Select the type of action this item performs from the dropdown.")]
    [TypeConverter(typeof(ActionTypeConverter))]
    [JsonPropertyName("type")]
    public string Type { get; set; } = "url";

    [Category("2. Action")]
    [DisplayName("Target / Value")]
    [Description("The URL, executable path, or command to run.")]
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [Category("2. Action")]
    [DisplayName("Multiple Targets")]
    [Description("Use this instead of 'Value' to launch multiple URLs at once.")]
    [JsonPropertyName("values")]
    public List<string> Values { get; set; } = new();

    [Category("3. Modifiers")]
    [DisplayName("Arguments")]
    [Description("Extra command-line arguments for 'app' or 'exe' types.")]
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";

    [Category("3. Modifiers")]
    [DisplayName("RunAs Profile")]
    [Description("Select a saved profile to automatically run this item as another user.")]
    [TypeConverter(typeof(RunAsProfileConverter))]
    [JsonPropertyName("runas_profile")]
    public string RunAsProfile { get; set; } = "";

    [Category("3. Modifiers")]
    [DisplayName("Require Admin")]
    [Description("If true, requests UAC elevation before launching.")]
    [JsonPropertyName("admin")]
    public bool Admin { get; set; } = false;

    [Category("4. GitLab Scripts")]
    [DisplayName("Project ID")]
    [Description("The GitLab Project ID containing the script.")]
    [JsonPropertyName("projectId")]
    public int ProjectId { get; set; } = 0;

    [Category("4. GitLab Scripts")]
    [DisplayName("File Path")]
    [Description("The exact path to the script in the repository.")]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = "";

    [Category("4. GitLab Scripts")]
    [DisplayName("Branch")]
    [Description("The repository branch to pull the script from.")]
    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "main";
}

//######################################
//DropDown Menu Converters for the UI
//######################################

public class ActionTypeConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        return new StandardValuesCollection(new[]
        {
            "url", "incognito", "app", "explorer", "runas",
            "powershell", "script", "pandashell", "pandapassgen"
        });
    }
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

        return new StandardValuesCollection(profiles);
    }
}
