using System.Text.Json;

namespace Summit.Core;

public sealed class Settings
{
    public static readonly int[] FpsOptions = [15, 30, 60, 120, 0];

    private const float DefaultZoom = 2f;
    private const int DefaultFps = 60;

    /// <summary>A real settings file is a few hundred bytes; anything far larger is not ours.</summary>
    private const long MaxFileSize = 64 * 1024;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Device name of the monitor being shown.</summary>
    public string? Monitor { get; set; }

    /// <summary>Device name of the monitor the window was last on.</summary>
    public string? WindowScreen { get; set; }

    public float Zoom { get; set; } = DefaultZoom;
    public float AnchorX { get; set; } = 0.5f;
    public float AnchorY { get; set; } = 0.5f;

    /// <summary>Frames per second; 0 means the monitor's maximum.</summary>
    public int Fps { get; set; } = DefaultFps;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Summit", "settings.json");

    public void Normalize()
    {
        Zoom = float.IsFinite(Zoom) ? Math.Clamp(Zoom, ViewMath.MinZoom, ViewMath.MaxZoom) : DefaultZoom;
        AnchorX = SnapAnchor(AnchorX);
        AnchorY = SnapAnchor(AnchorY);
        if (Array.IndexOf(FpsOptions, Fps) < 0) Fps = DefaultFps;
    }

    private static float SnapAnchor(float v)
        => float.IsFinite(v) ? MathF.Round(Math.Clamp(v, 0f, 1f) * 2f) / 2f : 0.5f;

    public static Settings Load(string path)
    {
        Settings? s = null;
        try
        {
            var file = new FileInfo(path);
            if (file.Exists && file.Length <= MaxFileSize)
            {
                using var stream = file.OpenRead();
                s = JsonSerializer.Deserialize<Settings>(stream);
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // An unreadable file must never keep the app from starting.
        }
        s ??= new Settings();
        s.Normalize();
        return s;
    }

    /// <summary>
    /// Writes the file as a whole: a new copy is written and flushed first, then swapped in, so
    /// a crash or power cut mid-save leaves the previous settings intact.
    /// </summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, this, WriteOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
