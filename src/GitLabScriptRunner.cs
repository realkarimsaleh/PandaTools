using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;
using System.Diagnostics;

public static class GitLabScriptRunner
{
    private static readonly string CacheFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PandaTools", "ScriptCache");

    private static readonly HttpClient Http = new();

    public static async Task RunAsync(int projectId, string filePath, string branch = "main")
    {
        var cfg     = ConfigLoader.AppConfig;
        var apiBase = cfg.UrlServer.TrimEnd('/') + "/api/v4";
        var diag    = cfg.Diagnostics;
        var log     = new System.Text.StringBuilder();

        void Log(string msg) { if (diag) log.AppendLine(msg); }

        Log($"ProjectId   : {projectId}");
        Log($"FilePath    : {filePath}");
        Log($"Branch      : {branch}");
        Log($"ApiBase     : {apiBase}");

        try
        {
            var token = TokenManager.GetToken();
            Log($"Token       : {(token != null ? "Found" : "Not found")}");

            Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
            if (token != null) Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
            if (!Http.DefaultRequestHeaders.UserAgent.Any())
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");

            var safeFile     = $"{projectId}_{Path.GetFileName(filePath)}";
            var cachedScript = Path.Combine(CacheFolder, safeFile);
            var cachedHash   = Path.Combine(CacheFolder, safeFile + ".hash");
            Directory.CreateDirectory(CacheFolder);

            var encoded  = Uri.EscapeDataString(filePath);
            var metaUrl  = $"{apiBase}/projects/{projectId}/repository/files/{encoded}?ref={branch}";
            var metaJson = await Http.GetStringAsync(metaUrl);
            var meta     = JsonSerializer.Deserialize<JsonElement>(metaJson);
            var latest   = meta.GetProperty("last_commit_id").GetString() ?? "";
            var local    = File.Exists(cachedHash) ? await File.ReadAllTextAsync(cachedHash) : "";

            Log($"LatestHash  : {latest}");
            Log($"LocalHash   : {(local == "" ? "None" : local)}");

            if (latest != local || !File.Exists(cachedScript))
            {
                var isUpdate = File.Exists(cachedScript) && local != "";
                MessageBox.Show(
                    isUpdate
                        ? $"{Path.GetFileName(filePath)} has been updated — fetching latest version."
                        : $"{Path.GetFileName(filePath)} is being cached for the first time.",
                    "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);

                var raw = $"{apiBase}/projects/{projectId}/repository/files/{encoded}/raw?ref={branch}";
                await File.WriteAllTextAsync(cachedScript, await Http.GetStringAsync(raw));
                await File.WriteAllTextAsync(cachedHash, latest);
                Log("Downloaded and cached successfully.");
            }
            else { Log("Hash matches — using cached version."); }

            if (diag)
                MessageBox.Show($"Diagnostic log:\n\n{log}", "PandaTools Debug",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

            Process.Start(new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -File \"{cachedScript}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log($"\nFAILED: {ex.Message}");
            var cachedScript = Path.Combine(CacheFolder, $"{projectId}_{Path.GetFileName(filePath)}");

            if (!File.Exists(cachedScript))
            {
                MessageBox.Show(
                    diag ? $"Failed (no cache):\n\n{log}" : $"Script failed:\n{ex.Message}",
                    "PandaTools — Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(
                diag ? $"Failed (using cache):\n\n{log}" : "GitLab unreachable — running cached version.",
                "PandaTools — Using Cache", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            Process.Start(new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -File \"{cachedScript}\"",
                UseShellExecute = true
            });
        }
    }

    public static void ClearCache()
    {
        if (!Directory.Exists(CacheFolder) || Directory.GetFiles(CacheFolder).Length == 0)
        {
            MessageBox.Show("Cache is already empty.", "PandaTools",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var files = Directory.GetFiles(CacheFolder);
        foreach (var f in files) File.Delete(f);
        MessageBox.Show($"Cleared {files.Length} cached file(s).", "PandaTools",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
