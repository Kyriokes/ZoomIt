namespace Summit.Core;

public static class MonitorChoice
{
    /// <summary>
    /// Picks the monitor to show: the saved one if still connected, otherwise the first that
    /// isn't the window's own screen, otherwise the first available.
    /// </summary>
    public static string? Pick(string? saved, IReadOnlyList<string> available, string? ownScreen)
    {
        if (available.Count == 0) return null;
        if (saved is not null && available.Contains(saved)) return saved;
        return available.FirstOrDefault(m => m != ownScreen) ?? available[0];
    }
}
