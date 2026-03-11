using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class TokenExpiryChecker
{
    private static readonly HttpClient Http = new();

    public static async Task CheckAsync()
    {
        try
        {
            var cfg      = ConfigLoader.AppConfig;
            var apiUrl   = $"{cfg.UrlServer.TrimEnd('/')}/api/v4/personal_access_tokens/self";
            var token    = TokenManager.GetToken();
            if (string.IsNullOrWhiteSpace(token)) return;

            PrepareHeaders(token);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var json      = await Http.GetStringAsync(apiUrl, cts.Token);
            var doc       = JsonSerializer.Deserialize<JsonElement>(json);

            if (!doc.TryGetProperty("expires_at", out var expiryProp)
                || expiryProp.ValueKind == JsonValueKind.Null) return;

            if (!DateOnly.TryParse(expiryProp.GetString(), out var expiry)) return;

            var daysLeft  = expiry.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
            var threshold = cfg.TokenExpiryWarnDays;

            if (daysLeft < 0)
                TrayContext.ShowBalloon("GitLab Token Expired",
                    $"Your GitLab token expired on {expiry:dd MMM yyyy}.\n" +
                    "GitLab features are unavailable. Update your token in Settings.",
                    ToolTipIcon.Error);
            else if (daysLeft <= threshold)
                TrayContext.ShowBalloon("GitLab Token Expiring Soon",
                    $"Your GitLab token expires in {daysLeft} day{(daysLeft == 1 ? "" : "s")} ({expiry:dd MMM yyyy}).\n" +
                    "Update it in Settings → Connection → New Token.",
                    ToolTipIcon.Warning);
        }
        catch { /* off-network or no token - fail silently */ }
    }

    public static async Task<string> GetExpiryInfoAsync()
    {
        try
        {
            var cfg    = ConfigLoader.AppConfig;
            var apiUrl = $"{cfg.UrlServer.TrimEnd('/')}/api/v4/personal_access_tokens/self";
            var token  = TokenManager.GetToken();

            if (string.IsNullOrWhiteSpace(token))
                return "❌ No token configured.";

            PrepareHeaders(token);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var json      = await Http.GetStringAsync(apiUrl, cts.Token);
            var doc       = JsonSerializer.Deserialize<JsonElement>(json);

            var name = doc.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? "Unknown" : "Unknown";

            if (!doc.TryGetProperty("expires_at", out var expiryProp)
                || expiryProp.ValueKind == JsonValueKind.Null)
            {
                TrayContext.ShowBalloon("GitLab Token", $"Token '{name}' has no expiry date.", ToolTipIcon.Info);
                return $"✅ Token '{name}' has no expiry date.";
            }

            if (!DateOnly.TryParse(expiryProp.GetString(), out var expiry))
                return "⚠️ Could not parse expiry date.";

            var daysLeft = expiry.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;

            if (daysLeft < 0)
            {
                TrayContext.ShowBalloon("GitLab Token Expired",
                    $"Token '{name}' expired {Math.Abs(daysLeft)} day{(Math.Abs(daysLeft) == 1 ? "" : "s")} ago ({expiry:dd MMM yyyy}).",
                    ToolTipIcon.Error);
                return $"❌ Token '{name}' expired {Math.Abs(daysLeft)} days ago ({expiry:dd MMM yyyy}).";
            }

            TrayContext.ShowBalloon("GitLab Token",
                $"Token '{name}' expires in {daysLeft} day{(daysLeft == 1 ? "" : "s")} ({expiry:dd MMM yyyy}).",
                daysLeft <= ConfigLoader.AppConfig.TokenExpiryWarnDays ? ToolTipIcon.Warning : ToolTipIcon.Info);

            return $"✅ Token '{name}' expires in {daysLeft} day{(daysLeft == 1 ? "" : "s")} ({expiry:dd MMM yyyy}).";
        }
        catch (Exception ex)
        {
            return $"❌ Could not check token: {ex.Message}";
        }
    }

    private static void PrepareHeaders(string token)
    {
        if (!Http.DefaultRequestHeaders.UserAgent.Any())
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("PandaTools");
        Http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
        Http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
    }
}
