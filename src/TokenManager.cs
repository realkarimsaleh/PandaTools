using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class TokenManager
{
    private static string? _cachedToken;

    public static void Reset() => _cachedToken = null;

    /// <summary>Returns the decrypted plain-text token, or null if not set.</summary>
    // PLATFORM NOTE: Uses Windows DPAPI (ProtectedData).
    // For macOS port, replace Protect/Unprotect with Keychain interop.
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
            catch { /* corrupted or wrong user — fall through */ }
        }

        // Legacy plain-text keyFile/tokenFile path (kept for migration)
        if (!string.IsNullOrWhiteSpace(cfg.KeyFile) && File.Exists(cfg.KeyFile)
            && !string.IsNullOrWhiteSpace(cfg.TokenFile) && File.Exists(cfg.TokenFile))
        {
            _cachedToken = LegacyDecrypt(cfg.KeyFile, cfg.TokenFile);
            return _cachedToken;
        }

        return null;
    }

    /// <summary>Encrypts and persists a new token to config.json via DPAPI.</summary>
    public static void SaveToken(string plainToken)
    {
        var plain  = Encoding.UTF8.GetBytes(plainToken.Trim());
        var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        var b64    = Convert.ToBase64String(cipher);

        var cfg = ConfigLoader.AppConfig;
        cfg.TokenEncrypted = b64;
        // Clear legacy fields
        cfg.KeyFile   = "";
        cfg.TokenFile = "";

        File.WriteAllText(ConfigLoader.ConfigPath,
            JsonSerializer.Serialize(cfg, ConfigLoader.JsonOpts));

        Reset();
    }

    /// <summary>Returns true if a token (encrypted or legacy) is configured.</summary>
    public static bool HasToken()
    {
        var cfg = ConfigLoader.AppConfig;
        return !string.IsNullOrWhiteSpace(cfg.TokenEncrypted)
            || (!string.IsNullOrWhiteSpace(cfg.KeyFile) && File.Exists(cfg.KeyFile));
    }

    // ── Legacy AES/PowerShell decrypt (migration path) ─────────────────
    private static string? LegacyDecrypt(string keyFile, string tokenFile)
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
                FileName               = PS51,
                Arguments              = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tmp}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };
            psi.EnvironmentVariables["PSModulePath"] =
                @"C:\Windows\system32\WindowsPowerShell\v1.0\Modules";

            using var proc = System.Diagnostics.Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            File.Delete(tmp);

            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .LastOrDefault()?.Trim();
        }
        catch { return null; }
    }
}
