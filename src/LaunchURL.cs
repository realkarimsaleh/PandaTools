using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public static class LaunchURL
{
    //######################################
    //URL Launcher
    //######################################
    public static void Open(List<string> urls, string browserName, string customPath, bool isIncognito, RunAsProfile? profile)
    {
        if (urls == null || urls.Count == 0) return;

        // Combine all URLs into a single space-separated string: "url1" "url2"
        string urlArgs = string.Join(" ", urls.Select(u => $"\"{u}\""));
        
        string exePath = "";
        string flag = "";

        //Resolve Executable and Flag
        if (isIncognito)
        {
            var resolved = ResolveIncognitoBrowser(browserName, customPath);
            exePath = resolved.exePath;
            flag = resolved.flag;
        }
        else
        {
            if (browserName.Equals("default", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(browserName))
            {
                if (profile != null) 
                {
                    exePath = FindBrowser("edge");
                    if (string.IsNullOrEmpty(exePath)) exePath = FindBrowser("chrome");
                    if (string.IsNullOrEmpty(exePath)) exePath = FindBrowser("firefox");
                    if (string.IsNullOrEmpty(exePath)) exePath = FindBrowser("brave");
                }
            }
            else
            {
                exePath = ResolveBrowserExe(browserName, customPath);
            }
        }

        //Add the incognito flag to the front of the URLs if needed
        string args = string.IsNullOrWhiteSpace(flag) ? urlArgs : $"{flag} {urlArgs}";

        //Execution Routing
        if (profile != null)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                MessageBox.Show($"Could not locate a browser for RunAs launch.\n\nConfigure it in Settings → Browser.", 
                    "PandaTools – Browser Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            CredentialPrompt.LaunchWithRunAs(exePath, args, profile, "Browser");
        }
        else
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, Arguments = args, UseShellExecute = true });
                }
                else
                {
                    //If no explicit browser is found, we can't easily pass multiple URLs to the default shell handler reliably.
                    //Fallback to launching them individually via the OS default handler.
                    foreach (var u in urls)
                    {
                        Process.Start(new ProcessStartInfo(u) { UseShellExecute = true });
                    }
                }
            }
            catch
            {
                foreach (var u in urls)
                {
                    Process.Start(new ProcessStartInfo(u) { UseShellExecute = true });
                }
            }
        }
    }

    //######################################
    //Browser resolution helpers
    //######################################
    private static string ResolveBrowserExe(string browserName, string customPath) =>
        browserName.ToLowerInvariant() switch
        {
            "chrome"  => FindBrowser("chrome"),
            "edge"    => FindBrowser("edge"),
            "firefox" => FindBrowser("firefox"),
            "brave"   => FindBrowser("brave"),
            "custom"  => customPath,
            _         => ""
        };

    private static (string exePath, string flag) ResolveIncognitoBrowser(string browserName, string customPath) =>
        browserName.ToLowerInvariant() switch
        {
            "chrome"  => (FindBrowser("chrome"),  "--incognito"),
            "edge"    => (FindBrowser("edge"),    "--inprivate"),
            "firefox" => (FindBrowser("firefox"), "-private-window"),
            "brave"   => (FindBrowser("brave"),   "--incognito"),
            "custom"  => (customPath,             "--incognito"),
            _         => (FindBrowser("edge"),    "--inprivate")
        };

    private static string FindBrowser(string name) => name switch
    {
        "chrome"  => FirstExisting(
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"),
        "edge"    => FirstExisting(
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
        "firefox" => FirstExisting(
            @"C:\Program Files\Mozilla Firefox\firefox.exe",
            @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"),
        "brave"   => FirstExisting(
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
            @"C:\Program Files (x86)\BraveSoftware\Brave-Browser\Application\brave.exe"),
        _         => ""
    };

    private static string FirstExisting(params string[] paths) =>
        paths.FirstOrDefault(File.Exists) ?? "";
}