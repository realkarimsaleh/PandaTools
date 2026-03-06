using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;
using System.Diagnostics;

public static class GitLabScriptRunner
{
    //──────────────────────────────────────────────────────────────────
    //  Configuration
    //──────────────────────────────────────────────────────────────────
    private const string GitLabHost = "gitlab.leedsbeckett.ac.uk";
    private const string ApiBase    = $"https://{GitLabHost}/api/v4";

    //  Local cache folder — scripts stored here after first download
    //  Lives at %LOCALAPPDATA%\PandaTools\ScriptCache\
    private static readonly string CacheFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PandaTools", "ScriptCache");

    private static readonly HttpClient Http = new();

    //  RunAsync
    //  Checks GitLab for the latest commit hash on the file
    //  Downloads fresh copy only if hash has changed since last cache
    //  Notifies user when a new version is detected
    //  Falls back to cached copy if GitLab is unreachable
    //  projectId  : GitLab project ID (Settings > General in your repo)
    //  filePath   : path to .ps1 inside the repo e.g. "Powershell/MyScript/MyScript.ps1"
    //  branch     : branch to pull from — defaults to main
    public static async Task RunAsync(int projectId, string filePath, string branch = "main")
    {
        //  Diagnostic log — tracks each step to help identify failures
        var log = new System.Text.StringBuilder();
        log.AppendLine($"ProjectId   : {projectId}");
        log.AppendLine($"FilePath    : {filePath}");
        log.AppendLine($"Branch      : {branch}");
        log.AppendLine($"CacheFolder : {CacheFolder}");

        try
        {
            //  Step 1 — Inject PRIVATE-TOKEN header
            //  Always remove and re-add — prevents stale/bad token from a previous run
            //  Token decrypted once via CredEncrypt-Utility — same mechanism as GitLabSync
            var token = TokenManager.GetToken();
            log.AppendLine($"Token       : {(token != null ? "Found" : "Not found — proceeding without")}");

            Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
            if (token != null)
                Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);

            //  Only add UserAgent once — throws if added multiple times
            if (!Http.DefaultRequestHeaders.UserAgent.Any())
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");

            //  Step 2 — Build cache file paths
            var safeFileName = $"{projectId}_{Path.GetFileName(filePath)}";
            var cachedScript = Path.Combine(CacheFolder, safeFileName);
            var cachedHash   = Path.Combine(CacheFolder, safeFileName + ".hash");
            Directory.CreateDirectory(CacheFolder);
            log.AppendLine($"CachedScript: {cachedScript}");

            //  Step 3 — Ask GitLab for latest commit hash on this specific file
            //  Tiny metadata call — no script content downloaded yet
            log.AppendLine("Fetching metadata from GitLab...");
            var encoded    = Uri.EscapeDataString(filePath);
            var metaUrl    = $"{ApiBase}/projects/{projectId}/repository/files/{encoded}?ref={branch}";
            log.AppendLine($"MetaUrl     : {metaUrl}");

            var metaJson   = await Http.GetStringAsync(metaUrl);
            var meta       = JsonSerializer.Deserialize<JsonElement>(metaJson);
            var latestHash = meta.GetProperty("last_commit_id").GetString() ?? "";
            log.AppendLine($"LatestHash  : {latestHash}");

            //  Step 4 — Compare against locally cached hash
            var localHash = File.Exists(cachedHash) ? await File.ReadAllTextAsync(cachedHash) : "";
            log.AppendLine($"LocalHash   : {(localHash == "" ? "None (not cached yet)" : localHash)}");

            if (latestHash != localHash || !File.Exists(cachedScript))
            {
                //  Hash mismatch or no cache — notify user then download fresh copy
                log.AppendLine("Hash mismatch or no cache — downloading script...");
                var isUpdate = File.Exists(cachedScript) && localHash != "";
                MessageBox.Show(
                    isUpdate
                        ? $"{Path.GetFileName(filePath)} has been updated on GitLab — fetching latest version."
                        : $"{Path.GetFileName(filePath)} is being cached for the first time.",
                    "PandaTools",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                //  Download the latest script content
                var rawUrl = $"{ApiBase}/projects/{projectId}/repository/files/{encoded}/raw?ref={branch}";
                var script = await Http.GetStringAsync(rawUrl);

                //  Save script and updated hash to cache
                await File.WriteAllTextAsync(cachedScript, script);
                await File.WriteAllTextAsync(cachedHash,   latestHash);
                log.AppendLine("Script downloaded and cached successfully.");
            }
            else
            {
                log.AppendLine("Hash matches — using cached version.");
            }

            //  Step 5 — Show diagnostic log then execute
            log.AppendLine($"Executing   : {cachedScript}");
            MessageBox.Show($"Diagnostic log:\n\n{log}", "PandaTools Debug",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            //  Run the cached script — always executed from local cache
            Process.Start(new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -File \"{cachedScript}\"",
                UseShellExecute = true   //  Visible window while testing
            });
        }

        catch (Exception ex)
        {
            log.AppendLine($"\nFAILED AT   : {ex.Message}");
            log.AppendLine($"StackTrace  : {ex.StackTrace}");

            //  GitLab unreachable — fall back to cached copy if one exists
            var cachedScript = Path.Combine(CacheFolder, $"{projectId}_{Path.GetFileName(filePath)}");

            if (!File.Exists(cachedScript))
            {
                MessageBox.Show($"Diagnostic log:\n\n{log}",
                    "PandaTools Debug — Failed (No Cache)",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //  Warn user they are running the last known good cached version
            MessageBox.Show($"Diagnostic log:\n\n{log}",
                "PandaTools Debug — Failed (Using Cache)",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            Process.Start(new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -File \"{cachedScript}\"",
                UseShellExecute = true
            });
        }
    }

    //  ClearCache
    //  Deletes all cached scripts and hash files
    //  Wired to the "Clear Script Cache" menu item in MenuConfig.cs
    public static void ClearCache()
    {
        if (!Directory.Exists(CacheFolder))
        {
            MessageBox.Show("Cache is already empty.", "PandaTools",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var files = Directory.GetFiles(CacheFolder);

        if (files.Length == 0)
        {
            MessageBox.Show("Cache is already empty.", "PandaTools",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        foreach (var file in files)
            File.Delete(file);

        MessageBox.Show($"Cleared {files.Length} cached file(s).", "PandaTools",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
