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
    //######################################
    //Returns SecureString - plain text never exists as a managed string
    //######################################
    public static (bool ok, SecureString? pwd) Show(string username, string message, string appName = "")
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

        var ok  = new Button { Text = "OK",     Left = bol, Top = bnt, Width = bw, Height = bh, DialogResult = DialogResult.OK,     Font = new Font("Segoe UI", 9f) };
        var can = new Button { Text = "Cancel", Left = bcl, Top = bnt, Width = bw, Height = bh, DialogResult = DialogResult.Cancel, Font = new Font("Segoe UI", 9f) };

        frm.Controls.AddRange(new Control[] { ok, can });
        frm.AcceptButton = ok;
        frm.CancelButton = can;
        frm.Shown += (_, _) => txt.Focus();

        var result = frm.ShowDialog();
        if (result != DialogResult.OK) return (false, null);

        // Convert TextBox content to SecureString char by char - never read .Text into a variable
        var secure = new SecureString();
        foreach (var c in txt.Text) secure.AppendChar(c);
        secure.MakeReadOnly();

        // Clear the textbox immediately so the string in WinForms internal buffer is gone
        txt.Clear();

        return (true, secure);
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

        // Decrypt saved password directly to SecureString - no plain string created
        using var savedPassword = profile.HasSavedPassword ? profile.DecryptToSecureString() : null;

        if (savedPassword == null)
        {
            // No saved password - prompt immediately
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
                Password        = savedPassword,
                LoadUserProfile = true
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { } // UAC Cancelled
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1326)     // Bad password - prompt for new one
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

            var (ok, securePassword) = Show(username, promptMsg, appName);
            if (!ok || securePassword == null) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = resolvedExe,
                    Arguments       = resolvedArgs,
                    UseShellExecute = false,
                    UserName        = user,
                    Domain          = domain,
                    Password        = securePassword,  // SecureString passed directly - no plain string
                    LoadUserProfile = true
                });

                if (offerSave && profile != null)
                {
                    if (MessageBox.Show($"Password updated successfully for \"{profileName}\".\n\nSave the new password?", "Save Password?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        SaveProfilePassword(profile.Name, securePassword);
                    }
                }
                return;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return; }   // UAC Cancelled
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1326) { }            // Bad password - loop
            catch (Exception ex) { ShowError(ex); return; }
            finally
            {
                // Always dispose SecureString after each attempt
                securePassword.Dispose();
            }
        }
    }

    //######################################
    //Helpers
    //######################################
    private static void SaveProfilePassword(string profileName, SecureString securePassword)
    {
        var cfg   = ConfigLoader.AppConfig;
        var match = cfg.RunAsProfiles.FirstOrDefault(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;
        // Encrypt directly from SecureString - no plain string ever created
        match.EncryptFromSecureString(securePassword);
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

    private static void ShowError(Exception ex)
    {
        MessageBox.Show($"RunAs failed:\n{ex.Message}", "PandaTools Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}