namespace FileManagerNet.Models;

/// <summary>
/// One browsable root folder of a file manager instance. An instance may expose
/// a single folder (the folder itself is the root) or several folders (they are
/// listed as virtual folders under the root node).
/// </summary>
public sealed class FileManagerRoot
{
    /// <summary>Creates an empty root; use the other constructor or set the properties.</summary>
    public FileManagerRoot() { }

    /// <summary>Creates a root folder with an optional display name.</summary>
    /// <param name="path">Physical folder on the server.</param>
    /// <param name="name">Name shown in the UI; empty uses the folder name.</param>
    public FileManagerRoot(string path, string? name = null)
    {
        Path = path;
        Name = name ?? "";
    }

    /// <summary>Physical folder on the server.</summary>
    public string Path { get; set; } = "";

    /// <summary>Name shown for this folder; empty uses the folder name.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Access rules for this root, matched by virtual path. An empty list means
    /// everything is allowed, subject to the instance-level
    /// <see cref="FileManagerConfig"/> switches.
    /// </summary>
    public List<FileManagerAccessControl> AccessControls { get; set; } = new();

    /// <summary>Adds an access rule and returns this root, so calls can be chained.</summary>
    /// <param name="path">Virtual folder the rule applies to.</param>
    /// <param name="allowed">Permissions granted on that folder and below it.</param>
    /// <param name="denied">Permissions removed again; wins over <paramref name="allowed"/>.</param>
    /// <param name="allowedFileTypes">Optional allowed type filter, for example "*.jpg|*.png".</param>
    /// <param name="deniedFileTypes">Optional denied type filter.</param>
    /// <param name="quota">Optional size limit for the subtree, for example "500 MB".</param>
    /// <returns>This root, for fluent chaining.</returns>
    public FileManagerRoot Allow(string path, FileManagerPermissions allowed,
                                 FileManagerPermissions denied = FileManagerPermissions.None,
                                 string allowedFileTypes = "", string deniedFileTypes = "",
                                 string quota = "")
    {
        AccessControls.Add(new FileManagerAccessControl
        {
            Path = path,
            Allowed = allowed,
            Denied = denied,
            AllowedFileTypes = allowedFileTypes,
            DeniedFileTypes = deniedFileTypes,
            Quota = quota,
        });
        return this;
    }

    /// <summary>Adds a read-only rule for <paramref name="path"/>.</summary>
    /// <param name="path">Virtual folder that becomes read-only.</param>
    /// <returns>This root, for fluent chaining.</returns>
    public FileManagerRoot ReadOnly(string path) =>
        Allow(path, FileManagerPermissions.ReadOnly);

    /// <summary>
    /// Parses a root-folder specification: folders separated by <c>;</c> (or
    /// newlines), each optionally prefixed with a display name and <c>=</c>,
    /// for example <c>"D:\Data;名称=E:\Backup"</c>.
    /// </summary>
    /// <param name="spec">Root-folder text from the tag helper or options.</param>
    /// <returns>The parsed roots; empty when <paramref name="spec"/> is blank.</returns>
    public static List<FileManagerRoot> ParseMany(string? spec)
    {
        var list = new List<FileManagerRoot>();
        if (string.IsNullOrWhiteSpace(spec)) return list;
        foreach (var raw in spec!.Split(new[] { ';', '\n', '\r' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = raw.IndexOf('=');
            if (eq > 0)
            {
                var name = raw[..eq].Trim();
                var path = raw[(eq + 1)..].Trim();
                if (path.Length == 0) continue;
                list.Add(new FileManagerRoot(path, name));
            }
            else
            {
                list.Add(new FileManagerRoot(raw));
            }
        }
        return list;
    }
}
