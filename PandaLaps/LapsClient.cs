using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

public static class LapsClient
{
    //######################################
    //FETCH PASSWORD
    //######################################
    public static async Task<string?> GetLapsPasswordAsync(
        string computerName, LapsConfig cfg, RunAsProfile? queryAs = null)
    {
        if (string.IsNullOrWhiteSpace(computerName)) return null;

        var import  = string.IsNullOrWhiteSpace(cfg.ImportCommand) ? "Import-Module LAPS" : cfg.ImportCommand.Trim();
        var cmdlet  = string.IsNullOrWhiteSpace(cfg.CmdletName)    ? "Get-LapsADPassword"  : cfg.CmdletName.Trim();
        var dc      = cfg.GetActiveDc();
        var dcPart  = string.IsNullOrWhiteSpace(dc) ? "" : $"-DomainController '{EscPs(dc)}' ";

        //Minified to prevent Windows API length limits
        var script = $@"
{import} -EA 0
$r={cmdlet} -Identity '{EscPs(computerName)}' {dcPart}-AsPlainText -EA 0
if($r){{ $r.Password }}";

        var encodedCmd = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

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

    //######################################
    //FETCH EXPIRY (Minified to bypass 1024 char limit)
    //######################################
    public static async Task<string> GetLapsExpiryAsync(string computerName, LapsConfig cfg, RunAsProfile? queryAs = null)
    {
        if (string.IsNullOrWhiteSpace(computerName)) return "Invalid Host";

        var import = string.IsNullOrWhiteSpace(cfg.ImportCommand) ? "Import-Module LAPS" : cfg.ImportCommand.Trim();
        var cmdlet = string.IsNullOrWhiteSpace(cfg.CmdletName)    ? "Get-LapsADPassword"  : cfg.CmdletName.Trim();
        var dc = cfg.GetActiveDc();
        
        string dcParam = "";
        if (!string.IsNullOrWhiteSpace(dc))
        {
            dcParam = cmdlet.Contains("LapsADPassword", StringComparison.OrdinalIgnoreCase) 
                        ? $"-DomainController '{EscPs(dc)}'" 
                        : $"-Server '{EscPs(dc)}'";
        }
        
        //Highly compressed script to ensure Base64 stays under 1024 characters
        var script = $@"
{import} -EA 0
$p={cmdlet} -Identity '{EscPs(computerName)}' {dcParam} -EA 0
if($p){{if($p.ExpirationTimestamp){{$p.ExpirationTimestamp.ToString('g')}}elseif($p.ExpirationTime){{$p.ExpirationTime.ToString('g')}}else{{'No Expiry'}}}}else{{'NotFound'}}";

        var encodedCmd = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        return await Task.Run(() =>
        {
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
                
                proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) sb.AppendLine(e.Data.Trim()); };
                
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit(15000);

                var lines = sb.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    return lines.LastOrDefault()?.Trim() ?? "Parse Error";
                }
                return "No Output Received";
            }
            catch (Exception ex) { return $"C# Error: {ex.Message}"; }
        });
    }

    //######################################
    //SET EXPIRY
    //######################################
    public static async Task<bool> SetLapsExpiryAsync(string computerName, long fileTime, LapsConfig cfg, RunAsProfile? queryAs = null)
    {
        if (string.IsNullOrWhiteSpace(computerName)) return false;

        var dc = cfg.GetActiveDc();
        var dcParam = string.IsNullOrWhiteSpace(dc) ? "" : $"-Server '{EscPs(dc)}'";
        
        var script = $@"
Import-Module ActiveDirectory -EA 0
Set-ADComputer -Identity '{EscPs(computerName)}' {dcParam} -Replace @{{ 'ms-Mcs-AdmPwdExpirationTime' = {fileTime} }} -EA Stop
";
        var encodedCmd = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        return await Task.Run(() =>
        {
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
                proc.Start();
                
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                
                proc.WaitForExit(15000); 

                return proc.ExitCode == 0;
            }
            catch { return false; }
        });
    }

    //######################################
    //HELPERS
    //######################################    
    private static (string domain, string user) SplitUser(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return ("", "");
        if (username.Contains('\\')) { var p = username.Split('\\', 2); return (p[0], p[1]); }
        if (username.Contains('@'))  return (".", username); 
        return (Environment.MachineName, username);
    }

    private static string EscPs(string v) => (v ?? "").Replace("'", "''");
}