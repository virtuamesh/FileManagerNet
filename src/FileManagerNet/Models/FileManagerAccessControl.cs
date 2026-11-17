using System.Text.Json.Serialization;

namespace FileManagerNet.Models;

/// <summary>
/// One access-control entry mounted on a folder. The entry applies to that
/// folder and, unless a more specific entry exists below it, to everything
/// underneath: subfolders inherit the <em>nearest</em> ancestor entry rather
/// than merging every level.
/// <code>
/// root.AccessControls.Add(new FileManagerAccessControl {
///     Path    = "/",                     // the whole tree is read-only
///     Allowed = FileManagerPermissions.ReadOnly
/// });
/// root.AccessControls.Add(new FileManagerAccessControl {
///     Path             = "/public/upload", // except here: images only
///     Allowed          = FileManagerPermissions.ReadOnly
///                        | FileManagerPermissions.Upload
///                        | FileManagerPermissions.Create,
///     AllowedFileTypes = "*.jpg|*.png",
///     Quota            = "500 MB"
/// });
/// </code>
/// <see cref="Denied"/> always wins over <see cref="Allowed"/>.
/// </summary>
public sealed class FileManagerAccessControl
{
    /// <summary>
    /// Virtual folder this entry is mounted on, for example <c>/</c> or
    /// <c>/finance</c>. Wildcards are supported per segment
    /// (<c>/dept-*/docs</c>): <c>*</c> matches within one segment and
    /// <c>**</c> matches across segments.
    /// </summary>
    public string Path { get; set; } = "/";

    /// <summary>Permissions granted by this entry.</summary>
    public FileManagerPermissions Allowed { get; set; } = FileManagerPermissions.Full;

    /// <summary>Permissions removed again; takes precedence over <see cref="Allowed"/>.</summary>
    public FileManagerPermissions Denied { get; set; } = FileManagerPermissions.None;

    /// <summary>
    /// When set, only these file types are visible and manageable, for example
    /// <c>"*.jpg|*.png"</c>. Folders are always visible. Empty allows all types.
    /// </summary>
    public string AllowedFileTypes { get; set; } = "";

    /// <summary>
    /// File types that are hidden and rejected, for example <c>"*.exe|*.dll"</c>.
    /// Takes precedence over <see cref="AllowedFileTypes"/>.
    /// </summary>
    public string DeniedFileTypes { get; set; } = "";

    /// <summary>
    /// Upper bound for the content below this folder, for example
    /// <c>"500 MB"</c> or <c>"2 GB"</c>. Empty means unlimited.
    /// </summary>
    public string Quota { get; set; } = "";

    /// <summary>Set to <c>false</c> to lift an inherited quota for this subtree.</summary>
    public bool LimitDiskUsage { get; set; } = true;

    /// <summary>Quota in bytes, or 0 when unlimited.</summary>
    [JsonIgnore]
    public long QuotaBytes => ParseSize(Quota);

    /// <summary>
    /// Parses a human-readable size such as <c>"500 MB"</c>, <c>"2 GB"</c> or
    /// <c>"1048576"</c> into bytes.
    /// </summary>
    /// <param name="text">Size text; empty or invalid input yields 0.</param>
    /// <returns>The size in bytes, or 0 for unlimited / invalid input.</returns>
    public static long ParseSize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var s = text.Trim().Replace(" ", "").ToUpperInvariant();

        long multiplier = 1;
        var suffixes = new (string Suffix, long Factor)[]
        {
            ("TB", 1L << 40), ("GB", 1L << 30), ("MB", 1L << 20), ("KB", 1L << 10), ("B", 1)
        };
        foreach (var (suffix, factor) in suffixes)
        {
            if (!s.EndsWith(suffix, StringComparison.Ordinal)) continue;
            s = s[..^suffix.Length];
            multiplier = factor;
            break;
        }

        return double.TryParse(s, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
            ? (long)(value * multiplier)
            : 0;
    }
}
