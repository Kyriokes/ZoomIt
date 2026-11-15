using Summit.Core;

namespace Summit.Tests;

public class MonitorChoiceTests
{
    [Fact]
    public void Keeps_saved_when_available() => Assert.Equal("B", MonitorChoice.Pick("B", ["A", "B"], "A"));

    [Fact]
    public void Falls_back_to_one_that_is_not_own_screen() => Assert.Equal("B", MonitorChoice.Pick("Z", ["A", "B"], "A"));

    [Fact]
    public void Falls_back_to_first_when_only_own_screen() => Assert.Equal("A", MonitorChoice.Pick(null, ["A"], "A"));

    [Fact]
    public void Empty_list_gives_null() => Assert.Null(MonitorChoice.Pick("Z", [], null));
}
