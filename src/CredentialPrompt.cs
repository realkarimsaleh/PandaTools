using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security;
using System.Windows.Forms;

public static class CredentialPrompt
{
    //######################################
    //Core UI Prompt
    //######################################
    public static (bool ok, string pwd) Show(string username, string message, string appName = "")
    {
        var title = string.IsNullOrWhiteSpace(appName) ? "Password Prompt" : appName;
        const int pad = 16, fw = 400, iw = fw - pad * 2 - 16, tw = 32, bw = 80, bg = 8;
        const int mt = 16, mh = 60, ut = mt + mh + 8, pt = ut + 26, bnt = pt + 34, bh = 28, fh = bnt + bh + 48;
        const int bcl = fw - pad - 16 - bw, bol = bcl - bg - bw;

        using var frm = new Form { Text = title, Size = new Size(fw, fh), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, TopMost = true, Font = new Font("Segoe UI", 9f), Icon = AppIcon.Get() };
        frm.Controls.Add(new Label { Text = message, Left = pad, Top = mt, Width = iw, Height = mh, Font = new Font("Segoe UI", 9f), AutoSize = false });
        frm.Controls.Add(new Label { Text = "Username:", Left = pad, Top = ut, Width = 74, Height = 20, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 9f), AutoSize = false });
        frm.Controls.Add(new Label { Text = username, Left = pad + 76, Top = ut, Width = iw - 76, Height = 20, Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = false });
        
        var txt = new TextBox { Left = pad, Top = pt, Width = iw - tw - 4, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 9f) }; 
        frm.Controls.Add(txt);
        
        var tog = new Button { Text = "👁", Left = pad + iw - tw, Top = pt - 1, Width = tw, Height = txt.Height + 2, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f), TabStop = false, Cursor = Cursors.Hand };
        tog.FlatAppearance.BorderSize = 1; 
        tog.Click += (_, _) => { txt.UseSystemPasswordChar = !txt.UseSystemPasswordChar; txt.Focus(); txt.SelectionStart = txt.Text.Length; }; 
        frm.Controls.Add(tog);
        
        var ok = new Button { Text = "OK", Left = bol, Top = bnt, Width = bw, Height = bh, DialogResult = DialogResult.OK, Font = new Font("Segoe UI", 9f) };
        var can = new Button { Text = "Cancel", Left = bcl, Top = bnt, Width = bw, Height = bh, DialogResult = DialogResult.Cancel, Font = new Font("Segoe UI", 9f) };
        
        frm.Controls.AddRange(new Control[] { ok, can }); 
        frm.AcceptButton = ok; 
        frm.CancelButton = can; 
        frm.Shown += (_, _) => txt.Focus();
        
        var r = frm.ShowDialog(); 
        return (r == DialogResult.OK, txt.Text);
    }

    //######################################
    //Main RunAs Execution Logic
    //######################################
    public static void LaunchWithRunAs(string resolvedExe, string resolvedArgs, RunAsProfile? profile, string appName = "")
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.Username))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = resolvedExe,
                    Arguments       = resolvedArgs,
                    UseShellExecute = true,
                    Verb            = "runas"
                });
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { } // UAC Cancelled
            catch (Exception ex) { ShowError(ex); }
            return;
        }

        var (domain, user) = SplitDomainUser(profile.Username);

        if (string.IsNullOrEmpty(profile.Password))
        {
            HandlePasswordLoop(resolvedExe, resolvedArgs, user, domain, profile, firstAttempt: true, offerSave: false, appName: appName);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = resolvedExe,
                Arguments       = resolvedArgs,
                UseShellExecute = false,
                UserName        = user,
                Domain          = domain,
                Password        = ToSecureString(profile.Password),
                LoadUserProfile = true
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { } // UAC Cancelled
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1326)     // Bad Password
        {
            HandlePasswordLoop(resolvedExe, resolvedArgs, user, domain, profile, firstAttempt: false, offerSave: true, appName: appName);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    //######################################
    //The Retry Loop
    //######################################
    private static void HandlePasswordLoop(string resolvedExe, string resolvedArgs, string user, string domain, RunAsProfile? profile, bool firstAttempt, bool offerSave, string appName = "")
    {
        var profileName = profile?.Name ?? user;
        var username    = profile?.Username ?? user;
        bool isFirst    = firstAttempt;

        while (true)
        {
            var promptMsg = isFirst
                ? $"Enter password for \"{profileName}\""
                : $"Incorrect password for \"{profileName}\".\n\nPlease try again";

            isFirst = false;

            var (ok, plainText) = Show(username, promptMsg, appName);
            if (!ok) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = resolvedExe,
                    Arguments       = resolvedArgs,
                    UseShellExecute = false,
                    UserName        = user,
                    Domain          = domain,
                    Password        = ToSecureString(plainText),
                    LoadUserProfile = true
                });

                if (offerSave && profile != null)
                {
                    if (MessageBox.Show($"Password updated successfully for \"{profileName}\".\n\nSave the new password?", "Save Password?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        SaveProfilePassword(profile.Name, plainText);
                    }
                }
                return;
            }

            //UAC Cancelled
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return; }

            //Bad Password, loop restarts
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1326) { }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }
        }
    }

    //######################################
    //Helpers
    //######################################
    private static void SaveProfilePassword(string profileName, string plainPassword)
    {
        var cfg   = ConfigLoader.AppConfig;
        var match = cfg.RunAsProfiles.FirstOrDefault(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;
        match.Password = plainPassword;
        ConfigLoader.Save(cfg);
    }

    private static (string domain, string user) SplitDomainUser(string username)
    {
        if (username.Contains('\\'))
        {
            var parts = username.Split('\\', 2);
            return (parts[0], parts[1]);
        }
        if (username.Contains('@')) return (".", username);
        return (Environment.MachineName, username);
    }

    private static SecureString ToSecureString(string input)
    {
        var s = new SecureString();
        foreach (var c in input) s.AppendChar(c);
        s.MakeReadOnly();
        return s;
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show($"RunAs failed:\n{ex.Message}", "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}