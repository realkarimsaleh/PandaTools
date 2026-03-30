using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class TokenManager
{
    private static string? _cachedToken;
    private static string? _cachedAppToken;

    public static void Reset()
    {
        _cachedToken    = null;
        _cachedAppToken = null;
    }

    //######################################
    //Config server token (GitLab or GitHub PAT for config/flavour repo)
    //######################################
    public static string? GetToken()
    {
        if (_cachedToken != null) return _cachedToken;

        var cfg = ConfigLoader.AppConfig;
        if (!string.IsNullOrWhiteSpace(cfg.TokenEncrypted))
        {
            try
            {
                var cipher = Convert.FromBase64String(cfg.TokenEncrypted);
                var plain  = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                _cachedToken = Encoding.UTF8.GetString(plain);
                return _cachedToken;
            }
            catch { }
        }
        return null;
    }

    public static void SaveToken(string plainToken)
    {
        var plain  = Encoding.UTF8.GetBytes(plainToken.Trim());
        var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        var cfg    = ConfigLoader.AppConfig;
        cfg.TokenEncrypted = Convert.ToBase64String(cipher);
        File.WriteAllText(ConfigLoader.ConfigPath, JsonSerializer.Serialize(cfg, ConfigLoader.JsonOpts));
        _cachedToken = null;
    }

    public static bool HasToken()
    {
        var cfg = ConfigLoader.AppConfig;
        return !string.IsNullOrWhiteSpace(cfg.TokenEncrypted);
    }

    //######################################
    //App update token
    //Returns config token if app is on the same server, otherwise app-specific token
    //######################################
    public static string? GetAppToken()
    {
        var cfg = ConfigLoader.AppConfig;

        //Public app repo - no token needed
        if (cfg.AppPublic) return null;

        //Same server as config - reuse config token
        if (cfg.AppSameAsConfig) return GetToken();

        //Separate app token
        if (_cachedAppToken != null) return _cachedAppToken;
        if (!string.IsNullOrWhiteSpace(cfg.AppTokenEncrypted))
        {
            try
            {
                var cipher = Convert.FromBase64String(cfg.AppTokenEncrypted);
                var plain  = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                _cachedAppToken = Encoding.UTF8.GetString(plain);
                return _cachedAppToken;
            }
            catch { }
        }
        return null;
    }

    public static void SaveAppToken(string plainToken)
    {
        var plain  = Encoding.UTF8.GetBytes(plainToken.Trim());
        var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        var cfg    = ConfigLoader.AppConfig;
        cfg.AppTokenEncrypted = Convert.ToBase64String(cipher);
        File.WriteAllText(ConfigLoader.ConfigPath, JsonSerializer.Serialize(cfg, ConfigLoader.JsonOpts));
        _cachedAppToken = null;
    }

    public static bool HasAppToken()
    {
        var cfg = ConfigLoader.AppConfig;
        if (cfg.AppPublic)        return true;   // public = no token needed = considered "ok"
        if (cfg.AppSameAsConfig)  return HasToken();
        return !string.IsNullOrWhiteSpace(cfg.AppTokenEncrypted);
    }

    //######################################
    //Legacy AES/PowerShell decrypt (migration only)
    //######################################
    public static string? LegacyDecrypt(string keyFile, string tokenFile)
    {
        const string PS51 = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        if (!File.Exists(PS51)) return null;
        try
        {
            var script = $@"
$AesKey = Get-Content -Path '{keyFile}' -Encoding Default |
    Where-Object {{ $_ -match '\S' }} |
    ForEach-Object {{ [byte]$_.Trim() }}
$Encrypted = Get-Content -Path '{tokenFile}' -Raw
$SecureToken = ConvertTo-SecureString $Encrypted.Trim() -Key $AesKey
[System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureToken)
)";
            var tmp = Path.Combine(Path.GetTempPath(), "PandaTools_LegacyDecrypt.ps1");
            File.WriteAllText(tmp, script, Encoding.UTF8);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = PS51, Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tmp}\"",
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
            };
            psi.EnvironmentVariables["PSModulePath"] = @"C:\Windows\system32\WindowsPowerShell\v1.0\Modules";
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            File.Delete(tmp);
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        }
        catch { return null; }
    }
}
