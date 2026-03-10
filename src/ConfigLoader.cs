using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;

public static class ConfigLoader
{
    public static readonly string ConfigDir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PandaTools"
    );
    public static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");
    public static readonly string FlavourDir = Path.Combine(ConfigDir, "flavours");

    public static event Action? OnConfigReloaded;

    private static AppConfig?     _appConfig;
    private static FlavourConfig? _flavourConfig;
    private static readonly object _lock = new();
    private static System.Timers.Timer?  _pollTimer;
    private static FileSystemWatcher?    _configWatcher;
    private static string _lastHash = "";
    private static readonly HttpClient Http = new();

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true,
        WriteIndented               = true
    };

    public static AppConfig     AppConfig     => _appConfig     ?? new AppConfig();
    public static FlavourConfig FlavourConfig => _flavourConfig ?? new FlavourConfig();

    // ── Initial load ──────────────────────────────────────────────────
    public static void Load()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(FlavourDir);
        EnsureDefaultFiles();
        lock (_lock)
        {
            _appConfig     = LoadAppConfig();
            _flavourConfig = LoadFlavourConfig(_appConfig.Flavour);
        }
        StartConfigWatcher();
        StartFlavourPoller();
    }

    // ── Reload (called by watcher, poller, or manually) ───────────────
    public static void Reload()
    {
        lock (_lock)
        {
            _appConfig     = LoadAppConfig();
            _flavourConfig = LoadFlavourConfig(_appConfig.Flavour);
        }
        OnConfigReloaded?.Invoke();
    }

    // ── Switch flavour ────────────────────────────────────────────────
    public static void SetFlavour(string name)
    {
        var cfg = LoadAppConfig();
        cfg.Flavour = name;
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOpts));
        _lastHash = "";
        Reload();
        StartFlavourPoller();
    }

    // ── Toggle diagnostics ────────────────────────────────────────────
    public static void ToggleDiagnostics()
    {
        var cfg = LoadAppConfig();
        cfg.Diagnostics = !cfg.Diagnostics;
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOpts));
        Reload();
    }

    // ── Available flavours ────────────────────────────────────────────
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

    // ── Ensure default files on first run ─────────────────────────────
    private static void EnsureDefaultFiles()
    {
        if (!File.Exists(ConfigPath))
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new AppConfig(), JsonOpts));

        // ── LBU-DS-ServiceDesk ────────────────────────────────────────
        WriteDefaultFlavour("LBU-DS-ServiceDesk", hidden: false, new List<FlavourSection>
        {
            new()
            {
                Section = "Web Tools", Icon = "🌐",
                Items = new()
                {
                    new() { Label = "Staff Portal",  Type = "url", Value = "https://portal.leedsbeckett.ac.uk" },
                    new() { Label = "GitLab",         Type = "url", Value = "https://gitlab.leedsbeckett.ac.uk" },
                    new() { Label = "Service Desk",   Type = "url", Value = "https://servicedesk.leedsbeckett.ac.uk" }
                }
            },
            new()
            {
                Section = "GitLab Scripts", Icon = "📜",
                Items = new()
                {
                    new() { Label = "Panopto Delta Informant", Type = "script", ProjectId = 522,
                            FilePath = "Powershell/PanoptoDeltaInformant/PanoptoDeltaInformant.ps1" }
                }
            }
        });

        // ── LBU-DS-SupportServices ────────────────────────────────────
        WriteDefaultFlavour("LBU-DS-SupportServices", hidden: false, new List<FlavourSection>
        {
            new()
            {
                Section = "Support Tools", Icon = "🛠️",
                Items = new()
                {
                    new() { Label = "Service Desk",  Type = "url",        Value = "https://servicedesk.leedsbeckett.ac.uk" },
                    new() { Label = "Staff Portal",  Type = "url",        Value = "https://portal.leedsbeckett.ac.uk" },
                    new() { Label = "Flush DNS",      Type = "powershell", Value = "ipconfig /flushdns" }
                }
            },
            new()
            {
                Section = "Applications", Icon = "🖥️",
                Items = new()
                {
                    new() { Label = "SCCM Console", Type = "app",
                            Value = @"C:\Program Files (x86)\Microsoft Configuration Manager\AdminConsole\bin\Microsoft.ConfigurationManagement.exe" }
                }
            },
            new()
            {
                Section = "GitLab Scripts", Icon = "📜",
                Items = new()
                {
                    new() { Label = "SCCM Collection Tool", Type = "script", ProjectId = 525,
                            FilePath = "SCCMCollectionMembership-Utility.ps1" }
                }
            }
        });
    }

    private static void WriteDefaultFlavour(string name, bool hidden, List<FlavourSection> sections)
    {
        var path = Path.Combine(FlavourDir, $"{name}.json");
        if (File.Exists(path)) return;   // never overwrite user edits
        var fc = new FlavourConfig { Version = "1.0", Hidden = hidden, Menu = sections };
        File.WriteAllText(path, JsonSerializer.Serialize(fc, JsonOpts));
    }

    // ── File loading ──────────────────────────────────────────────────
    private static AppConfig LoadAppConfig()
    {
        try { return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOpts) ?? new AppConfig(); }
        catch { return new AppConfig(); }
    }

    private static FlavourConfig LoadFlavourConfig(string name)
    {
        var path = Path.Combine(FlavourDir, $"{name}.json");
        if (!File.Exists(path)) return new FlavourConfig();
        try { return JsonSerializer.Deserialize<FlavourConfig>(File.ReadAllText(path), JsonOpts) ?? new FlavourConfig(); }
        catch { return new FlavourConfig(); }
    }

    // ── Watch config.json for local edits ─────────────────────────────
    private static void StartConfigWatcher()
    {
        _configWatcher?.Dispose();
        _configWatcher = new FileSystemWatcher(ConfigDir)
        {
            Filter              = "config.json",
            NotifyFilter        = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        var debounce = new System.Timers.Timer(500) { AutoReset = false };
        debounce.Elapsed += (_, _) => Reload();
        _configWatcher.Changed += (_, _) => { debounce.Stop(); debounce.Start(); };
    }

    // ── Poll GitLab for flavour changes ───────────────────────────────
    private static void StartFlavourPoller()
    {
        _pollTimer?.Dispose();
        var ms = Math.Max(30, AppConfig.FlavourPollSeconds) * 1000.0;
        _pollTimer = new System.Timers.Timer(ms) { AutoReset = true };
        _pollTimer.Elapsed += async (_, _) => await CheckFlavourUpdateAsync();
        _pollTimer.Start();
    }

    public static async Task CheckFlavourUpdateAsync()
    {
        try
        {
            var cfg = AppConfig;
            if (cfg.ManualMode || cfg.FlavourProjectId == 0) return;

            var token = TokenManager.GetToken();
            Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
            if (token != null) Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
            if (!Http.DefaultRequestHeaders.UserAgent.Any())
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");

            var apiBase  = cfg.UrlServer.TrimEnd('/') + "/api/v4";
            var repoPath = $"{cfg.FlavourRepoPath.TrimEnd('/')}/{cfg.Flavour}.json";
            var encoded  = Uri.EscapeDataString(repoPath);
            var metaUrl  = $"{apiBase}/projects/{cfg.FlavourProjectId}/repository/files/{encoded}?ref=main";

            using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var       metaJson = await Http.GetStringAsync(metaUrl, cts.Token);
            var       meta     = JsonSerializer.Deserialize<JsonElement>(metaJson);
            var       latest   = meta.GetProperty("last_commit_id").GetString() ?? "";

            if (latest == _lastHash || latest == "") return;

            var rawUrl  = $"{apiBase}/projects/{cfg.FlavourProjectId}/repository/files/{encoded}/raw?ref=main";
            var content = await Http.GetStringAsync(rawUrl);
            await File.WriteAllTextAsync(Path.Combine(FlavourDir, $"{cfg.Flavour}.json"), content);
            _lastHash = latest;
            Reload();
        }
        catch { /* off-network or not configured — fail silently */ }
    }
}
