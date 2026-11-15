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
}
