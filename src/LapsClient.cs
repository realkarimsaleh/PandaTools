using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Threading.Tasks;

public static class LapsClient
{
    ///<summary>
    ///Retrieves the LAPS password for <paramref name="computerName"/>.
    ///Mirrors the working GetLAPS.ps1 pattern exactly.
    ///If <paramref name="queryAs"/> is supplied with credentials the PS process
    ///runs under those credentials.
    ///</summary>
    public static async Task<string?> GetLapsPasswordAsync(
        string computerName, LapsConfig cfg, RunAsProfile? queryAs = null)
    {
        if (string.IsNullOrWhiteSpace(computerName)) return null;

        var import  = string.IsNullOrWhiteSpace(cfg.ImportCommand) ? "Import-Module LAPS" : cfg.ImportCommand.Trim();
        var cmdlet  = string.IsNullOrWhiteSpace(cfg.CmdletName)    ? "Get-LapsADPassword"  : cfg.CmdletName.Trim();
        var dc      = cfg.GetActiveDc();
        var dcPart  = string.IsNullOrWhiteSpace(dc) ? "" : $"-DomainController '{EscPs(dc)}' ";

        var script = $@"
{import} -ErrorAction Stop
$r = {cmdlet} -Identity '{EscPs(computerName)}' {dcPart}-AsPlainText -ErrorAction Stop
if ($r -ne $null) {{ $r.Password }}";

        var scriptBytes = Encoding.Unicode.GetBytes(script);
        var encodedCmd  = Convert.ToBase64String(scriptBytes);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCmd}",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            if (queryAs != null && !string.IsNullOrWhiteSpace(queryAs.Password))
            {
                var (domain, user) = SplitUser(queryAs.Username);
                psi.UserName        = user;
                psi.Domain          = domain;
                psi.LoadUserProfile = true;

                var ss = new SecureString();
                foreach (var c in queryAs.Password) ss.AppendChar(c);
                ss.MakeReadOnly();
                psi.Password = ss;
            }

            using var proc = new Process { StartInfo = psi };
            var sb = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await Task.Run(() => proc.WaitForExit(15000));

            if (proc.ExitCode != 0) return null;

            var lines = sb.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 ? lines[^1].Trim() : null;
        }
        catch { return null; }
    }

    private static (string domain, string user) SplitUser(string username)
    {
        if (username.Contains('\\')) { var p = username.Split('\\', 2); return (p[0], p[1]); }
        if (username.Contains('@'))  return (".", username);
        return (Environment.MachineName, username);
    }

    private static string EscPs(string v) => (v ?? "").Replace("'", "''");
}