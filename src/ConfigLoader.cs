using System.Text.Json;
using System.Net.Http;

public static class ConfigLoader
{
    public static readonly string ConfigDir  = Application.StartupPath;
    public static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");
    public static readonly string FlavourDir = Path.Combine(ConfigDir, "flavours");

    public static event Action? OnConfigReloaded;

    private static AppConfig?              _appConfig;
    private static FlavourConfig?          _flavourConfig;
    private static readonly object         _lock        = new();
    private static System.Timers.Timer?    _pollTimer;
    private static FileSystemWatcher?      _configWatcher;
    private static string                  _lastHash    = "";
    private static readonly HttpClient     Http         = new();

    //private static readonly JsonSerializerOptions JsonOpts = new()
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

    // ── Reload — called by watcher, poller, or manually ───────────────
    public static void Reload()
    {
        lock (_lock)
        {
            _appConfig     = LoadAppConfig();
            _flavourConfig = LoadFlavourConfig(_appConfig.Flavour);
        }
        OnConfigReloaded?.Invoke();
    }

    // ── Switch flavour — updates config.json and reloads ──────────────
    public static void SetFlavour(string name)
    {
        var cfg    = LoadAppConfig();
        cfg.Flavour = name;
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOpts));
        _lastHash = "";  // Force flavour re-download on next poll
        Reload();
        StartFlavourPoller();
    }

    // ── Toggle diagnostics ────────────────────────────────────────────
    public static void ToggleDiagnostics()
    {
        var cfg       = LoadAppConfig();
        cfg.Diagnostics = !cfg.Diagnostics;
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOpts));
        Reload();
    }

    // ── Available flavours (excludes hidden unless requested) ─────────
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

    // ── Generate default files on first run ───────────────────────────
    private static void EnsureDefaultFiles()
    {
        if (!File.Exists(ConfigPath))
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new AppConfig(), JsonOpts));

        WriteDefaultFlavour("LBU-DS-ServiceDesk", false, new()
        {
            new() { Section = "Web Tools", Icon = "🌐", Items = new()
            {
                new() { Label = "Example Portal", Type = "url", Value = "https://example.com" }
            }},
            new() { Section = "GitLab Scripts", Icon = "☁️", Items = new()
            {
                new() { Label = "Panopto Delta Informant", Type = "script",
                        ProjectId = 522, FilePath = "Powershell/PanoptoDeltaInformant/PanoptoDeltaInformant.ps1" }
            }}
        });

        WriteDefaultFlavour("LBU-DS-SupportServices", false, new()
        {
            new() { Section = "Support Tools", Icon = "🔧", Items = new()
            {
                new() { Label = "Example Tool", Type = "url", Value = "https://example.com" }
            }}
        });

        WriteDefaultFlavour("KDEV", true, new()
        {
            new() { Section = "Dev Tools", Icon = "💻", Items = new()
            {
                new() { Label = "Home Assistant",   Type = "url", Value = "http://homeassistant.local:8123" },
                new() { Label = "Proxmox",           Type = "url", Value = "https://192.168.1.5:8006" },
                new() { Label = "Portainer",         Type = "url", Value = "http://192.168.1.10:9000" },
                new() { Label = "Flush DNS",         Type = "powershell", Value = "ipconfig /flushdns" }
            }}
        });
    }

    private static void WriteDefaultFlavour(string name, bool hidden, List<FlavourSection> sections)
    {
        var path = Path.Combine(FlavourDir, $"{name}.json");
        if (File.Exists(path)) return;
        var fc = new FlavourConfig { Hidden = hidden, Menu = sections };
        File.WriteAllText(path, JsonSerializer.Serialize(fc, JsonOpts));
    }

    // ── File loading ──────────────────────────────────────────────────
    private static AppConfig LoadAppConfig()
    {
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOpts)
                   ?? new AppConfig();
        }
        catch { return new AppConfig(); }
    }

    private static FlavourConfig LoadFlavourConfig(string name)
    {
        var path = Path.Combine(FlavourDir, $"{name}.json");
        if (!File.Exists(path)) return new FlavourConfig();
        try
        {
            return JsonSerializer.Deserialize<FlavourConfig>(File.ReadAllText(path), JsonOpts)
                   ?? new FlavourConfig();
        }
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

        _ = CheckFlavourUpdateAsync();  // Check immediately on start
    }

    public static async Task CheckFlavourUpdateAsync()
    {
        try
        {
            var cfg = AppConfig;
            if (cfg.FlavourProjectId <= 0) return;

            var token = TokenManager.GetToken();
            Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
            if (token != null) Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
            if (!Http.DefaultRequestHeaders.UserAgent.Any())
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");

            var apiBase  = cfg.UrlServer.TrimEnd('/') + "/api/v4";
            var repoPath = $"{cfg.FlavourRepoPath.TrimEnd('/')}/{cfg.Flavour}.json";
            var encoded  = Uri.EscapeDataString(repoPath);
            var metaUrl  = $"{apiBase}/projects/{cfg.FlavourProjectId}/repository/files/{encoded}?ref=main";

            using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var metaJson   = await Http.GetStringAsync(metaUrl, cts.Token);
            var meta       = JsonSerializer.Deserialize<JsonElement>(metaJson);
            var latestHash = meta.GetProperty("last_commit_id").GetString() ?? "";

            if (latestHash == _lastHash && _lastHash != "") return;

            var rawUrl  = $"{apiBase}/projects/{cfg.FlavourProjectId}/repository/files/{encoded}/raw?ref=main";
            var content = await Http.GetStringAsync(rawUrl);

            await File.WriteAllTextAsync(Path.Combine(FlavourDir, $"{cfg.Flavour}.json"), content);
            _lastHash = latestHash;

            Reload();
        }
        catch { /* Off-network or not configured — fail silently */ }
    }
}
