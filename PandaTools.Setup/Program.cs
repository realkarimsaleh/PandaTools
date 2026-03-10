using System;
using System.Security.Principal;
using System.Windows.Forms;

static class SetupProgram
{
    [STAThread]
    static void Main(string[] args)
    {
        if (!IsElevated())
        {
            Elevate(args);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length > 0 && args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            Application.Run(new UninstallerForm());
        else
            Application.Run(new InstallerForm());
    }

    static bool IsElevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    static void Elevate(string[] args)
    {
        var exe = Environment.ProcessPath
            ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = exe,
                Arguments       = string.Join(" ", args),
                UseShellExecute = true,
                Verb            = "runas"
            });
        }
        catch { /* user cancelled UAC */ }
    }
}
