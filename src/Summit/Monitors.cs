using System.Runtime.InteropServices;

namespace Summit;

internal sealed record MonitorInfo(string DeviceName, string Label, IntPtr Handle, Rectangle Bounds)
{
    public override string ToString() => Label;
}

internal static class Monitors
{
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point pt, uint flags);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    public static IReadOnlyList<MonitorInfo> All()
    {
        var screens = Screen.AllScreens;
        var list = new List<MonitorInfo>(screens.Length);
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var center = new Point(s.Bounds.Left + s.Bounds.Width / 2, s.Bounds.Top + s.Bounds.Height / 2);
            string label = $"Monitor {i + 1} — {s.Bounds.Width}×{s.Bounds.Height}" + (s.Primary ? " (principal)" : "");
            list.Add(new MonitorInfo(s.DeviceName, label, MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST), s.Bounds));
        }
        return list;
    }
}
