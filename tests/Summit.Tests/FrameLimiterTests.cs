using Summit.Core;

namespace Summit.Tests;

public class FrameLimiterTests
{
    [Fact]
    public void Unlimited_always_renders()
    {
        var l = new FrameLimiter(1000) { Fps = 0 };
        Assert.True(l.ShouldRender(0));
        Assert.True(l.ShouldRender(1));
        Assert.True(l.ShouldRender(2));
    }

    [Fact]
    public void Limits_to_the_interval()
    {
        var l = new FrameLimiter(1000) { Fps = 10 };
        Assert.True(l.ShouldRender(0));
        Assert.False(l.ShouldRender(50));
        Assert.True(l.ShouldRender(100));
        Assert.False(l.ShouldRender(150));
        Assert.True(l.ShouldRender(200));
    }

    [Fact]
    public void Does_not_accumulate_debt_after_a_gap()
    {
        var l = new FrameLimiter(1000) { Fps = 10 };
        Assert.True(l.ShouldRender(0));
        Assert.True(l.ShouldRender(1000));
        Assert.False(l.ShouldRender(1050));
    }

    [Fact]
    public void Tolerates_small_jitter()
    {
        var l = new FrameLimiter(1000) { Fps = 10 };
        Assert.True(l.ShouldRender(0));
        Assert.True(l.ShouldRender(95));
        Assert.True(l.ShouldRender(200));
    }

    // Realistic streams: frames arrive at a monitor's refresh rate, timestamps in 100 ns ticks.

    private static double AcceptedPerSecond(double sourceHz, int cap, int seconds = 10)
    {
        var l = new FrameLimiter(TimeSpan.TicksPerSecond) { Fps = cap };
        int frames = (int)(sourceHz * seconds), accepted = 0;
        for (int i = 0; i < frames; i++)
            if (l.ShouldRender((long)(i * TimeSpan.TicksPerSecond / sourceHz))) accepted++;
        return accepted / (double)seconds;
    }

    [Theory]
    [InlineData(59.94, 60)]
    [InlineData(60.0, 60)]
    [InlineData(75.0, 75)]
    [InlineData(59.94, 120)]
    public void Keeps_every_frame_when_the_cap_is_at_or_above_the_monitor_rate(double hz, int cap)
        => Assert.InRange(AcceptedPerSecond(hz, cap), hz - 0.2, hz + 0.01);

    [Theory]
    [InlineData(75.0, 60, 59.0)]
    [InlineData(120.0, 60, 59.5)]
    [InlineData(144.0, 60, 57.5)]
    [InlineData(60.0, 30, 29.8)]
    [InlineData(75.0, 30, 29.5)]
    [InlineData(60.0, 15, 14.9)]
    public void Holds_the_cap_against_a_faster_monitor(double hz, int cap, double atLeast)
        => Assert.InRange(AcceptedPerSecond(hz, cap), atLeast, cap + 0.01);
}
