using System.Collections.Concurrent;
using System.IO.Enumeration;
using FileManagerNet.Models;

namespace FileManagerNet.Services;

/// <summary>
/// Resolves the effective permissions of a folder from its
/// <see cref="FileManagerAccessControl"/> entries.
///
/// Rules, in order:
/// <list type="number">
///   <item>An entry applies to its own folder and to every folder below it;
///   the <em>deepest</em> matching entry wins (subfolders inherit the nearest
///   ancestor, levels are not merged).</item>
///   <item><c>Denied</c> is removed from <c>Allowed</c>.</item>
///   <item>The instance-level <c>FileManagerConfig</c> switches cap the result:
///   an ACL can never grant what the host disabled globally.</item>
/// </list>
///
/// Cost: the ACL list is small (a handful of entries) and a folder's path has a
/// few segments, so a miss costs a few dozen string comparisons. Results are
/// memoised per folder, which makes the steady-state cost of a permission check
/// a dictionary lookup — deliberately, because every file operation performs
/// one.
/// </summary>
public sealed class AccessControlResolver
{
    private readonly IReadOnlyList<(string[] Segments, FileManagerAccessControl Entry)> _entries;
    private readonly FileManagerConfig _config;
    private readonly ConcurrentDictionary<string, EffectiveAccess> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a resolver for one root folder.</summary>
    /// <param name="controls">Access-control entries of the root; empty allows everything.</param>
    /// <param name="config">Instance-level switches that cap every result.</param>
    public AccessControlResolver(IEnumerable<FileManagerAccessControl>? controls, FileManagerConfig config)
    {
        _config = config;
        var list = new List<(string[], FileManagerAccessControl)>();
        foreach (var acl in controls ?? Enumerable.Empty<FileManagerAccessControl>())
        {
            var path = Normalize(acl.Path);
            list.Add((path.Split('/', StringSplitOptions.RemoveEmptyEntries), acl));
        }
        _entries = list;
    }

    /// <summary>True when no access-control entries were configured.</summary>
    public bool IsEmpty => _entries.Count == 0;

    /// <summary>Returns the effective access for a virtual folder.</summary>
    /// <param name="virtualPath">Virtual folder path; blank means the root.</param>
    /// <returns>The resolved permissions, type filters and quota.</returns>
    public EffectiveAccess Resolve(string? virtualPath)
    {
        var vp = Normalize(virtualPath);
        if (_cache.TryGetValue(vp, out var cached)) return cached;

        var segments = vp.Split('/', StringSplitOptions.RemoveEmptyEntries);

        FileManagerAccessControl? best = null;
        var bestDepth = -1;

        foreach (var (aclSegments, entry) in _entries)
        {
            var depth = MatchDepth(aclSegments, segments);
            if (depth < 0) continue;
            /* deeper wins; on a tie the entry added later wins, so a specific
             * rule can be appended to override a broader one */
            if (depth >= bestDepth)
            {
                bestDepth = depth;
                best = entry;
            }
        }

        var permissions = best?.Allowed ?? FileManagerPermissions.Full;
        if (best is not null) permissions &= ~best.Denied;
        permissions = ApplyConfigCaps(permissions);

        var access = new EffectiveAccess
        {
            Permissions = permissions,
            AllowedTypes = FileTypeFilter.Parse(best?.AllowedFileTypes),
            DeniedTypes = FileTypeFilter.Parse(best?.DeniedFileTypes),
            QuotaBytes = best is { LimitDiskUsage: true } ? best.QuotaBytes : 0,
            /* a wildcard rule cannot be turned back into a folder, so fall back
             * to measuring the folder being written to */
            QuotaPath = best is null ? vp
                : best.Path.Contains('*') ? vp
                : Normalize(best.Path),
        };

        /* folder paths are bounded by the tree the users actually open, but cap
         * it anyway so a crawler cannot grow the map without limit */
        if (_cache.Count > 8192) _cache.Clear();
        _cache[vp] = access;
        return access;
    }

