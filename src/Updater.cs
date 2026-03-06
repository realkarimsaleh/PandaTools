using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;

public static class Updater
{
    //──────────────────────────────────────────────────────────────────
    //  Configuration
    //──────────────────────────────────────────────────────────────────

    //  Version is read directly from AssemblyVersion in .csproj
    //  No hardcoded string — single source of truth
    private static readonly string CurrentVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private const string GitLabHost      = "gitlab.leedsbeckett.ac.uk";
    private const int    GitLabProjectId = 999; // ← CHANGE THIS to your PandaTools project ID
    private static readonly string ApiUrl =
        $"https://{GitLabHost}/api/v4/projects/{GitLabProjectId}/releases";

    private static readonly HttpClient Http = new();

    //  CheckAsync
    //  silent = true  : runs on startup — swallows all errors quietly
    //  silent = false : triggered by menu click — shows result either way
    public static async Task CheckAsync(bool silent)
    {
        try
        {
            //  Only add UserAgent once — throws if added multiple times
            if (!Http.DefaultRequestHeaders.UserAgent.Any())
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");

            //  Always remove and re-add — prevents stale/bad token from a previous run
            //  Token decrypted once via CredEncrypt-Utility — same mechanism as GitLabSync
            var token = TokenManager.GetToken();
            Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
            if (token != null)
                Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);

            //  Short timeout — if not on network, fail fast and silently
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));

            var json     = await Http.GetStringAsync(ApiUrl, cts.Token);
            var releases = JsonSerializer.Deserialize<JsonElement[]>(json);

            if (releases is null || releases.Length == 0) return;

            var latestTag  = releases[0].GetProperty("tag_name").GetString()!;
            var releaseUrl = $"https://{GitLabHost}/service-delivery/pandatools/-/releases/{latestTag}";

            if (IsNewer(latestTag, CurrentVersion))
            {
                var result = MessageBox.Show(
                    $"PandaTools {latestTag} is available (you have v{CurrentVersion}).\n\nOpen download page?",
                    "Update Available ✨",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );

                if (result == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
            }
            else if (!silent)
            {
                MessageBox.Show(
                    $"You're up to date! (v{CurrentVersion})",
                    "PandaTools",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        //  Swallow all errors on background startup check — user is likely off-network
        catch when (silent) { }

        //  Show error when triggered manually from the menu
        catch (Exception ex) when (!silent)
        {
            MessageBox.Show(
                $"Update check failed:\n{ex.Message}",
                "PandaTools",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    //  IsNewer — compares two version strings numerically
    private static bool IsNewer(string latest, string current)
    {
        var l = Version.Parse(latest.TrimStart('v'));
        var c = Version.Parse(current);
        return l > c;
    }
}
