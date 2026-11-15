using Summit.Capture;
using Summit.Core;

namespace Summit;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        if (!Mirror.IsSupported)
        {
            MessageBox.Show("Esta versión de Windows no permite mostrar un monitor dentro de una ventana.\n" +
                            "Summit necesita Windows 10 versión 2004 o posterior.",
                "Summit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string path = Settings.DefaultPath;
        Application.Run(new MainForm(Settings.Load(path), path));
    }
}