    /// <summary>Throws when a folder does not grant <paramref name="required"/>.</summary>
    /// <param name="virtualPath">Virtual folder path.</param>
    /// <param name="required">Permission or combination the operation needs.</param>
    public void Ensure(string? virtualPath, FileManagerPermissions required)
    {
        var access = Resolve(virtualPath);
        if (!access.Has(required))
            throw new UnauthorizedAccessException(Describe(required));
    }

    /// <summary>Builds the client-facing reason for a denied permission.</summary>
    private static string Describe(FileManagerPermissions required) => required switch
    {
        FileManagerPermissions.ListFiles or FileManagerPermissions.ListSubfolders =>
            "You do not have permission to view this folder.",
        FileManagerPermissions.Download => "Downloading is not allowed in this folder.",
        FileManagerPermissions.Upload => "Uploading is not allowed in this folder.",
        FileManagerPermissions.Create => "Creating items is not allowed in this folder.",
        FileManagerPermissions.Delete => "Deleting is not allowed in this folder.",
        FileManagerPermissions.Rename => "Renaming is not allowed in this folder.",
        FileManagerPermissions.Edit => "Replacing existing files is not allowed in this folder.",
        FileManagerPermissions.Compress => "Compressing is not allowed in this folder.",
        FileManagerPermissions.Extract => "Extracting is not allowed in this folder.",
        FileManagerPermissions.Paste => "Writing into this folder is not allowed.",
        FileManagerPermissions.Preview => "Previewing is not allowed in this folder.",
        _ => "This operation is not allowed in this folder.",
    };

    /// <summary>How deep an ACL path matches into the target path; -1 = no
    /// match, 0 = matches the root only.</summary>
    private static int MatchDepth(string[] acl, string[] target)
    {
        if (acl.Length == 0) return 0;
        return MatchSegments(acl, 0, target, 0);
    }

    /// <summary>Matches an ACL segment list against a target path.</summary>
    private static int MatchSegments(string[] acl, int ai, string[] target, int ti)
    {
        if (ai == acl.Length) return ti;                 // fully matched, covers ti segments

        if (acl[ai] == "**")
        {
            var deepest = -1;
            for (var skip = 0; ti + skip <= target.Length; skip++)
            {
                var r = MatchSegments(acl, ai + 1, target, ti + skip);
                if (r > deepest) deepest = r;
            }
            return deepest;
        }

        if (ti >= target.Length) return -1;
        if (!FileSystemName.MatchesSimpleExpression(acl[ai], target[ti], ignoreCase: true)) return -1;

        /* `*` matches inside one segment only */
        return MatchSegments(acl, ai + 1, target, ti + 1);
    }

    /// <summary>Removes permissions that the instance-level switches disable.</summary>
    private FileManagerPermissions ApplyConfigCaps(FileManagerPermissions p)
    {
        const FileManagerPermissions download = FileManagerPermissions.Download | FileManagerPermissions.Preview;
        if (!_config.AllowDownload) p &= ~download;
        if (!_config.AllowUpload) p &= ~(FileManagerPermissions.Upload | FileManagerPermissions.Edit);
        if (!_config.AllowDelete) p &= ~FileManagerPermissions.Delete;
        if (!_config.AllowRename) p &= ~FileManagerPermissions.Rename;
        if (!_config.AllowCreateFolder && !_config.AllowCreateFile) p &= ~FileManagerPermissions.Create;
        if (!_config.AllowCopyMove) p &= ~(FileManagerPermissions.Copy | FileManagerPermissions.Cut | FileManagerPermissions.Paste);
        if (!_config.AllowArchive) p &= ~(FileManagerPermissions.Compress | FileManagerPermissions.Extract);
        return p;
    }

    /// <summary>Normalizes a virtual path to "/"-rooted, forward-slash form.</summary>
    internal static string Normalize(string? virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath)) return "/";
        var p = virtualPath.Replace('\\', '/').Trim();
        if (!p.StartsWith('/')) p = "/" + p;
        while (p.Length > 1 && p.EndsWith('/')) p = p[..^1];
        return p.Length == 0 ? "/" : p;
    }
}
