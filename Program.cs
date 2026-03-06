using System.Windows.Forms;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
ConfigLoader.Load();
Application.Run(new TrayContext());
