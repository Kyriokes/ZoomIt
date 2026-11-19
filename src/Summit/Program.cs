using System.Diagnostics;
using System.Runtime.InteropServices;
using Summit.Capture;
using Summit.Core;

// Native libraries come only from System32, never from the folder Summit was started from.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace Summit;

internal static class Program
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int cmd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [STAThread]
    private static void Main()
    {
        // One Summit at a time: a second one would double the capture work, and both would
        // overwrite each other's settings. Launching it again brings the open one forward.
        using var instance = new Mutex(true, @"Local\Summit", out bool first);
        if (!first)
        {
            ShowExistingInstance();
            return;
        }

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

    private static void ShowExistingInstance()
    {
        const int SW_RESTORE = 9;
        using var current = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(current.ProcessName))
        {
            using (other)
            {
                if (other.Id == current.Id || other.MainWindowHandle == IntPtr.Zero) continue;
                if (IsIconic(other.MainWindowHandle)) ShowWindow(other.MainWindowHandle, SW_RESTORE);
                SetForegroundWindow(other.MainWindowHandle);
                return;
            }
        }
    }
}
