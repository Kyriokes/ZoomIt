using Summit.Core;

namespace Summit.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "summit-tests-" + Guid.NewGuid().ToString("N"));
    private string File_ => Path.Combine(_dir, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static void AssertDefaults(Settings s)
    {
        Assert.Equal(2f, s.Zoom);
        Assert.Equal(.5f, s.AnchorX);
        Assert.Equal(.5f, s.AnchorY);
        Assert.Equal(60, s.Fps);
        Assert.Null(s.Monitor);
    }

    [Fact]
    public void Missing_file_gives_defaults() => AssertDefaults(Settings.Load(File_));

    [Theory]
    [InlineData("{ basura")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    public void Corrupt_file_gives_defaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, content);
        AssertDefaults(Settings.Load(File_));
    }

    [Fact]
    public void Round_trip_keeps_values()
    {
        var s = new Settings { Monitor = @"\\.\DISPLAY2", WindowScreen = @"\\.\DISPLAY1", Zoom = 3.5f, AnchorX = 1, AnchorY = 0, Fps = 0 };
        s.Save(File_);
        var r = Settings.Load(File_);
        Assert.Equal(@"\\.\DISPLAY2", r.Monitor);
        Assert.Equal(@"\\.\DISPLAY1", r.WindowScreen);
        Assert.Equal(3.5f, r.Zoom);
        Assert.Equal(1f, r.AnchorX);
        Assert.Equal(0f, r.AnchorY);
        Assert.Equal(0, r.Fps);
    }

    [Fact]
    public void Load_normalizes_out_of_range_values()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, """{"Zoom":99,"AnchorX":0.4,"AnchorY":0.9,"Fps":7}""");
        var s = Settings.Load(File_);
        Assert.Equal(8f, s.Zoom);
        Assert.Equal(.5f, s.AnchorX);
        Assert.Equal(1f, s.AnchorY);
        Assert.Equal(60, s.Fps);
    }

    [Fact]
    public void Normalize_handles_low_and_nan()
    {
        var s = new Settings { Zoom = -1 };
        s.Normalize();
        Assert.Equal(1f, s.Zoom);

        s = new Settings { Zoom = float.NaN, AnchorX = float.NaN };
        s.Normalize();
        Assert.Equal(2f, s.Zoom);
        Assert.Equal(.5f, s.AnchorX);
    }

    [Fact]
    public void Oversized_file_gives_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, "{\"Zoom\":3," + new string(' ', 100_000) + "\"Fps\":30}");
        AssertDefaults(Settings.Load(File_));
    }

    [Fact]
    public void Save_replaces_an_existing_file_and_leaves_no_temp_file()
    {
        new Settings { Zoom = 3 }.Save(File_);
        new Settings { Zoom = 5 }.Save(File_);
        Assert.Equal(5f, Settings.Load(File_).Zoom);
        Assert.False(File.Exists(File_ + ".tmp"));
    }
}
