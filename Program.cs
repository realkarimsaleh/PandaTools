using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "Global\\PandaTools_SingleInstance", out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "PandaTools is already running.\n\nCheck the system tray ( ↑ in the taskbar).",
                "PandaTools", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayContext());
    }
}
