namespace Summit.Core;

public readonly record struct RectF(float X, float Y, float Width, float Height);

public static class ViewMath
{
    public const float MinZoom = 1f;
    public const float MaxZoom = 8f;

    /// <summary>
    /// Where to draw and what to sample. At 1x the whole source fits in the destination; as
    /// zoom grows the image first fills any empty bars and then crops toward the anchor.
    /// </summary>
    /// <returns>Destination rectangle in pixels and source rectangle in 0..1 coordinates.</returns>
    public static (RectF Dest, RectF Source) Fit(int srcW, int srcH, int dstW, int dstH, float zoom, float anchorX, float anchorY)
    {
        var fit = Letterbox(srcW, srcH, dstW, dstH);
        if (fit.Width <= 0 || fit.Height <= 0) return default;

        zoom = float.IsNaN(zoom) ? MinZoom : Math.Clamp(zoom, MinZoom, MaxZoom);
        var (x, w, u, uw) = FitAxis(fit.Width * zoom, dstW, ClampAnchor(anchorX));
        var (y, h, v, vh) = FitAxis(fit.Height * zoom, dstH, ClampAnchor(anchorY));
        return (new RectF(x, y, w, h), new RectF(u, v, uw, vh));
    }

    private static (float Pos, float Size, float SrcPos, float SrcSize) FitAxis(float image, float view, float anchor)
    {
        if (image <= view) return ((view - image) / 2f, image, 0f, 1f);
        float visible = view / image;
        return (0f, view, anchor * (1f - visible), visible);
    }

    private static float ClampAnchor(float a) => float.IsNaN(a) ? 0.5f : Math.Clamp(a, 0f, 1f);

    /// <summary>Largest rectangle with the source's aspect ratio centered in the destination, in pixels.</summary>
    public static RectF Letterbox(int srcW, int srcH, int dstW, int dstH)
    {
        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) return default;
        float scale = Math.Min((float)dstW / srcW, (float)dstH / srcH);
        float w = srcW * scale, h = srcH * scale;
        return new RectF((dstW - w) / 2f, (dstH - h) / 2f, w, h);
    }
}
