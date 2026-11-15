namespace Summit.Core;

/// <summary>Decides which incoming frames to keep so the output rate stays at or below <see cref="Fps"/>.</summary>
public sealed class FrameLimiter(long ticksPerSecond)
{
    private long _next = long.MinValue;
    private int _fps;

    /// <summary>Target rate; 0 means unlimited.</summary>
    public int Fps
    {
        get => _fps;
        set { _fps = Math.Max(0, value); _next = long.MinValue; }
    }

    /// <summary>Returns true if a frame arriving at <paramref name="nowTicks"/> should be shown, and records it.</summary>
    public bool ShouldRender(long nowTicks)
    {
        if (_fps <= 0) return true;
        long interval = ticksPerSecond / _fps;
        if (_next != long.MinValue && nowTicks < _next - interval / 8) return false;

        // Keep a steady cadence, but never try to catch up after a gap.
        _next = _next == long.MinValue || nowTicks - _next > interval ? nowTicks + interval : _next + interval;
        return true;
    }
}
