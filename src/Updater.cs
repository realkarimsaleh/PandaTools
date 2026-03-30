using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

public static class Updater
{
    private static readonly HttpClient Http = new();

    private static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public static async Task CheckAsync(bool silent = false)
    {
        try
        {
            var cfg = ConfigLoader.AppConfig;
            PrepareAppHeaders(cfg);

            string  latestTag;
            string  releaseUrl;
            string? downloadUrl;

            if (cfg.AppIsGitHub)
            {
                //######################################
                //GitHub Releases API
                //GET /repos/{owner}/{repo}/releases
                //######################################
                var owner = cfg.AppRepoOwner.Trim('/');
                var repo  = cfg.AppRepoName.Trim('/');

                if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
                {
                    if (!silent) MessageBox.Show(
                        "GitHub Repo Owner or Repo Name is not configured.\n\nGo to Settings → Connection.",
                        "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var releasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";
                using var listCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var json          = await Http.GetStringAsync(releasesUrl, listCts.Token);
                var releases      = JsonSerializer.Deserialize<JsonElement[]>(json);

                if (releases is null || releases.Length == 0)
                {
                    if (!silent) MessageBox.Show("No releases found on GitHub.", "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                latestTag  = releases[0].GetProperty("tag_name").GetString()!;
                releaseUrl = $"https://github.com/{owner}/{repo}/releases/tag/{latestTag}";

                if (!IsNewer(latestTag, CurrentVersion))
                {
                    if (!silent) MessageBox.Show($"You're up to date! (v{CurrentVersion})", "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                //Find .exe asset in GitHub release
                downloadUrl = null;
                if (releases[0].TryGetProperty("assets", out var ghAssets))
                {
                    foreach (var asset in ghAssets.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!name.Contains("Setup", StringComparison.OrdinalIgnoreCase)) continue;
                        if (asset.TryGetProperty("browser_download_url", out var dlUrl))
                            downloadUrl = dlUrl.GetString();
                        break;
                    }
                }
            }
            else
            {
                //######################################
                //GitLab Releases API
                //GET /api/v4/projects/{id}/releases
                //######################################
                var baseUrl   = (string.IsNullOrWhiteSpace(cfg.AppUrlServer) ? cfg.UrlServer : cfg.AppUrlServer).TrimEnd('/');
                var repoPath  = cfg.AppRepoPath.Trim('/');
                var projectId = cfg.AppProjectId;

                if (projectId == 0)
                {
                    if (!silent) MessageBox.Show(
                        "App Project ID is not configured.\n\nGo to Settings → Connection.",
                        "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var apiBase     = $"{baseUrl}/api/v4";
                var releasesUrl = $"{apiBase}/projects/{projectId}/releases";

                using var listCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var json          = await Http.GetStringAsync(releasesUrl, listCts.Token);
                var releases      = JsonSerializer.Deserialize<JsonElement[]>(json);

                if (releases is null || releases.Length == 0)
                {
                    if (!silent) MessageBox.Show("No releases found on GitLab.", "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                latestTag  = releases[0].GetProperty("tag_name").GetString()!;
                releaseUrl = $"{baseUrl}/{repoPath}/-/releases/{latestTag}";

                if (!IsNewer(latestTag, CurrentVersion))
                {
                    if (!silent) MessageBox.Show($"You're up to date! (v{CurrentVersion})", "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var detailUrl = $"{apiBase}/projects/{projectId}/releases/{Uri.EscapeDataString(latestTag)}";
                using var detailCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var detailJson      = await Http.GetStringAsync(detailUrl, detailCts.Token);
                var release         = JsonSerializer.Deserialize<JsonElement>(detailJson);
                downloadUrl         = FindGitLabSetupAssetUrl(release);
            }

            bool canAutoInstall = downloadUrl != null;

            var promptMsg = canAutoInstall
                ? $"PandaTools {latestTag} is available (you have v{CurrentVersion}).\n\n" +
                  "Download and install now?\n\nPandaTools will close and the installer will launch automatically."
                : $"PandaTools {latestTag} is available (you have v{CurrentVersion}).\n\nOpen the download page?";

            var result = MessageBox.Show(promptMsg, "Update Available ✨", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result != DialogResult.Yes) return;

            if (!canAutoInstall)
            {
                Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
                return;
            }

            await DownloadAndInstallAsync(downloadUrl!, latestTag);
        }
        catch when (silent) { }
        catch (Exception ex) when (!silent)
        {
            MessageBox.Show($"Update check failed:\n{ex.Message}", "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    //######################################
    //Prepare headers for the app update server
    //GitHub: Authorization Bearer (only if private), GitLab: PRIVATE-TOKEN
    //Public repos: no auth header needed
    //######################################
    private static void PrepareAppHeaders(AppConfig cfg)
    {
        if (!Http.DefaultRequestHeaders.UserAgent.Any())
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");

        Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
        Http.DefaultRequestHeaders.Remove("Authorization");

        //Public - no token needed
        if (cfg.AppPublic) return;

        var token = TokenManager.GetAppToken();
        if (token == null) return;

        if (cfg.AppIsGitHub)
            Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        else
            Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
    }

    //######################################
    //Download the setup exe then launch it
    //######################################
    private static async Task DownloadAndInstallAsync(string downloadUrl, string version)
    {
        var tempSetup = Path.Combine(Path.GetTempPath(), $"PandaToolsSetup_{version}.exe");

        if (File.Exists(tempSetup))
        {
            var useCached = MessageBox.Show(
                $"A cached installer for {version} was found from a previous download.\n\nUse the cached version?\n\nClick No to re-download.",
                "Cached Installer Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (useCached == DialogResult.Yes) { LaunchInstaller(tempSetup, version); return; }
            try { File.Delete(tempSetup); } catch { }
        }

        var progressForm = new Form
        {
            Text = "PandaTools – Downloading Update", Size = new Size(440, 110),
            StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, TopMost = true,
            BackColor = Color.White, Font = new Font("Segoe UI", 9f)
        };
        var bar = new ProgressBar { Left = 16, Top = 16, Width = 400, Height = 22, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
        var lbl = new Label { Left = 16, Top = 46, Width = 400, Height = 20, Text = $"Downloading PandaTools {version}…", ForeColor = Color.DimGray };
        progressForm.Controls.AddRange(new Control[] { bar, lbl });
        progressForm.Show();
        Application.DoEvents();

        try
        {
            using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            long read = 0;
            {
                await using var fs     = File.Create(tempSetup);
                await using var stream = await response.Content.ReadAsStreamAsync();
                var buffer             = new byte[81920];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                    read += bytesRead;
                    if (total > 0)
                    {
                        var pct = (int)(read * 100 / total);
                        if (progressForm.IsHandleCreated)
                            progressForm.Invoke(() =>
                            {
                                bar.Style = ProgressBarStyle.Continuous;
                                bar.Value = Math.Clamp(pct, 0, 100);
                                lbl.Text  = $"Downloading PandaTools {version}… {pct}%";
                            });
                    }
                    Application.DoEvents();
                }
            }
            progressForm.Close(); progressForm.Dispose();
            LaunchInstaller(tempSetup, version);
        }
        catch (Exception ex)
        {
            if (!progressForm.IsDisposed) { progressForm.Close(); progressForm.Dispose(); }
            MessageBox.Show($"Download failed:\n\n{ex.Message}", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void LaunchInstaller(string tempSetup, string version)
    {
        MessageBox.Show(
            $"PandaTools {version} has been downloaded.\n\n" +
            "The installer will now launch.\n" +
            "PandaTools will close automatically - it will restart once the install is complete.",
            "Update Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Process.Start(new ProcessStartInfo { FileName = tempSetup, UseShellExecute = true });
        Application.Exit();
    }

    public static void ClearUpdateCache()
    {
        try
        {
            var files = Directory.GetFiles(Path.GetTempPath(), "PandaToolsSetup_*.exe");
            foreach (var f in files) try { File.Delete(f); } catch { }
            MessageBox.Show(
                files.Length == 0 ? "Update cache is already empty." : $"Cleared {files.Length} cached installer{(files.Length == 1 ? "" : "s")}.",
                "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Failed to clear cache: {ex.Message}", "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    //######################################
    //GitLab release asset finder
    //######################################
    private static string? FindGitLabSetupAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets)) return null;
        if (!assets.TryGetProperty("links", out var links))    return null;
        foreach (var link in links.EnumerateArray())
        {
            var name = link.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.Contains("Setup", StringComparison.OrdinalIgnoreCase)) continue;
            if (link.TryGetProperty("direct_asset_url", out var direct)) return direct.GetString();
            if (link.TryGetProperty("url", out var url)) return url.GetString();
        }
        return null;
    }

    private static bool IsNewer(string latest, string current) =>
        Version.TryParse(latest.TrimStart('v'), out var l) &&
        Version.TryParse(current,               out var c) &&
        l > c;
}
