using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace FileManagerNet.Services;

/// <summary>
/// Extracts the icon Windows associates with a file extension — i.e. the icon
/// of the program registered as the default handler — so the list can look like
/// Explorer. Only available on Windows; elsewhere the built-in SVG icons are
/// used. Results (including "no association") are cached per extension + size.
/// </summary>
public static class SystemIconService
{
    private static readonly ConcurrentDictionary<string, byte[]?> Icons = new();
    private static readonly ConcurrentDictionary<int, byte[]> Unknown = new();

    /// <summary>True when the current platform can extract shell icons (Windows only).</summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>Returns PNG bytes for the icon of <paramref name="extension"/>,
    /// or <c>null</c> when the platform is unsupported or no program is
    /// associated with the extension.</summary>
    /// <param name="extension">File extension, with or without the dot.</param>
    /// <param name="size">Requested icon size in pixels (clamped to 16–64).</param>
    public static byte[]? GetPng(string extension, int size)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var ext = Normalize(extension);
        if (ext is null) return null;
        size = Math.Clamp(size, 16, 64);

        var key = ext + "@" + size;
        if (Icons.TryGetValue(key, out var cached)) return cached;

        var png = Extract(ext, size);
        var generic = GetUnknown(size);
        if (png is not null && generic is not null && png.AsSpan().SequenceEqual(generic))
            png = null;                      // no real association, keep the SVG icon

        Icons[key] = png;
        return png;
    }

    /// <summary>Validates and normalizes an extension ("DOCX" → "docx").</summary>
    private static string? Normalize(string extension)
    {
        var ext = (extension ?? "").Trim().TrimStart('.').ToLowerInvariant();
        if (ext.Length is 0 or > 16) return null;
        foreach (var c in ext) if (!char.IsLetterOrDigit(c)) return null;
        return ext;
    }

    /// <summary>Returns the generic "unknown file" icon, used to detect that an
    /// extension has no real association.</summary>
    [SupportedOSPlatform("windows")]
    private static byte[]? GetUnknown(int size)
    {
        if (Unknown.TryGetValue(size, out var hit)) return hit;
        var png = Extract("fmnetunknown", size);
        if (png is not null) Unknown[size] = png;
        return png;
    }

    /// <summary>Extracts and rasterizes the icon Windows associates with an
    /// extension. Returns <c>null</c> on any failure.</summary>
    [SupportedOSPlatform("windows")]
    private static byte[]? Extract(string ext, int size)
    {
        var probe = Path.Combine(Path.GetTempPath(),
            "fmnet-ico-" + Guid.NewGuid().ToString("N") + "." + ext);
        try
        {
            File.WriteAllBytes(probe, Array.Empty<byte>());
            using var icon = Icon.ExtractAssociatedIcon(probe);
            if (icon is null) return null;

            using var source = icon.ToBitmap();
            using var target = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(target))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(source, 0, 0, size, size);
            }

            using var ms = new MemoryStream();
            target.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
        finally
        {
            try { File.Delete(probe); } catch { /* best effort */ }
        }
    }
}
