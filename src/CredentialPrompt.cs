using System.Drawing;
using System.Windows.Forms;

public static class CredentialPrompt
{
    public static (bool ok, string pwd) Show(string username, string message)
    {
        const int pad = 16, fw = 400, iw = fw - pad * 2 - 16, tw = 32, bw = 80, bg = 8;
        const int mt = 16, mh = 40, ut = mt + mh + 8, pt = ut + 26, bnt = pt + 34, bh = 28, fh = bnt + bh + 48;
        const int bcl = fw - pad - 16 - bw, bol = bcl - bg - bw;

        using var frm = new Form { Text = "Password Required", Size = new Size(fw, fh), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, TopMost = true, Font = new Font("Segoe UI", 9f), Icon = AppIcon.Get() };
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
}