using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using FileManagerNet.Models;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;

namespace FileManagerNet.Services;

/// <summary>
/// Default <see cref="IFileSystemService"/> implementation over one or more
/// physical root folders. Clients only see virtual paths ("/", "/sub/file.txt").
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    /// <summary>Windows paths are case-insensitive and treat '\' as a separator;
    /// Linux/macOS paths are case-sensitive and may contain ':' and '..' in names.</summary>
    private static readonly bool IsWindows = OperatingSystem.IsWindows();

    private static readonly StringComparison PathComparison =
        IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _root;                 // first mount; in single-root mode: the root itself
    private readonly IReadOnlyList<FileSystemMount> _mounts; // one or more browsable folders
    private readonly bool _multi;                  // >1 mount → "/" is a virtual folder
    private readonly ILogger<FileSystemService>? _logger;
    private readonly DirectoryListingCache _listCache;

    /// <summary>Creates a service over a single root folder.</summary>
    /// <param name="root">Physical root folder.</param>
    /// <param name="logger">Optional logger for file-system failures.</param>
    /// <param name="listCacheMs">Listing reuse window in milliseconds; 0 reuses nothing.</param>
    /// <param name="config">Feature switches that cap the effective permissions.</param>
    public FileSystemService(string root, ILogger<FileSystemService>? logger = null, int listCacheMs = 0,
                             FileManagerConfig? config = null)
        : this(new[] { new FileManagerRoot(root) }, logger, listCacheMs, config) { }

    /// <summary>Creates a service over one or more root folders. With a single
    /// folder the folder itself is the root; with several, the root lists them
    /// as virtual folders and every virtual path starts with the mount name.</summary>
    /// <param name="roots">Browsable root folders with their access rules.</param>
    /// <param name="logger">Optional logger for file-system failures.</param>
    /// <param name="listCacheMs">How long a directory listing may be reused;
    /// concurrent requests for the same folder are always collapsed into one
    /// enumeration. 0 disables reuse.</param>
    /// <param name="config">Feature switches that cap the effective permissions.</param>
    public FileSystemService(IReadOnlyList<FileManagerRoot> roots, ILogger<FileSystemService>? logger = null,
                             int listCacheMs = 0, FileManagerConfig? config = null)
    {
        _listCache = new DirectoryListingCache(listCacheMs);
        if (roots is null || roots.Count == 0)
            throw new ArgumentException("At least one root folder must be provided.", nameof(roots));

        var mounts = new List<FileSystemMount>();
        foreach (var r in roots)
        {
            if (string.IsNullOrWhiteSpace(r.Path))
                throw new ArgumentException("Root folder paths must not be empty.", nameof(roots));

            var physical = Path.GetFullPath(r.Path);
            // the same folder registered twice would show up twice
            if (mounts.Any(m => string.Equals(m.PhysicalPath, physical, PathComparison))) continue;

            var name = MakeUniqueName(
                string.IsNullOrWhiteSpace(r.Name) ? NameFromPath(physical) : r.Name, mounts);
            Directory.CreateDirectory(physical);
            mounts.Add(new FileSystemMount(name, physical,
                new AccessControlResolver(r.AccessControls, config ?? new FileManagerConfig())));
        }

        _mounts = mounts;
        _multi = mounts.Count > 1;
        _root = mounts[0].PhysicalPath;
        _logger = logger;
    }

    /// <summary>Physical path of the first (or only) root folder.</summary>
    public string RootPath => _root;
    /// <summary>True when several root folders are mounted, so "/" is a virtual
    /// folder whose entries are the mounts.</summary>
    public bool HasMultipleRoots => _multi;

    // ---------------------------------------------------------------- mounts

    /// <summary>Derives a mount name from the folder name (or its volume root).</summary>
    private static string NameFromPath(string physical)
    {
        var trimmed = physical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(name))
            name = Path.GetPathRoot(physical)?.TrimEnd('\\', '/') ?? "Root";
        return name;
    }

    /// <summary>A mount name is the first segment of every virtual path, so it must
    /// not contain separators. Clashes get a " (2)" suffix.</summary>
    private static string MakeUniqueName(string raw, List<FileSystemMount> existing)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw.Trim())
            sb.Append(ch is '/' or '\0' || char.IsControl(ch) || (IsWindows && ch is '\\' or ':')
                ? '_' : ch);
        var name = sb.ToString().Trim();
        if (name is "." or "..") name = "_" + name;
        if (name.Length == 0) name = "Root";

        var candidate = name;
        for (var i = 2; existing.Any(m => string.Equals(m.Name, candidate, PathComparison)); i++)
            candidate = $"{name} ({i})";
        return candidate;
    }

    // ---------------------------------------------------------------- path safety

    /// <summary>Validates a virtual path: forward slashes, rooted, no traversal.</summary>
    private static string NormalizeVirtual(string? virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath)) return "/";

        /* On Windows '\' is also a separator; on Linux/macOS it is a legal file-name
         * character, so only Windows input is converted. Trailing/leading spaces are
         * legal in Unix names too, so trimming is Windows-only. */
        var p = IsWindows ? virtualPath.Replace('\\', '/').Trim() : virtualPath;
        if (p.Length > 1 && p.EndsWith('/')) p = p.TrimEnd('/');

        /* '.' and '..' are only dangerous as complete segments — "report..final" is a
         * perfectly valid name on every platform. ':' is a drive/stream separator on
         * Windows only. */
        var segments = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (p.Contains('\0') || p.Contains("//") || segments.Any(s => s is "." or "..") ||
            (IsWindows && p.Contains(':')))
            throw new UnauthorizedAccessException("Invalid path.");
        return p.StartsWith('/') ? p : "/" + p;
    }

    /// <summary>Appends a directory separator when the path has none.</summary>
    private static string WithSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    /// <summary>True for the virtual root of a multi-root instance — the only
    /// folder that has no physical counterpart.</summary>
    public bool IsVirtualRoot(string? virtualPath) => _multi && NormalizeVirtual(virtualPath) == "/";

    /// <summary>True when the virtual path is a bare mount name ("/folder") of a
    /// multi-root instance, i.e. an entry that cannot receive new items.</summary>
    public bool IsMountRootLevel(string? virtualPath) => IsMountLevel(virtualPath);

    /// <summary>Maps a physical path back to its virtual path (used to report the
    /// result of an upload).</summary>
    public string ToVirtualPath(string physicalPath) =>
        VirtualOfPhysical(Path.GetFullPath(physicalPath));

    /// <summary>Converts a virtual path into a full physical path, rejecting traversal.</summary>
    public string Resolve(string virtualPath)
    {
        var normalized = NormalizeVirtual(virtualPath);

        if (!_multi)
        {
            var combined = Path.GetFullPath(Path.Combine(_root, normalized.TrimStart('/')));
            if (combined != _root && !combined.StartsWith(WithSeparator(_root), StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Path escapes the root folder.");
            return combined;
        }

        if (normalized == "/")
            throw new UnauthorizedAccessException("The root only contains folders; open one of them first.");

        var slash = normalized.IndexOf('/', 1);
        var name = slash < 0 ? normalized[1..] : normalized[1..slash];
        var rest = slash < 0 ? "" : normalized[(slash + 1)..];
        var mount = _mounts.FirstOrDefault(m => string.Equals(m.Name, name, PathComparison))
                    ?? throw new DirectoryNotFoundException("Folder not found: " + name);

        var physical = Path.GetFullPath(Path.Combine(mount.PhysicalPath,
            rest.Replace('/', Path.DirectorySeparatorChar)));
        if (physical != mount.PhysicalPath &&
            !physical.StartsWith(WithSeparator(mount.PhysicalPath), StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Path escapes the root folder.");
        return physical;
    }

    /// <summary>True when the path is one of the mount roots (which must not be
    /// deleted, renamed, copied or moved).</summary>
    private bool IsMountRoot(string physical) =>
        _mounts.Any(m => string.Equals(m.PhysicalPath, physical, PathComparison));

    private FileSystemMount MountOf(string physical) =>
        _mounts.First(m => physical == m.PhysicalPath
                           || physical.StartsWith(WithSeparator(m.PhysicalPath), StringComparison.Ordinal));

    private string VirtualOfPhysical(string physical)
    {
        var mount = MountOf(physical);
        var prefix = _multi ? "/" + mount.Name : "";
        var rel = physical.Length == mount.PhysicalPath.Length
            ? ""
            : physical[mount.PhysicalPath.Length..].TrimStart(Path.DirectorySeparatorChar);
        if (rel.Length == 0) return prefix.Length == 0 ? "/" : prefix;
        return prefix + "/" + (IsWindows ? rel.Replace('\\', '/') : rel);
    }

    /// <summary>Combines a virtual folder with a validated child name.</summary>
    private static string Combine(string folderVirtual, string name)
    {
        /* Windows trims trailing dots/spaces and forbids ':' and '\' in names;
         * Linux/macOS allow them, so only the separator and NUL are invalid there. */
        name = IsWindows ? name.Trim() : name;
        if (name is "" or "." or ".." || name.Contains('/') || name.Contains('\0') ||
            (IsWindows && (name.Contains('\\') || name.Contains(':'))))
            throw new UnauthorizedAccessException("Invalid name.");
        return folderVirtual == "/" ? "/" + name : folderVirtual + "/" + name;
    }

    // ---------------------------------------------------------------- queries

    /// <summary>Drops cached listings after a mutation, so the next request
    /// reflects the change instead of waiting out the TTL.</summary>
    private void InvalidateListings() => _listCache.Invalidate();

    // ------------------------------------------------------- access control

    /// <summary>Finds the root folder a virtual path belongs to and the path
    /// relative to it — access rules are written per root, without the mount
    /// prefix.</summary>
    private (AccessControlResolver Resolver, string LocalPath) AccessScope(string vp)
    {
        if (!_multi) return (_mounts[0].Access, vp);

        var segments = vp.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return (_mounts[0].Access, "/");   // the virtual root

        foreach (var m in _mounts)
            if (string.Equals(m.Name, segments[0], PathComparison))
                return (m.Access, "/" + string.Join('/', segments.Skip(1)));

        return (_mounts[0].Access, vp);
    }

    /// <summary>Effective permissions and type filters for a folder.</summary>
    public EffectiveAccess AccessAt(string? virtualPath)
    {
        var vp = NormalizeVirtual(virtualPath);
        var (resolver, local) = AccessScope(vp);
        return resolver.Resolve(local);
    }

    /// <summary>Throws when the folder does not grant the permission. Called by
    /// every mutating operation, so a hand-crafted API call cannot bypass what
    /// the UI would hide.</summary>
    private void EnsureAccess(string? virtualPath, FileManagerPermissions required)
    {
        var vp = NormalizeVirtual(virtualPath);
        var (resolver, local) = AccessScope(vp);
        resolver.Ensure(local, required);
    }

    /// <summary>
    /// Drops the cached size of the quota scope after a write that did not go
    /// through <see cref="SaveUploaded"/> (chunked upload), so the next check
    /// re-measures instead of trusting a stale figure.
    /// </summary>
    /// <param name="virtualPath">Virtual path that was written to.</param>
    public void InvalidateUsage(string virtualPath)
    {
        var access = AccessAt(virtualPath);
        if (access.QuotaBytes <= 0) return;
        DirectoryUsage.Forget(Resolve(access.QuotaPath));
    }

    /// <summary>
    /// Checks permissions, file type and quota for a write of
    /// <paramref name="size"/> bytes to <paramref name="targetPath"/>.
    /// Used by both upload paths so a failed check happens before any byte is
    /// transferred.
    /// </summary>
    /// <param name="targetPath">Virtual path of the target file.</param>
    /// <param name="overwrite">True when an existing file is replaced.</param>
    /// <param name="size">Number of bytes that will be written.</param>
    public void EnsureCanWrite(string targetPath, bool overwrite, long size)
    {
        EnsureAccess(targetPath, FileManagerPermissions.Upload);
        if (overwrite) EnsureAccess(targetPath, FileManagerPermissions.Edit);
        EnsureTypeAllowed(targetPath);
        EnsureQuota(targetPath, size);
    }

    /// <summary>Rejects a name whose type the folder's rule excludes.</summary>
    private void EnsureTypeAllowed(string filePath)
    {
        var vp = NormalizeVirtual(filePath);
        var name = Path.GetFileName(Resolve(vp));
        if (!AccessAt(vp).IsFileAllowed(name))
            throw new UnauthorizedAccessException($"'{name}' is not an allowed file type in this folder.");
    }

    /// <summary>Keeps the quota accounting in step after a write or delete.</summary>
    private void AddUsage(string virtualPath, long delta)
    {
        if (delta == 0) return;
        var access = AccessAt(virtualPath);
        if (access.QuotaBytes <= 0) return;
        DirectoryUsage.Add(Resolve(access.QuotaPath), delta);
    }

    /// <summary>Refuses a write that would push the folder past its quota.</summary>
    private void EnsureQuota(string virtualPath, long incomingBytes)
    {
        var access = AccessAt(virtualPath);
        if (access.QuotaBytes <= 0) return;

        var used = DirectoryUsage.GetUsage(Resolve(access.QuotaPath));
        if (used + incomingBytes > access.QuotaBytes)
            throw new UnauthorizedAccessException(
                $"This folder is limited to {FormatSize(access.QuotaBytes)} and has {FormatSize(used)} in use.");
    }

    /// <summary>Builds a human-readable size such as "1.5 MB" for error messages.</summary>
    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.#} {units[unit]}";
    }

    /// <summary>
    /// Lists a folder, folders first and then sorted by name.
    /// </summary>
    /// <param name="path">Virtual folder path.</param>
    /// <param name="searchPattern">Optional glob filter for file names.</param>
    /// <param name="bypassCache">Forces a fresh enumeration (explicit refresh);
    /// concurrent requests are still collapsed into one disk walk.</param>
    /// <returns>The folder entries.</returns>
    public ListResult List(string path, string searchPattern = "*", bool bypassCache = false)
    {
        var vp = NormalizeVirtual(path);

        /* filtered listings are rare and must not poison the shared cache */
        if (searchPattern != "*") return ListCore(vp, searchPattern);

        /* Many browsers opening the same folder is the normal multi-user case.
         * Collapsing them into one enumeration turns N disk walks into 1 — see
         * DirectoryListingCache for the measurements. */
        return _listCache.Get("list|" + vp, () => ListCore(vp, "*"), bypassCache);
    }

    /// <summary>Enumerates one folder and applies the folder's permission filters.</summary>
    private ListResult ListCore(string vp, string searchPattern)
    {
        var result = new ListResult();

        if (IsVirtualRoot(vp))
        {
            foreach (var m in _mounts)
            {
                /* a mount is hidden from users who may not see its folders */
                if (!m.Access.Resolve("/").Has(FileManagerPermissions.ListSubfolders)) continue;
                result.Entries.Add(new FileSystemEntryDto
                {
                    Name = m.Name,
                    Path = "/" + m.Name,
                    IsDirectory = true,
                    LastModified = new DateTimeOffset(Directory.GetLastWriteTimeUtc(m.PhysicalPath))
                        .ToUnixTimeMilliseconds(),
                    HasChildren = Directory.EnumerateDirectories(m.PhysicalPath).Any(),
                });
            }
            return result;
        }

        var access = AccessAt(vp);
        if (!access.Has(FileManagerPermissions.ListSubfolders) && !access.Has(FileManagerPermissions.ListFiles))
            throw new UnauthorizedAccessException("You do not have permission to view this folder.");

        var dir = Resolve(vp);
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException("Folder not found.");
        var virtualPath = ToVirtual(vp);

        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            var info = new DirectoryInfo(d);
            result.Entries.Add(new FileSystemEntryDto
            {
                Name = info.Name,
                Path = Combine(virtualPath, info.Name),
                IsDirectory = true,
                LastModified = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
                HasChildren = true, // cheap default; tree lazy-loads
            });
        }

        foreach (var f in Directory.EnumerateFiles(dir))
        {
            var info = new FileInfo(f);
            if (searchPattern != "*")
            {
                try { if (!MatchesPattern(info.Name, searchPattern)) continue; }
                catch { /* fall through and include */ }
            }
            result.Entries.Add(new FileSystemEntryDto
            {
                Name = info.Name,
                Path = Combine(virtualPath, info.Name),
                IsDirectory = false,
                Size = info.Length,
                LastModified = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
                Extension = info.Extension.TrimStart('.').ToLowerInvariant(),
                ReadOnly = info.IsReadOnly,
            });
        }

        result.Entries = result.Entries
            .Where(e => e.IsDirectory
                ? access.Has(FileManagerPermissions.ListSubfolders)
                : access.Has(FileManagerPermissions.ListFiles) && access.IsFileAllowed(e.Name))
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return result;
    }

    /// <summary>Returns the child folders of <paramref name="path"/> for the tree pane.</summary>
    /// <param name="path">Virtual folder path.</param>
    /// <returns>The child folder nodes, sorted by name.</returns>
    public List<TreeNodeDto> GetChildren(string path)
    {
        EnsureAccess(path, FileManagerPermissions.ListSubfolders);
        var vp = NormalizeVirtual(path);
        if (IsVirtualRoot(vp))
        {
            return _mounts
                .Select(m => new TreeNodeDto { Name = m.Name, Path = "/" + m.Name, HasChildren = true })
                .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var dir = Resolve(vp);
        var nodes = new List<TreeNodeDto>();
        if (!Directory.Exists(dir)) return nodes;
        var virtualPath = ToVirtual(vp);
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            var name = Path.GetFileName(d);
            nodes.Add(new TreeNodeDto
            {
                Name = name,
                Path = Combine(virtualPath, name),
                HasChildren = true, // lazily confirmed by the client on expand
            });
        }
        return nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Returns a single entry, or <c>null</c> when it does not exist.</summary>
    /// <param name="path">Virtual path of the entry.</param>
    public FileSystemEntryDto? GetEntry(string path)
    {
        EnsureAccess(path, FileManagerPermissions.ListFiles);
        var vp = NormalizeVirtual(path);
        if (IsVirtualRoot(vp))
            return new FileSystemEntryDto { Name = "", Path = "/", IsDirectory = true };

        var physical = Resolve(vp);
        if (Directory.Exists(physical))
        {
            var di = new DirectoryInfo(physical);
            return new FileSystemEntryDto
            {
                Name = di.Name, Path = ToVirtual(vp), IsDirectory = true,
                LastModified = new DateTimeOffset(di.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
            };
        }
        if (File.Exists(physical))
        {
            var fi = new FileInfo(physical);
            return new FileSystemEntryDto
            {
                Name = fi.Name, Path = ToVirtual(vp), IsDirectory = false, Size = fi.Length,
                Extension = fi.Extension.TrimStart('.').ToLowerInvariant(),
                LastModified = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
            };
        }
        return null;
    }

    /// <summary>
    /// Size and item counts for a path, plus the free space of its volume.
    ///
    /// <paramref name="deep"/> walks the whole subtree. It is off by default on
    /// purpose: the status bar derives the file/folder counts from the listing it
    /// already has and only needs the volume figures, so a deep walk here would be
    /// an O(subtree) disk scan whose result nobody reads — on a 20 000-file folder
    /// that is ~30 ms per request, paid on every folder change, per user. Callers
    /// that genuinely need recursive totals can pass <c>deep: true</c>.
    /// </summary>
    public PathStatsDto GetStats(string path, bool deep = false)
    {
        var vp = NormalizeVirtual(path);
        if (!IsVirtualRoot(vp))
        {
            var statsAccess = AccessAt(vp);
            if (!statsAccess.Has(FileManagerPermissions.ListFiles) &&
                !statsAccess.Has(FileManagerPermissions.ListSubfolders))
                throw new UnauthorizedAccessException("You do not have permission to view this folder.");
        }
        if (IsVirtualRoot(vp))
        {
            /* aggregating every file below every mount could take minutes on a
             * large volume; the status bar only needs the mount count and the
             * free space of the volume the first mount lives on */
            var rootStats = new PathStatsDto { Path = "/", FolderCount = _mounts.Count };
            FillDriveStats(rootStats, _root);
            return rootStats;
        }

        var physical = Resolve(vp);
        var stats = new PathStatsDto { Path = ToVirtual(vp) };
        if (File.Exists(physical))
        {
            stats.FileCount = 1;
            stats.TotalSize = new FileInfo(physical).Length;
        }
        else if (Directory.Exists(physical))
        {
            var dir = new DirectoryInfo(physical);
            var scope = deep ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var f in dir.EnumerateFiles("*", scope))
            {
                stats.FileCount++;
                stats.TotalSize += f.Length;
            }
            foreach (var _ in dir.EnumerateDirectories("*", scope))
                stats.FolderCount++;
        }
        FillDriveStats(stats, physical);
        return stats;
    }

    /// <summary>Fills free/total volume space; unknown volumes stay <c>null</c>.</summary>
    private static void FillDriveStats(PathStatsDto stats, string physical)
    {
        try
        {
            var driveRoot = Path.GetPathRoot(physical);
            if (driveRoot is not null)
            {
                var drive = new DriveInfo(driveRoot);
                if (drive.IsReady)
                {
                    stats.DriveFreeBytes = drive.AvailableFreeSpace;
                    stats.DriveTotalBytes = drive.TotalSize;
                }
            }
        }
        catch { /* drives like UNC roots may throw */ }
    }

    /// <summary>Searches for names containing <see cref="SearchOptions.Pattern"/>.</summary>
    /// <param name="path">Virtual folder to start from.</param>
    /// <param name="options">Search parameters.</param>
    /// <returns>The matching entries, capped by <see cref="SearchOptions.MaxResults"/>.</returns>
    public List<FileSystemEntryDto> Search(string path, SearchOptions options)
    {
        EnsureAccess(path, FileManagerPermissions.ListFiles);
        var vp = NormalizeVirtual(path);
        var pattern = (options.Pattern ?? "").Trim().Trim('*');
        var results = new List<FileSystemEntryDto>();
        var max = Math.Clamp(options.MaxResults <= 0 ? 500 : options.MaxResults, 1, 5000);

        /* searching the virtual root means searching every mount */
        var starts = IsVirtualRoot(vp)
            ? _mounts.Select(m => m.PhysicalPath).ToList()
            : new List<string> { Resolve(vp) };

        foreach (var start in starts)
        {
            if (results.Count >= max) break;
            if (!Directory.Exists(start)) continue;
            foreach (var item in new DirectoryInfo(start).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                if (!string.IsNullOrEmpty(pattern) &&
                    !item.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    continue;

                var isDir = item is DirectoryInfo;
                results.Add(new FileSystemEntryDto
                {
                    Name = item.Name,
                    Path = VirtualOfPhysical(item.FullName),
                    IsDirectory = isDir,
                    Size = item is FileInfo fi ? fi.Length : 0,
                    LastModified = new DateTimeOffset(item.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
                    Extension = item.Extension.TrimStart('.').ToLowerInvariant(),
                    HasChildren = isDir,
                });
                if (results.Count >= max) break;
            }
        }
        return results;
    }

    // ---------------------------------------------------------------- mutations

    /// <summary>True for a top-level path ("/name") of a multi-root instance.</summary>
    private bool IsMountLevel(string? virtualPath)
    {
        if (!_multi) return false;
        var p = NormalizeVirtual(virtualPath);
        return p.IndexOf('/', 1) < 0;
    }

    /// <summary>Creates a folder.</summary>
    /// <param name="path">Virtual path of the new folder.</param>
    public void CreateFolder(string path)
    {
        InvalidateListings();
        EnsureAccess(path, FileManagerPermissions.Create);
        if (IsVirtualRoot(path) || IsMountLevel(path))
            throw new UnauthorizedAccessException("Choose one of the folders first.");
        var physical = Resolve(path);
        if (Directory.Exists(physical) || File.Exists(physical))
            throw new IOException("An item with the same name already exists.");
        Directory.CreateDirectory(physical);
    }

    /// <summary>Creates a file with optional initial content.</summary>
    /// <param name="path">Virtual path of the new file.</param>
    /// <param name="content">Initial bytes; empty when <c>null</c>.</param>
    public void CreateFile(string path, byte[]? content = null)
    {
        InvalidateListings();
        EnsureAccess(path, FileManagerPermissions.Create);
        EnsureTypeAllowed(path);
        if (IsVirtualRoot(path) || IsMountLevel(path))
            throw new UnauthorizedAccessException("Choose one of the folders first.");
        var physical = Resolve(path);
        if (File.Exists(physical) || Directory.Exists(physical))
            throw new IOException("An item with the same name already exists.");
        File.WriteAllBytes(physical, content ?? Array.Empty<byte>());
    }

    /// <summary>Renames a file or folder inside its current parent folder.</summary>
    /// <param name="path">Virtual path of the item.</param>
    /// <param name="newName">New name, without any directory part.</param>
    public void Rename(string path, string newName)
    {
        InvalidateListings();
        EnsureAccess(path, FileManagerPermissions.Rename);
        var physical = Resolve(path);
        if (IsMountRoot(physical))
            throw new UnauthorizedAccessException("The root folder cannot be renamed.");
        var parent = Path.GetDirectoryName(physical)
                     ?? throw new IOException("Cannot rename root.");
        var target = Path.Combine(parent, newName);
        if (File.Exists(target) || Directory.Exists(target))
            throw new IOException("An item with the same name already exists.");
        if (Directory.Exists(physical)) Directory.Move(physical, target);
        else if (File.Exists(physical)) File.Move(physical, target);
        else throw new FileNotFoundException("Item not found.");
    }

    /// <summary>Deletes a file or folder.</summary>
    /// <param name="path">Virtual path of the item.</param>
    /// <param name="recursive">Deletes folder content recursively.</param>
    public void Delete(string path, bool recursive = true)
    {
        InvalidateListings();
        EnsureAccess(path, FileManagerPermissions.Delete);
        var physical = Resolve(path);
        if (IsMountRoot(physical))
            throw new UnauthorizedAccessException("The root folder cannot be deleted.");

        long freed = 0;
        if (Directory.Exists(physical))
        {
            try
            {
                foreach (var f in new DirectoryInfo(physical).EnumerateFiles("*", SearchOption.AllDirectories))
                    freed += f.Length;
            }
            catch (IOException ex) { _logger?.LogDebug(ex, "Could not measure '{Path}' before deletion.", physical); }
            Directory.Delete(physical, recursive);
        }
        else if (File.Exists(physical))
        {
            freed = new FileInfo(physical).Length;
            File.Delete(physical);
        }
        else throw new FileNotFoundException("Item not found.");

        if (freed > 0) AddUsage(Path.GetDirectoryName(path) ?? "/", -freed);
    }

    /// <summary>Copies a file or folder into another folder.</summary>
    /// <param name="fromPath">Virtual path of the source item.</param>
    /// <param name="toFolder">Virtual path of the destination folder.</param>
    /// <param name="overwrite">Replaces existing items with the same name.</param>
    /// <param name="renameOnConflict">Keeps both items by generating "name (1).ext".</param>
    public void Copy(string fromPath, string toFolder, bool overwrite = false, bool renameOnConflict = false)
    {
        InvalidateListings();
        EnsureAccess(fromPath, FileManagerPermissions.Copy);
        EnsureAccess(toFolder, FileManagerPermissions.Paste);
        var from = Resolve(fromPath);
        if (IsVirtualRoot(toFolder))
            throw new UnauthorizedAccessException("Choose one of the folders first.");
        var toDir = Resolve(toFolder);
        var target = Path.Combine(toDir, Path.GetFileName(from));

        if (IsMountRoot(from))
            throw new UnauthorizedAccessException("The root folder cannot be copied.");
        if (from.StartsWith(WithSeparator(toDir)) && Directory.Exists(from) && Directory.Exists(toDir))
            throw new IOException("Cannot copy a folder into one of its own subfolders.");

        var isDir = Directory.Exists(from);
        if (!isDir && !File.Exists(from)) throw new FileNotFoundException("Source not found.");

        /* pasting into the folder the item already lives in: overwriting itself
         * is a no-op, keeping both needs a fresh name */
        if (string.Equals(from, target, StringComparison.Ordinal))
        {
            if (!renameOnConflict) return;
            target = UniquePath(target, isDir);
        }

        if (File.Exists(target) || Directory.Exists(target))
        {
            if (renameOnConflict) target = UniquePath(target, isDir);
            else if (!overwrite) throw new IOException($"'{Path.GetFileName(from)}' already exists in the target folder.");
        }

        if (isDir) CopyDirectory(from, target, overwrite);
        else File.Copy(from, target, overwrite);
    }

    /// <summary>Moves a file or folder into another folder.</summary>
    /// <param name="fromPath">Virtual path of the source item.</param>
    /// <param name="toFolder">Virtual path of the destination folder.</param>
    /// <param name="overwrite">Replaces existing items with the same name.</param>
    /// <param name="renameOnConflict">Keeps both items by generating "name (1).ext".</param>
    public void Move(string fromPath, string toFolder, bool overwrite = false, bool renameOnConflict = false)
    {
        InvalidateListings();
        EnsureAccess(fromPath, FileManagerPermissions.Cut);
        EnsureAccess(toFolder, FileManagerPermissions.Paste);
        var from = Resolve(fromPath);
        if (IsVirtualRoot(toFolder))
            throw new UnauthorizedAccessException("Choose one of the folders first.");
        var toDir = Resolve(toFolder);
        var target = Path.Combine(toDir, Path.GetFileName(from));

        if (IsMountRoot(from))
            throw new UnauthorizedAccessException("The root folder cannot be moved.");
        if (Directory.Exists(from) && (toDir.StartsWith(WithSeparator(from)) || toDir == from))
            throw new IOException("Cannot move a folder into itself or one of its subfolders.");

        var isDir = Directory.Exists(from);
        if (!isDir && !File.Exists(from)) throw new FileNotFoundException("Source not found.");

        /* dropping an item back into its own folder is a no-op */
        if (string.Equals(from, target, StringComparison.Ordinal) && !renameOnConflict) return;

        if (File.Exists(target) || Directory.Exists(target))
        {
            if (renameOnConflict) target = UniquePath(target, isDir);
            else if (!overwrite) throw new IOException($"'{Path.GetFileName(from)}' already exists in the target folder.");
            else if (Directory.Exists(target)) Directory.Delete(target, true);
            else File.Delete(target);
        }

        /* the mounts may live on different volumes, where a rename is impossible
         * (Directory.Move/File.Move throw); fall back to copy + delete */
        var sameVolume = string.Equals(Path.GetPathRoot(from), Path.GetPathRoot(target),
            StringComparison.OrdinalIgnoreCase);
        if (isDir)
        {
            if (sameVolume) Directory.Move(from, target);
            else { CopyDirectory(from, target, true); Directory.Delete(from, true); }
        }
        else
        {
            if (sameVolume) File.Move(from, target);
            else { File.Copy(from, target, true); File.Delete(from); }
        }
    }

    /// <summary>Names already present in <paramref name="toFolder"/> that a copy
    /// or move of <paramref name="fromPaths"/> would overwrite.</summary>
    public List<string> FindConflicts(IReadOnlyList<string> fromPaths, string toFolder)
    {
        var toDir = Resolve(toFolder);
        var conflicts = new List<string>();
        foreach (var p in fromPaths)
        {
            var from = Resolve(p);
            if (!Directory.Exists(from) && !File.Exists(from)) continue;
            var name = Path.GetFileName(from);
            var target = Path.Combine(toDir, name);
            if (File.Exists(target) || Directory.Exists(target)) conflicts.Add(name);
        }
        return conflicts;
    }

    /// <summary>Builds "name (1).ext" style paths until one is free.</summary>
    private static string UniquePath(string target, bool isDirectory)
    {
        var dir = Path.GetDirectoryName(target)!;
        var name = Path.GetFileName(target);
        var stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        var ext = isDirectory ? "" : Path.GetExtension(name);
        for (var i = 1; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        throw new IOException("Could not find a free name for '" + name + "'.");
    }

    // ---------------------------------------------------------------- archives

    /// <summary>
    /// Archive formats that can be created, keyed by the suffix the user types
    /// in the archive name. 7z creation is not supported (it requires 7-Zip).
    /// </summary>
    public static readonly string[] CreatableFormats = { ".zip", ".tar", ".tar.gz", ".tgz", ".tar.bz2", ".tbz2" };

    /// <summary>Formats that can be extracted (SharpCompress reader support).</summary>
    public static readonly string[] ExtractableFormats =
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".tar.gz", ".bz2", ".tbz2", ".tar.bz2",
          ".xz", ".txz", ".tar.xz", ".zst", ".tar.zst", ".lz", ".lzma", ".arj", ".ace", ".arc", ".lzw" };

    /// <summary>Creates an archive without progress reporting.</summary>
    /// <param name="archivePath">Virtual path of the archive to create.</param>
    /// <param name="paths">Virtual paths of the items to add.</param>
    /// <returns>Virtual path of the created archive.</returns>
    public string CreateArchive(string archivePath, IReadOnlyList<string> paths) =>
        CreateArchive(archivePath, paths, null, true);

    /// <summary>Creates an archive while reporting progress.</summary>
    /// <param name="archivePath">Virtual path of the archive to create.</param>
    /// <param name="paths">Virtual paths of the items to add.</param>
    /// <param name="progress">Receives (done, total, currentEntry).</param>
    /// <param name="enforcePermission">False when the archive is an internal
    /// step of another operation (for example a multi-item download); the
    /// caller has already checked the permissions of the items.</param>
    /// <returns>Virtual path of the created archive.</returns>
    public string CreateArchive(string archivePath, IReadOnlyList<string> paths,
                               Action<int, int, string>? progress, bool enforcePermission = true)
    {
        InvalidateListings();
        if (enforcePermission)
            EnsureAccess(archivePath, FileManagerPermissions.Compress);
        var physical = Resolve(archivePath);
        var name = Path.GetFileName(physical).ToLowerInvariant();

        var isZip = name.EndsWith(".zip");
        var isGz = name.EndsWith(".tar.gz") || name.EndsWith(".tgz");
        var isBz2 = name.EndsWith(".tar.bz2") || name.EndsWith(".tbz2");
        var isTar = name.EndsWith(".tar");
        if (name.EndsWith(".7z"))
            throw new NotSupportedException("Creating 7z archives is not supported; use .zip, .tar.gz or .tar.");
        if (!isZip && !isTar && !isGz && !isBz2)
            throw new NotSupportedException("Unsupported archive format; use .zip, .tar, .tar.gz or .tar.bz2.");

        // Flatten the selection up front: the caller needs a real total for the
        // progress bar, and a locked file must not abort the whole job.
        var files = new List<(string Physical, string Entry)>();
        foreach (var p in paths)
        {
            var src = Resolve(p);
            if (Directory.Exists(src)) CollectFiles(src, Path.GetFileName(src), files);
            else if (File.Exists(src)) files.Add((src, Path.GetFileName(src)));
        }

        if (File.Exists(physical)) File.Delete(physical);
        using var stream = new FileStream(physical, FileMode.CreateNew, FileAccess.Write);

        var total = files.Count;
        var done = 0;
        progress?.Invoke(0, total, "");

        if (isZip)
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            foreach (var f in files)
            {
                try { zip.CreateEntryFromFile(f.Physical, f.Entry, CompressionLevel.Optimal); }
                catch (IOException ex)
                {
                    /* a file locked by another process must not abort the whole job */
                    _logger?.LogDebug(ex, "Skipping '{Path}' while creating an archive.", f.Physical);
                }
                progress?.Invoke(++done, total, f.Entry);
            }
        }
        else
        {
            var compression = isGz ? CompressionType.GZip
                            : isBz2 ? CompressionType.BZip2
                            : CompressionType.None;
            WriteTar(stream, files, compression, entry => progress?.Invoke(++done, total, entry));
        }

        return ToVirtual(archivePath);
    }

    /// <summary>Recursively collects files with their archive entry names.</summary>
    private static void CollectFiles(string dir, string entryBase, List<(string Physical, string Entry)> files)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
            files.Add((f, entryBase + "/" + Path.GetFileName(f)));
        foreach (var d in Directory.EnumerateDirectories(dir))
            CollectFiles(d, entryBase + "/" + Path.GetFileName(d), files);
    }

    /// <summary>Writes the collected files into a tar stream with the requested compression.</summary>
    private void WriteTar(Stream target, List<(string Physical, string Entry)> files,
                          CompressionType compression, Action<string>? onEntry)
    {
        var archive = ArchiveFactory.CreateArchive<TarWriterOptions>();
        var seenDirs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            // materialise parent folder entries so extractors recreate the tree
            for (var i = f.Entry.IndexOf('/'); i > 0; i = f.Entry.IndexOf('/', i + 1))
            {
                var dirEntry = f.Entry[..(i + 1)];
                if (seenDirs.Add(dirEntry)) archive.AddDirectoryEntry(dirEntry);
            }
            archive.AddEntry(f.Entry, new FileInfo(f.Physical));
            onEntry?.Invoke(f.Entry);
        }
        archive.SaveTo(target, new TarWriterOptions(compression, true));
    }

    /// <summary>Extracts an archive without progress reporting.</summary>
    /// <param name="archivePath">Virtual path of the archive.</param>
    /// <param name="toFolder">Virtual destination folder.</param>
    /// <param name="overwrite">Replaces existing files.</param>
    public void ExtractArchive(string archivePath, string toFolder, bool overwrite = false) =>
        ExtractArchive(archivePath, toFolder, overwrite, null);

    /// <summary>Extracts an archive while reporting progress.</summary>
    /// <param name="archivePath">Virtual path of the archive.</param>
    /// <param name="toFolder">Virtual destination folder.</param>
    /// <param name="overwrite">Replaces existing files.</param>
    /// <param name="progress">Receives (done, total, currentEntry); total is -1
    /// when the format cannot be pre-scanned.</param>
    public void ExtractArchive(string archivePath, string toFolder, bool overwrite,
                               Action<int, int, string>? progress)
    {
        InvalidateListings();
        EnsureAccess(archivePath, FileManagerPermissions.Extract);
        EnsureAccess(toFolder, FileManagerPermissions.Extract | FileManagerPermissions.Paste);
        var archivePhysical = Resolve(archivePath);
        var toDir = Resolve(toFolder);
        if (!File.Exists(archivePhysical)) throw new FileNotFoundException("Archive not found.");
        Directory.CreateDirectory(toDir);
        var destRoot = Path.GetFullPath(toDir);

        // Zip / 7z / rar / plain tar can be opened randomly, which lets us know
        // the entry count up front (real percentage in the progress bar).
        // gzip / bzip2 / xz / zstd streams only support a forward reader.
        if (IsRandomAccessArchive(archivePhysical))
        {
            ExtractWithArchive(archivePhysical, destRoot, overwrite, progress);
            return;
        }
        try
        {
            ExtractWithReader(archivePhysical, destRoot, overwrite, progress);
        }
        catch (InvalidFormatException)
        {
            // the reader rejects the header before writing anything, so retrying
            // with the random-access API cannot leave duplicate files behind
            ExtractWithArchive(archivePhysical, destRoot, overwrite, progress);
        }
    }

    /// <summary>Detects formats whose entries can be counted before extraction.</summary>
    private static bool IsRandomAccessArchive(string path)
    {
        using var fs = File.OpenRead(path);
        var length = fs.Length;
        Span<byte> head = stackalloc byte[8];
        var n = fs.Read(head);

        if (n >= 4 && head[0] == 0x50 && head[1] == 0x4B) return true;                 // PK  (zip)
        if (n >= 6 && head[0] == 0x37 && head[1] == 0x7A && head[2] == 0xBC
                  && head[3] == 0xAF && head[4] == 0x27 && head[5] == 0x1C) return true; // 7z
        if (n >= 6 && head[0] == 0x52 && head[1] == 0x61 && head[2] == 0x72
                  && head[3] == 0x21 && head[4] == 0x1A && head[5] == 0x07) return true; // Rar!

        if (length > 262)
        {
            fs.Seek(257, SeekOrigin.Begin);
            Span<byte> ustar = stackalloc byte[5];
            if (fs.Read(ustar) == 5 && ustar[0] == (byte)'u' && ustar[1] == (byte)'s'
                && ustar[2] == (byte)'t' && ustar[3] == (byte)'a' && ustar[4] == (byte)'r')
                return true;                                                            // tar
        }
        return false;
    }

    /// <summary>Extracts a stream-only archive (gzip / bzip2 / xz / zstd) forward-only.</summary>
    private static void ExtractWithReader(string archivePath, string destRoot, bool overwrite,
                                          Action<int, int, string>? progress)
    {
        using var reader = SharpCompress.Readers.ReaderFactory.OpenReader(
            archivePath, new SharpCompress.Readers.ReaderOptions());
        var done = 0;
        // forward-only readers cannot know the entry count up front
        progress?.Invoke(0, -1, "");
        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            ExtractEntry(entry.Key, entry.IsDirectory, () => reader.OpenEntryStream(), destRoot, overwrite);
            if (!entry.IsDirectory) progress?.Invoke(++done, -1, entry.Key ?? "");
        }
    }

    /// <summary>Extracts a random-access archive, so progress can report a real total.</summary>
    private static void ExtractWithArchive(string archivePath, string destRoot, bool overwrite,
                                           Action<int, int, string>? progress)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath, new SharpCompress.Readers.ReaderOptions());
        var entries = archive.Entries.ToList();
        var total = entries.Count;
        var done = 0;
        progress?.Invoke(0, total, "");
        foreach (var entry in entries)
        {
            ExtractEntry(entry.Key, entry.IsDirectory, () => entry.OpenEntryStream(), destRoot, overwrite);
            if (!entry.IsDirectory) progress?.Invoke(++done, total, entry.Key ?? "");
        }
    }

    /// <summary>Writes one archive entry, rejecting paths that would escape the target.</summary>
    private static void ExtractEntry(string? rawKey, bool isDirectory, Func<Stream> openEntry,
                                     string destRoot, bool overwrite)
    {
        var key = (rawKey ?? "").Replace('\\', '/').TrimStart('/');
        if (key.Length == 0) return;
        // Guard against zip-slip: entries may contain "../" or absolute paths.
        if (key.Split('/').Any(s => s == "..") || Path.IsPathRooted(key))
            throw new UnauthorizedAccessException("Unsafe archive entry: " + rawKey);

        var targetPath = Path.GetFullPath(Path.Combine(destRoot, key));
        if (!targetPath.StartsWith(destRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(targetPath, destRoot, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Unsafe archive entry: " + rawKey);

        if (isDirectory)
        {
            Directory.CreateDirectory(targetPath);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (File.Exists(targetPath) && !overwrite)
            throw new IOException($"'{Path.GetFileName(targetPath)}' already exists.");

        using var input = openEntry();
        using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
        input.CopyTo(output);
    }

    // ---------------------------------------------------------------- download / upload

    /// <summary>Resolves a file for download, or <c>null</c> when it is a folder or missing.</summary>
    /// <param name="path">Virtual path of the file.</param>
    public (string PhysicalPath, FileSystemEntryDto Entry)? ResolveForDownload(string path)
    {
        EnsureAccess(path, FileManagerPermissions.Download);
        EnsureTypeAllowed(path);
        if (IsVirtualRoot(path)) return null;
        var physical = Resolve(path);
        if (File.Exists(physical))
            return (physical, GetEntry(path)!);
        return null;
    }

    /// <summary>Writes an uploaded stream to its target, checking permissions,
    /// file type and quota first.</summary>
    /// <param name="stream">Content to write.</param>
    /// <param name="targetPath">Virtual path of the target file.</param>
    /// <param name="overwrite">Replaces an existing file.</param>
    public void SaveUploaded(Stream stream, string targetPath, bool overwrite)
    {
        InvalidateListings();
        EnsureCanWrite(targetPath, overwrite, stream.CanSeek ? stream.Length : 0);
        if (IsVirtualRoot(targetPath) || IsMountLevel(targetPath))
            throw new UnauthorizedAccessException("Choose one of the folders first.");
        var physical = Resolve(targetPath);
        if (Directory.Exists(physical))
            throw new IOException("A folder with the same name exists.");
        if (File.Exists(physical) && !overwrite)
            throw new IOException($"'{Path.GetFileName(targetPath)}' already exists.");
        var before = File.Exists(physical) ? new FileInfo(physical).Length : 0;
        long written;
        using (var fs = new FileStream(physical, overwrite ? FileMode.Create : FileMode.CreateNew))
        {
            stream.CopyTo(fs);
            written = fs.Length;
        }
        AddUsage(targetPath, written - before);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Turns a requested virtual path into the normalized virtual path.</summary>
    private string ToVirtual(string requested)
    {
        var normalized = NormalizeVirtual(requested);
        if (_multi && normalized == "/") return "/";
        return VirtualOfPhysical(Resolve(normalized));
    }

    /// <summary>Copies a folder recursively, replacing the target when asked to.</summary>
    private static void CopyDirectory(string source, string target, bool overwrite)
    {
        if (Directory.Exists(target))
        {
            if (!overwrite)
                throw new IOException($"'{Path.GetFileName(source)}' already exists in the target folder.");
            Directory.Delete(target, true);
        }
        Directory.CreateDirectory(target);
        foreach (var f in Directory.EnumerateFiles(source))
            File.Copy(f, Path.Combine(target, Path.GetFileName(f)), true);
        foreach (var d in Directory.EnumerateDirectories(source))
            CopyDirectory(d, Path.Combine(target, Path.GetFileName(d)), true);
    }

    /// <summary>Minimal glob matcher supporting * and ? (case-insensitive).</summary>
    private static bool MatchesPattern(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex
            .Escape(pattern.Trim())
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
