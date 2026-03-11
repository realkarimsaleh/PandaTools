using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;

public static class Updater
{
    private static readonly string CurrentVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private static readonly HttpClient Http = new();

    public static async Task CheckAsync(bool silent)
    {
        try
        {
            var cfg       = ConfigLoader.AppConfig;
            var baseUrl   = cfg.UrlServer.TrimEnd('/');
            var projectId = cfg.AppProjectId;
            var repoPath  = cfg.AppRepoPath.Trim('/');

            if (projectId <= 0)
            {
                if (!silent)
                    MessageBox.Show(
                        "App Project ID is not configured.\n\n" +
                        "Go to Settings → Advanced and set the App Project ID.",
                        "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var apiUrl = $"{baseUrl}/api/v4/projects/{projectId}/releases";

            if (!Http.DefaultRequestHeaders.UserAgent.Any())
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");

            var token = TokenManager.GetToken();
            Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
            if (token != null) Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var json      = await Http.GetStringAsync(apiUrl, cts.Token);
            var releases  = JsonSerializer.Deserialize<JsonElement[]>(json);
            if (releases is null || releases.Length == 0) return;

            var latestTag  = releases[0].GetProperty("tag_name").GetString()!;
            var releaseUrl = $"{baseUrl}/{repoPath}/-/releases/{latestTag}";

            if (IsNewer(latestTag, CurrentVersion))
            {
                var result = MessageBox.Show(
                    $"PandaTools {latestTag} is available (you have v{CurrentVersion}).\n\nOpen download page?",
                    "Update Available ✨", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
            }
            else if (!silent)
            {
                MessageBox.Show($"You're up to date! (v{CurrentVersion})",
                    "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch when (silent) { }
        catch (Exception ex) when (!silent)
        {
            MessageBox.Show($"Update check failed:\n{ex.Message}",
                "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static bool IsNewer(string latest, string current) =>
        Version.Parse(latest.TrimStart('v')) > Version.Parse(current);
}
