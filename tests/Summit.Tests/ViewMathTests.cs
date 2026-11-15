using Summit.Core;

namespace Summit.Tests;

public class ViewMathTests
{
    private static void AssertRect(RectF expected, RectF actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Width, actual.Width, 3);
        Assert.Equal(expected.Height, actual.Height, 3);
    }

    [Theory]
    [InlineData(2f, .5f, .5f, .25f, .25f, .5f, .5f)]
    [InlineData(4f, 0f, 0f, 0f, 0f, .25f, .25f)]
    [InlineData(4f, 1f, 1f, .75f, .75f, .25f, .25f)]
    [InlineData(2f, 1f, 0f, .5f, 0f, .5f, .5f)]
    [InlineData(4f, .5f, 1f, .375f, .75f, .25f, .25f)]
    public void Fit_follows_zoom_and_anchor(float zoom, float ax, float ay, float x, float y, float w, float h)
        => AssertRect(new RectF(x, y, w, h), ViewMath.Fit(1920, 1080, 1920, 1080, zoom, ax, ay).Source);

    [Fact]
    public void Fit_clamps_zoom_and_anchor()
    {
        AssertRect(new RectF(0, 0, 1, 1), ViewMath.Fit(1920, 1080, 1920, 1080, 0.2f, 1, 1).Source);
        AssertRect(ViewMath.Fit(1920, 1080, 1920, 1080, 8, .5f, .5f).Source, ViewMath.Fit(1920, 1080, 1920, 1080, 50, .5f, .5f).Source);
        AssertRect(ViewMath.Fit(1920, 1080, 1920, 1080, 2, 1, .5f).Source, ViewMath.Fit(1920, 1080, 1920, 1080, 2, 7, float.NaN).Source);
    }

    [Fact]
    public void Letterbox_same_aspect_fills()
        => AssertRect(new RectF(0, 0, 1920, 1080), ViewMath.Letterbox(1920, 1080, 1920, 1080));

    [Fact]
    public void Letterbox_wide_source_in_square_target()
        => AssertRect(new RectF(0, 218.75f, 1000, 562.5f), ViewMath.Letterbox(1920, 1080, 1000, 1000));

    [Fact]
    public void Letterbox_tall_source_in_wide_target()
        => AssertRect(new RectF(656.25f, 0, 607.5f, 1080), ViewMath.Letterbox(1080, 1920, 1920, 1080));

    [Theory]
    [InlineData(1920, 1080, 0, 500)]
    [InlineData(1920, 1080, 500, -1)]
    [InlineData(0, 1080, 500, 500)]
    public void Letterbox_empty_sizes_give_empty_rect(int sw, int sh, int dw, int dh)
        => Assert.Equal(default, ViewMath.Letterbox(sw, sh, dw, dh));

    // Fit: at 1x the whole source is visible; as zoom grows the image fills the bars before cropping.

    [Fact]
    public void Fit_at_1x_matches_letterbox_and_shows_everything()
    {
        var (dest, src) = ViewMath.Fit(1920, 1080, 1000, 1000, 1f, .5f, .5f);
        AssertRect(new RectF(0, 218.75f, 1000, 562.5f), dest);
        AssertRect(new RectF(0, 0, 1, 1), src);
    }

    [Fact]
    public void Fit_same_aspect_crops_both_axes()
    {
        var (dest, src) = ViewMath.Fit(1920, 1080, 1920, 1080, 2f, .5f, .5f);
        AssertRect(new RectF(0, 0, 1920, 1080), dest);
        AssertRect(new RectF(.25f, .25f, .5f, .5f), src);
    }

    [Fact]
    public void Fit_fills_the_bars_before_cropping_that_axis()
    {
        // Image is 1000x562.5 at 1x; at 1.6x it is 1600x900: wider than the view, still shorter.
        var (dest, src) = ViewMath.Fit(1920, 1080, 1000, 1000, 1.6f, 0f, 0f);
        AssertRect(new RectF(0, 50, 1000, 900), dest);
        AssertRect(new RectF(0, 0, .625f, 1), src);
    }

    [Fact]
    public void Fit_fills_the_view_once_zoom_covers_it()
    {
        // At 2x the image is 2000x1125: both axes overflow, anchored bottom-right.
        var (dest, src) = ViewMath.Fit(1920, 1080, 1000, 1000, 2f, 1f, 1f);
        AssertRect(new RectF(0, 0, 1000, 1000), dest);
        AssertRect(new RectF(.5f, 1 - 1000f / 1125f, .5f, 1000f / 1125f), src);
    }

    [Fact]
    public void Fit_empty_sizes_give_empty_rects()
    {
        var (dest, src) = ViewMath.Fit(1920, 1080, 0, 0, 2f, .5f, .5f);
        Assert.Equal(default, dest);
        Assert.Equal(default, src);
    }
}
