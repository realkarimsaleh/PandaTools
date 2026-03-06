using System.Diagnostics;
using System.Text;

public static class TokenManager
{
    private static string? _cachedToken;
    private const string   PS51 = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    public static void Reset() => _cachedToken = null;

    public static string? GetToken()
    {
        if (_cachedToken != null) return _cachedToken;

        var cfg = ConfigLoader.AppConfig;

        // 1) Plaintext token from config.json
        if (!string.IsNullOrWhiteSpace(cfg.Token))
            return _cachedToken = cfg.Token;

        // 2) Encrypted via key + token files from config.json
        if (string.IsNullOrWhiteSpace(cfg.KeyFile)   || !File.Exists(cfg.KeyFile)   ||
            string.IsNullOrWhiteSpace(cfg.TokenFile)  || !File.Exists(cfg.TokenFile) ||
            !File.Exists(PS51))
            return null;

        try
        {
            var script = $@"
$ErrorActionPreference = 'Stop'
$AesKey = Get-Content -Path '{cfg.KeyFile}' -Encoding Default |
    Where-Object   {{ $_ -match '\S' }} |
    ForEach-Object {{ [byte]$_.Trim() }}
$Encrypted   = Get-Content -Path '{cfg.TokenFile}' -Raw
$SecureToken = ConvertTo-SecureString $Encrypted.Trim() -Key $AesKey
[System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureToken)
)";
            var tmp = Path.Combine(Path.GetTempPath(), "PandaTools_Decrypt.ps1");
            File.WriteAllText(tmp, script, Encoding.UTF8);

            var psi = new ProcessStartInfo
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

            using var proc = Process.Start(psi)!;
            var output     = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            File.Delete(tmp);

            var token = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault()?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(token))
                _cachedToken = token;
        }
        catch { }

        return _cachedToken;
    }
}
