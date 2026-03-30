using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;

public static class ConfigLoader
{
    public static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PandaTools"
    );
    public static readonly string ConfigPath       = Path.Combine(ConfigDir, "config.json");
    public static readonly string FlavourDir       = Path.Combine(ConfigDir, "flavours");
    public static readonly string LocalFlavourPath = Path.Combine(FlavourDir, $"local_flavour_{Environment.UserName}.json");

    public static event Action? OnConfigReloaded;

    private static AppConfig?     _appConfig;
    private static FlavourConfig? _flavourConfig;
    private static FlavourConfig? _localFlavourConfig;
    private static readonly object _lock = new();
    private static System.Timers.Timer? _pollTimer;
    private static FileSystemWatcher?   _configWatcher;
    private static string _lastDefaultsHash = "";

    //######################################
    //Per-file blob SHA cache for flavour sync
    //Key   : flavour filename e.g. "LBU-DS-ServiceDesk.json"
    //Value : last seen blob SHA from the GitLab Tree API
    //######################################
    private static readonly Dictionary<string, string> _flavourHashes = new();

    private static readonly HttpClient Http = new();

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true,
        WriteIndented               = true
    };

    public static AppConfig     AppConfig          => _appConfig          ?? new AppConfig();
    public static FlavourConfig FlavourConfig      => _flavourConfig      ?? new FlavourConfig();
    public static FlavourConfig LocalFlavourConfig => _localFlavourConfig ?? new FlavourConfig();

    //######################################
    //Initial load
    //######################################
    public static void Load()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(FlavourDir);

        if (!File.Exists(ConfigPath))
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new AppConfig(), JsonOpts));

        SeedFlavoursFromExeDir();
        EnsureDefaultFlavour();
        EnsureTemplateFlavour();
        EnsureLocalFlavour();

        lock (_lock)
        {
            _appConfig          = LoadAppConfig();
            _flavourConfig      = LoadFlavourConfig(_appConfig.Flavour);
            _localFlavourConfig = LoadLocalFlavourConfig();
        }

        StartConfigWatcher();
        StartFlavourPoller();
    }

    //######################################
    //Reload
    //######################################
    public static void Reload()
    {
        lock (_lock)
        {
            _appConfig          = LoadAppConfig();
            _flavourConfig      = LoadFlavourConfig(_appConfig.Flavour);
            _localFlavourConfig = LoadLocalFlavourConfig();
        }
        OnConfigReloaded?.Invoke();
    }

    //######################################
    //Switch flavour
    //######################################
    public static void SetFlavour(string name)
    {
        var cfg = LoadAppConfig();
        cfg.Flavour = name;
        SerializeAndWrite(cfg);
        _flavourHashes.Clear();
        Reload();
        StartFlavourPoller();
    }

    //######################################
    //Toggle diagnostics
    //######################################
    public static void ToggleDiagnostics()
    {
        var cfg = LoadAppConfig();
        cfg.Diagnostics = !cfg.Diagnostics;
        SerializeAndWrite(cfg);
        Reload();
    }

    //######################################
    //Save entire AppConfig
    //######################################
    public static void Save(AppConfig cfg)
    {
        //Passwords are always DPAPI-encrypted in PasswordEncrypted - migrate any legacy remnants
        foreach (var p in cfg.RunAsProfiles) p.MigrateAndEncrypt();
        SerializeAndWrite(cfg);
        Reload();
    }

    //######################################
    //Available flavours
    //######################################
    public static string[] GetAvailableFlavours(bool includeHidden = false)
    {
        if (!Directory.Exists(FlavourDir)) return Array.Empty<string>();
        return Directory.GetFiles(FlavourDir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .Where(name =>
            {
                if (includeHidden) return true;
                try
                {
                    var json = File.ReadAllText(Path.Combine(FlavourDir, $"{name}.json"));
                    var fc   = JsonSerializer.Deserialize<FlavourConfig>(json, JsonOpts);
                    return !(fc?.Hidden ?? false);
                }
                catch { return true; }
            })
            .OrderBy(n => n)
            .ToArray();
    }

    //######################################
    //Seed from exe directory on each run (only new files)
    //######################################
    private static void SeedFlavoursFromExeDir()
    {
        var sourceFlavours = Path.Combine(AppContext.BaseDirectory, "flavours");
        if (!Directory.Exists(sourceFlavours)) return;

        foreach (var file in Directory.GetFiles(sourceFlavours, "*.json"))
        {
            var dest = Path.Combine(FlavourDir, Path.GetFileName(file));
            if (!File.Exists(dest))
                File.Copy(file, dest, overwrite: false);
        }
    }

    //######################################
    //Seed Default flavour on first run if it does not exist
    //This gives new users a working visible menu with no configuration needed.
    //Org flavours fetched from GitLab will appear alongside it once a token is set.
    //######################################
    private static void EnsureDefaultFlavour()
    {
        var path = Path.Combine(FlavourDir, "Default.json");
        if (File.Exists(path)) return;

        var defaultFlavour = new FlavourConfig
        {
            Version        = "1.0",
            Hidden         = false,
            ShowPandaShell = true,
            ShowPandaPassGen = true,
            Menu           = new()
            {
                new()
                {
                    Section = "Local Scripts",
                    Icon    = "⚡",
                    Items   = new()
                    {
                        new()
                        {
                            Label = "Flush DNS",
                            Type = "powershell",
                            Value = "ipconfig /flushdns"
                        },
                        new()
                        {
                            Label = "Restart Wi-Fi",
                            Type = "powershell",
                            Value = "Restart-NetAdapter -Name 'Wi-Fi' -Confirm:"
                        }
                    }
                },
                new()
                {
                    Section = "Web Tools",
                    Icon    = "🌐",
                    Items   = new()
                    {
                        new()
                        {
                            Label = "Google",
                            Type = "url",
                            Value = "https://www.google.com"
                        },
                        new() { Label = "Google (Incognito)", Type = "incognito", Value = "https://www.google.com" },
                    }
                },
                new()
                {
                    Section = "Applications",
                    Icon    = "🖥️",
                    Items   = new()
                    {
                        new()
                        {
                            Label = "Notepad",
                            Type = "app", 
                            Value = @"C:\Windows\System32\notepad.exe"
                        },
                        new()
                        {
                            Label = "Task Manager",
                            Type = "app",
                            Value = @"C:\Windows\System32\Taskmgr.exe"
                        },
                        new()
                        {
                            Label = "Event Viewer",
                            Type = "app", 
                            Value = @"C:\Windows\System32\eventvwr.msc"
                        }
                    }
                },
                new()
                {
                    Section = "PandaTools Utilities",
                    Icon    = "🐼",
                    Items   = new()
                    {
                        new()
                        {
                            Label = "PandaShell",
                            Type = "pandashell"
                        },
                        new()
                        {
                            Label = "PandaPassGen",
                            Type = "pandapassgen"
                        }
                    }
                }
            }
        };

        File.WriteAllText(path, JsonSerializer.Serialize(defaultFlavour, JsonOpts));
    }

    //######################################
    //Ensure _Template flavour only when no real flavours exist
    //######################################
    private static void EnsureTemplateFlavour()
    {
        var existingNonTemplate = Directory.GetFiles(FlavourDir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .Where(n => !string.Equals(n, "_Template", StringComparison.OrdinalIgnoreCase) && !n.StartsWith("local_flavour_"))
            .ToArray();

        if (existingNonTemplate.Length > 0) return;

        var path = Path.Combine(FlavourDir, "_Template.json");
        if (File.Exists(path)) return;

        var template = new FlavourConfig
        {
            Version = "1.0",
            Hidden  = true,
            Menu    = new()
            {
                new()
                {
                    Section = "Web Tools",
                    Icon    = "🌐",
                    Items   = new()
                    {
                        new()
                        {
                            Label = "Google",
                            Type = "url",
                            Value = "https://google.com"
                    },
                        new()
                        {
                            Label = "Google Incognito",
                            Type = "incognito",
                            Value = "https://google.com"
                        }
                    }
                },
                new()
                {
                    Section = "Scripts",
                    Icon    = "📜",
                    Items   = new()
                    {
                        new()
                        {
                            Label = "Example GitLab Script",
                            Type = "script",
                            ProjectId = 0, FilePath = "Scripts/Example.ps1", Branch = "main" },
                        new()
                        {
                            Label = "Example PowerShell",
                            Type = "powershell",
                            Value = "Write-Host 'Hello from PandaTools'"
                        },
                    }
                },
                new()
                {
                    Section = "Applications",
                    Icon    = "🖥️",
                    Items   = new()
                    {
                        new()
                        {
                            Label = "Notepad",
                            Type = "app",
                            Value = @"C:\Windows\System32\notepad.exe"
                        },
                        new()
                        {
                            Label = "Notepad (UAC)",
                            Type = "app",
                            Value = @"C:\Windows\System32\notepad.exe",
                            Admin = true
                        },
                    }
                },
                 new()
                {
                    Section = "PandaTools Utilities",
                    Icon    = "🐼",
                    Items   = new()
                    {
                        new()
                        {
                            Label = "PandaShell",
                            Type = "pandashell"
                        },
                        new()
                        {
                            Label = "PandaPassGen",
                            Type = "pandapassgen"
                        }
                    }
                }
            }
        };

        File.WriteAllText(path, JsonSerializer.Serialize(template, JsonOpts));
    }

    //######################################
    //Ensure Personal Local Flavour exists
    //######################################
    private static void EnsureLocalFlavour()
    {
        if (File.Exists(LocalFlavourPath)) return;
        var local = new FlavourConfig { Version = "1.0", Hidden = true, Menu = new() };
        File.WriteAllText(LocalFlavourPath, JsonSerializer.Serialize(local, JsonOpts));
    }

    //######################################
    //File loading
    //######################################
    private static AppConfig LoadAppConfig()
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<AppConfig>(
                File.ReadAllText(ConfigPath), JsonOpts) ?? new AppConfig();

            // Migrate any legacy plain-text passwords to DPAPI on load
            bool needsMigration = cfg.RunAsProfiles.Any(p =>
                !string.IsNullOrEmpty(p.LegacyPassword) && string.IsNullOrEmpty(p.PasswordEncrypted));
            if (needsMigration)
            {
                foreach (var p in cfg.RunAsProfiles) p.MigrateAndEncrypt();
                SerializeAndWrite(cfg);
            }

            return cfg;
        }
        catch { return new AppConfig(); }
    }

    private static FlavourConfig LoadFlavourConfig(string name)
    {
        var path = Path.Combine(FlavourDir, $"{name}.json");
        if (!File.Exists(path)) return new FlavourConfig();
        try { return JsonSerializer.Deserialize<FlavourConfig>(File.ReadAllText(path), JsonOpts) ?? new FlavourConfig(); }
        catch { return new FlavourConfig(); }
    }

    private static FlavourConfig LoadLocalFlavourConfig()
    {
        if (!File.Exists(LocalFlavourPath)) return new FlavourConfig();
        try { return JsonSerializer.Deserialize<FlavourConfig>(File.ReadAllText(LocalFlavourPath), JsonOpts) ?? new FlavourConfig(); }
        catch { return new FlavourConfig(); }
    }

    private static void SerializeAndWrite(AppConfig cfg) =>
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOpts));

    //######################################
    //Watch config.json for local edits
    //######################################
    private static void StartConfigWatcher()
    {
        _configWatcher?.Dispose();
        _configWatcher = new FileSystemWatcher(ConfigDir)
        {
            Filter              = "*.json",
            NotifyFilter        = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        var debounce = new System.Timers.Timer(500) { AutoReset = false };
        debounce.Elapsed += (_, _) => Reload();
        _configWatcher.Changed += (_, _) => { debounce.Stop(); debounce.Start(); };
    }

    //######################################
    //Poll GitLab for flavour folder changes and defaults
    //######################################
    private static void StartFlavourPoller()
    {
        _pollTimer?.Dispose();
        var ms = Math.Max(30, AppConfig.FlavourPollSeconds) * 1000.0;
        _pollTimer = new System.Timers.Timer(ms) { AutoReset = true };
        _pollTimer.Elapsed += async (_, _) =>
        {
            await CheckFlavourUpdateAsync();
            await CheckDefaultsUpdateAsync();
        };
        _pollTimer.Start();
    }

    //######################################
    //Prepare HTTP headers for the config server
    //GitHub: Authorization Bearer, GitLab: PRIVATE-TOKEN
    //Public repos: no auth header
    //######################################
    private static void PrepareConfigHeaders()
    {
        if (!Http.DefaultRequestHeaders.UserAgent.Any())
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");

        Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
        Http.DefaultRequestHeaders.Remove("Authorization");

        var cfg   = AppConfig;
        var token = TokenManager.GetToken();
        if (token == null || cfg.CfgPublic) return;

        if (cfg.CfgIsGitHub)
            Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        else
            Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
    }

    //######################################
    //Flavour folder sync - branches on CfgIsGitHub
    //######################################
    public static async Task CheckFlavourUpdateAsync()
    {
        try
        {
            var cfg = AppConfig;
            if (cfg.ManualMode) return;

            PrepareConfigHeaders();

            if (cfg.CfgIsGitHub)
                await SyncFlavoursFromGitHub(cfg);
            else
                await SyncFlavoursFromGitLab(cfg);
        }
        catch { /* off-network or not configured - fail silently */ }
    }

    //######################################
    //GitHub Contents API flavour sync
    //GET /repos/{owner}/{repo}/contents/{path}
    //Returns array of { name, sha, download_url, type }
    //######################################
    private static async Task SyncFlavoursFromGitHub(AppConfig cfg)
    {
        var owner = cfg.CfgRepoOwner.Trim('/');
        var repo  = cfg.CfgRepoName.Trim('/');
        var path  = cfg.FlavourRepoPath.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo)) return;

        var contentsUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";

        using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var json       = await Http.GetStringAsync(contentsUrl, cts.Token);
        var entries    = JsonSerializer.Deserialize<JsonElement[]>(json) ?? Array.Empty<JsonElement>();
        var anyChanged = false;

        foreach (var entry in entries)
        {
            if (entry.GetProperty("type").GetString() != "file") continue;

            var fileName = entry.GetProperty("name").GetString() ?? "";
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            if (fileName.StartsWith("local_flavour_", StringComparison.OrdinalIgnoreCase)) continue;
            if (fileName.Equals("_Template.json", StringComparison.OrdinalIgnoreCase)) continue;

            var sha       = entry.GetProperty("sha").GetString() ?? "";
            var localPath = Path.Combine(FlavourDir, fileName);

            if (_flavourHashes.TryGetValue(fileName, out var cachedSha) &&
                cachedSha == sha && File.Exists(localPath))
                continue;

            var downloadUrl = entry.GetProperty("download_url").GetString() ?? "";
            if (string.IsNullOrEmpty(downloadUrl)) continue;

            var fileContent = await Http.GetStringAsync(downloadUrl);
            await File.WriteAllTextAsync(localPath, fileContent);
            _flavourHashes[fileName] = sha;
            anyChanged = true;
        }

        if (anyChanged) Reload();
    }

    //######################################
    //GitLab Tree API flavour sync (existing logic)
    //######################################
    private static async Task SyncFlavoursFromGitLab(AppConfig cfg)
    {
        if (cfg.FlavourProjectId == 0) return;

        var apiBase  = cfg.UrlServer.TrimEnd('/') + "/api/v4";
        var repoPath = cfg.FlavourRepoPath.TrimEnd('/');
        var treeUrl  = $"{apiBase}/projects/{cfg.FlavourProjectId}/repository/tree" +
                       $"?path={Uri.EscapeDataString(repoPath)}&ref=main&per_page=100";

        using var cts   = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var treeJson    = await Http.GetStringAsync(treeUrl, cts.Token);
        var treeEntries = JsonSerializer.Deserialize<JsonElement[]>(treeJson) ?? Array.Empty<JsonElement>();
        var anyChanged  = false;

        foreach (var entry in treeEntries)
        {
            if (entry.GetProperty("type").GetString() != "blob") continue;

            var fileName = entry.GetProperty("name").GetString() ?? "";
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            if (fileName.StartsWith("local_flavour_", StringComparison.OrdinalIgnoreCase)) continue;
            if (fileName.Equals("_Template.json", StringComparison.OrdinalIgnoreCase)) continue;

            var blobSha   = entry.GetProperty("id").GetString() ?? "";
            var localPath = Path.Combine(FlavourDir, fileName);

            if (_flavourHashes.TryGetValue(fileName, out var cachedSha) &&
                cachedSha == blobSha && File.Exists(localPath))
                continue;

            var filePath    = $"{repoPath}/{fileName}";
            var encodedPath = Uri.EscapeDataString(filePath);
            var rawUrl      = $"{apiBase}/projects/{cfg.FlavourProjectId}/repository/files/{encodedPath}/raw?ref=main";

            var fileContent = await Http.GetStringAsync(rawUrl);
            await File.WriteAllTextAsync(localPath, fileContent);
            _flavourHashes[fileName] = blobSha;
            anyChanged = true;
        }

        if (anyChanged) Reload();
    }

    //######################################
    //Defaults update check - poll cycle, seed-only
    //######################################
    public static async Task CheckDefaultsUpdateAsync()
    {
        try
        {
            var cfg      = AppConfig;
            var defaults = await FetchOrgDefaultsAsync();
            var changed  = ApplyOrgDefaults(cfg, defaults, force: false);
            if (changed) PersistConfig(cfg);
        }
        catch { /* off-network or not configured - fail silently */ }
    }

    //######################################
    //Force apply org defaults - called by Restore Defaults button
    //######################################
    public static async Task<string> ForceApplyOrgDefaultsAsync()
    {
        try
        {
            var cfg      = AppConfig;
            var defaults = await FetchOrgDefaultsAsync();
            var changed  = ApplyOrgDefaults(cfg, defaults, force: true);

            if (changed)
            {
                PersistConfig(cfg);
                return "✅ Org defaults applied - LAPS, token warn days and profile seeds restored";
            }

            return defaults is null
                ? "⚠️ Org defaults unavailable - not configured or off-network"
                : "✅ Already up to date with org defaults";
        }

        catch (Exception ex)
        {
            return $"❌ Failed to apply org defaults: {ex.Message}";
        }
    }

    //######################################
    //Fetch defaults.json - branches on CfgIsGitHub
    //######################################
    private static async Task<OrgDefaults?> FetchOrgDefaultsAsync()
    {
        var cfg = AppConfig;
        if (string.IsNullOrWhiteSpace(cfg.DefaultsRepoPath)) return null;

        PrepareConfigHeaders();

        if (cfg.CfgIsGitHub)
            return await FetchDefaultsFromGitHub(cfg);
        else
            return await FetchDefaultsFromGitLab(cfg);
    }

    //######################################
    //GitHub Contents API defaults fetch
    //######################################
    private static async Task<OrgDefaults?> FetchDefaultsFromGitHub(AppConfig cfg)
    {
        var owner = cfg.CfgRepoOwner.Trim('/');
        var repo  = cfg.CfgRepoName.Trim('/');
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo)) return null;

        var contentsUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{cfg.DefaultsRepoPath}";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var json      = await Http.GetStringAsync(contentsUrl, cts.Token);
        var meta      = JsonSerializer.Deserialize<JsonElement>(json);
        var sha       = meta.GetProperty("sha").GetString() ?? "";

        if (sha != "" && sha == _lastDefaultsHash) return null;

        var downloadUrl = meta.GetProperty("download_url").GetString() ?? "";
        if (string.IsNullOrEmpty(downloadUrl)) return null;

        var rawJson = await Http.GetStringAsync(downloadUrl);
        var result  = JsonSerializer.Deserialize<OrgDefaults>(rawJson, JsonOpts);
        if (result is not null) _lastDefaultsHash = sha;
        return result;
    }

    //######################################
    //GitLab API defaults fetch (existing logic)
    //######################################
    private static async Task<OrgDefaults?> FetchDefaultsFromGitLab(AppConfig cfg)
    {
        if (cfg.FlavourProjectId == 0) return null;

        var apiBase = cfg.UrlServer.TrimEnd('/') + "/api/v4";
        var encoded = Uri.EscapeDataString(cfg.DefaultsRepoPath);
        var metaUrl = $"{apiBase}/projects/{cfg.FlavourProjectId}/repository/files/{encoded}?ref=main";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var metaJson  = await Http.GetStringAsync(metaUrl, cts.Token);
        var meta      = JsonSerializer.Deserialize<JsonElement>(metaJson);
        var latest    = meta.GetProperty("last_commit_id").GetString() ?? "";

        if (latest != "" && latest == _lastDefaultsHash) return null;

        var rawUrl  = $"{apiBase}/projects/{cfg.FlavourProjectId}/repository/files/{encoded}/raw?ref=main";
        var rawJson = await Http.GetStringAsync(rawUrl);
        var result  = JsonSerializer.Deserialize<OrgDefaults>(rawJson, JsonOpts);
        if (result is not null) _lastDefaultsHash = latest;
        return result;
    }

    //######################################
    //Smart merge org defaults into AppConfig
    //######################################
    private static bool ApplyOrgDefaults(AppConfig cfg, OrgDefaults? defaults, bool force)
    {
        if (defaults is null) return false;

        var changed = false;

        if (defaults.Laps is not null)
        {
            var notConfigured = string.IsNullOrWhiteSpace(cfg.Laps.DomainController);
            if (force || notConfigured) { cfg.Laps = defaults.Laps; changed = true; }
        }

        if (defaults.TokenExpiryWarnDays.HasValue)
        {
            var notConfigured = cfg.TokenExpiryWarnDays == 0;
            if (force || notConfigured) { cfg.TokenExpiryWarnDays = defaults.TokenExpiryWarnDays.Value; changed = true; }
        }

        if (defaults.RunAsProfiles is not null)
        {
            foreach (var orgProfile in defaults.RunAsProfiles)
            {
                var exists = cfg.RunAsProfiles.Any(p =>
                    string.Equals(p.Name, orgProfile.Name, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    cfg.RunAsProfiles.Add(new RunAsProfile
                    {
                        Name     = orgProfile.Name,
                        Username = orgProfile.Username
                    });
                    changed = true;
                }
            }
        }

        return changed;
    }

    //######################################
    //Persist config after a defaults apply
    //######################################
    private static void PersistConfig(AppConfig cfg)
    {
        foreach (var p in cfg.RunAsProfiles) p.MigrateAndEncrypt();
        SerializeAndWrite(cfg);
        Reload();
    }
}
