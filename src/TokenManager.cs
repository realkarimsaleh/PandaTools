using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

public static class TokenManager
{
    private const string KeyPath   = @"C:\Windows\Build\Sync-Gitlab\K_Sync-Gitlab.txt";
    private const string TokenPath = @"C:\Windows\Build\Sync-Gitlab\C_Sync-Gitlab.txt";

    //  Debug override — set to raw token to bypass decryption during dev
    //  Leave as "" in production — never commit a real token here
    private static readonly string PlainTextToken = "";

    //  Cached token — decrypted once per session, held in memory only
    private static string? _cachedToken = null;

    //  Hardcoded to Windows PowerShell 5.1 — never resolves to pwsh/PS7
    private const string PS51 = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    //  Called on app startup to clear any stale cached token
    public static void Reset() => _cachedToken = null;

    public static string? GetToken()
    {
        if (_cachedToken != null) return _cachedToken;

        if (PlainTextToken != "")
        {
            _cachedToken = PlainTextToken;
            return _cachedToken;
        }

        if (!File.Exists(KeyPath) || !File.Exists(TokenPath) || !File.Exists(PS51))
            return null;

        try
        {
            var script = $@"
$ErrorActionPreference = 'Stop'
$AesKey = Get-Content -Path '{KeyPath}' -Encoding Default |
    Where-Object   {{ $_ -match '\S' }} |
    ForEach-Object {{ [byte]$_.Trim() }}
$Encrypted   = Get-Content -Path '{TokenPath}' -Raw
$SecureToken = ConvertTo-SecureString $Encrypted.Trim() -Key $AesKey
[System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureToken)
)";

            var tempScript = Path.Combine(Path.GetTempPath(), "PandaTools_Decrypt.ps1");
            File.WriteAllText(tempScript, script, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName               = PS51,
                Arguments              = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tempScript}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            //  Prevent PS7 module paths bleeding in — causes type data conflicts
            psi.EnvironmentVariables["PSModulePath"] =
                @"C:\Windows\system32\WindowsPowerShell\v1.0\Modules";

            using var proc = Process.Start(psi)!;
            var stdOut     = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            File.Delete(tempScript);

            var token = stdOut
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault()
                ?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(token))
                _cachedToken = token;
        }
        catch { }

        return _cachedToken;
    }
}
