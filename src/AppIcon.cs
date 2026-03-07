using System.Drawing;
using System.Windows.Forms;

public static class AppIcon
{
    private static Icon? _icon;

    public static Icon Get()
    {
        if (_icon != null) return _icon;

        var icoPath = Path.Combine(Application.StartupPath, "assets", "PandaTools.ico");
        if (File.Exists(icoPath))
            _icon = new Icon(icoPath);

        return _icon ?? SystemIcons.Application;
    }
}
