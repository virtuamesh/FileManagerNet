using FileManagerNet.Models;

namespace FileManagerNet.Services;

/// <summary>
/// File operations exposed to the HTTP layer. All operations work on virtual,
/// root-relative paths ("/", "/sub/file.txt"); clients never see a physical
/// server path.
/// </summary>
public interface IFileSystemService
{
    /// <summary>Physical path of the first (or only) root folder.</summary>
    string RootPath { get; }

    /// <summary>Lists a folder, folders first and then sorted by name.</summary>
    /// <param name="path">Virtual folder path.</param>
    /// <param name="searchPattern">Optional glob filter for file names.</param>
    /// <param name="bypassCache">Forces a fresh enumeration (explicit refresh);
    /// concurrent requests are still collapsed into one disk walk.</param>
    /// <returns>The folder entries.</returns>
    ListResult List(string path, string searchPattern = "*", bool bypassCache = false);

    /// <summary>Returns the child folders of <paramref name="path"/> for the tree pane.</summary>
    /// <param name="path">Virtual folder path.</param>
    /// <returns>The child folder nodes.</returns>
    List<TreeNodeDto> GetChildren(string path);

    /// <summary>Returns item counts and volume space for a path.</summary>
    /// <param name="path">Virtual path.</param>
    /// <param name="deep">Walks the whole subtree; off by default because it is
    /// an O(subtree) disk scan.</param>
    /// <returns>The statistics.</returns>
    PathStatsDto GetStats(string path, bool deep = false);

    /// <summary>Returns a single entry, or <c>null</c> when it does not exist.</summary>
    /// <param name="path">Virtual path.</param>
    FileSystemEntryDto? GetEntry(string path);

    /// <summary>Creates a folder.</summary>
    /// <param name="path">Virtual path of the new folder.</param>
    void CreateFolder(string path);

    /// <summary>Creates a file with optional initial content.</summary>
    /// <param name="path">Virtual path of the new file.</param>
    /// <param name="content">Initial bytes; empty when <c>null</c>.</param>
    void CreateFile(string path, byte[]? content = null);

    /// <summary>Renames a file or folder inside its current parent folder.</summary>
    /// <param name="path">Virtual path of the item.</param>
    /// <param name="newName">New name, without any directory part.</param>
    void Rename(string path, string newName);

    /// <summary>Deletes a file or folder.</summary>
    /// <param name="path">Virtual path of the item.</param>
    /// <param name="recursive">Deletes folder content recursively.</param>
    void Delete(string path, bool recursive = true);

    /// <summary>Copies a file or folder into another folder.</summary>
    /// <param name="fromPath">Virtual path of the source item.</param>
    /// <param name="toFolder">Virtual path of the destination folder.</param>
    /// <param name="overwrite">Replaces existing items with the same name.</param>
    /// <param name="renameOnConflict">Keeps both items by generating "name (1).ext".</param>
    void Copy(string fromPath, string toFolder, bool overwrite = false, bool renameOnConflict = false);

    /// <summary>Moves a file or folder into another folder.</summary>
    /// <param name="fromPath">Virtual path of the source item.</param>
    /// <param name="toFolder">Virtual path of the destination folder.</param>
    /// <param name="overwrite">Replaces existing items with the same name.</param>
    /// <param name="renameOnConflict">Keeps both items by generating "name (1).ext".</param>
    void Move(string fromPath, string toFolder, bool overwrite = false, bool renameOnConflict = false);

    /// <summary>Returns the item names that already exist in the destination folder.</summary>
    /// <param name="fromPaths">Virtual paths of the items to copy or move.</param>
    /// <param name="toFolder">Virtual path of the destination folder.</param>
    List<string> FindConflicts(IReadOnlyList<string> fromPaths, string toFolder);

    /// <summary>Searches for names containing <see cref="SearchOptions.Pattern"/>.</summary>
    /// <param name="path">Virtual folder to start from.</param>
    /// <param name="options">Search parameters.</param>
    List<FileSystemEntryDto> Search(string path, SearchOptions options);

    /// <summary>
    /// Creates an archive; the format follows the target extension (.zip,
    /// .tar, .tar.gz/.tgz, .tar.bz2/.tbz2).
    /// </summary>
    /// <param name="archivePath">Virtual path of the archive to create.</param>
    /// <param name="paths">Virtual paths of the items to add.</param>
    /// <returns>Virtual path of the created archive.</returns>
    string CreateArchive(string archivePath, IReadOnlyList<string> paths);

    /// <summary>Creates an archive while reporting progress.</summary>
    /// <param name="archivePath">Virtual path of the archive to create.</param>
    /// <param name="paths">Virtual paths of the items to add.</param>
    /// <param name="progress">Receives (done, total, currentEntry).</param>
    /// <param name="enforcePermission">False when the archive is an internal
    /// step of another operation (for example a multi-item download); the
    /// caller has already checked the permissions of the items.</param>
    /// <returns>Virtual path of the created archive.</returns>
    string CreateArchive(string archivePath, IReadOnlyList<string> paths, Action<int, int, string>? progress,
                         bool enforcePermission);

    /// <summary>Extracts zip / 7z / rar / tar / tar.gz / tar.bz2 / gz / bz2 / xz archives.</summary>
    /// <param name="archivePath">Virtual path of the archive.</param>
    /// <param name="toFolder">Virtual destination folder.</param>
    /// <param name="overwrite">Replaces existing files.</param>
    void ExtractArchive(string archivePath, string toFolder, bool overwrite = false);

    /// <summary>Extracts an archive while reporting progress.</summary>
    /// <param name="archivePath">Virtual path of the archive.</param>
    /// <param name="toFolder">Virtual destination folder.</param>
    /// <param name="overwrite">Replaces existing files.</param>
    /// <param name="progress">Receives (done, total, currentEntry); total is -1
    /// when the format cannot be pre-scanned.</param>
    void ExtractArchive(string archivePath, string toFolder, bool overwrite, Action<int, int, string>? progress);

    /// <summary>Resolves a file for download, or <c>null</c> when it is a folder or missing.</summary>
    /// <param name="path">Virtual path of the file.</param>
    (string PhysicalPath, FileSystemEntryDto Entry)? ResolveForDownload(string path);

    /// <summary>Writes an uploaded stream to its target, checking permissions,
    /// file type and quota first.</summary>
    /// <param name="stream">Content to write.</param>
    /// <param name="targetPath">Virtual path of the target file.</param>
    /// <param name="overwrite">Replaces an existing file.</param>
    void SaveUploaded(Stream stream, string targetPath, bool overwrite);

    /// <summary>Converts a virtual path into a full physical path, rejecting traversal.</summary>
    /// <param name="virtualPath">Virtual path to resolve.</param>
    string Resolve(string virtualPath);

    /// <summary>Returns the effective permissions and type filters for a folder.</summary>
    /// <param name="virtualPath">Virtual folder path.</param>
    EffectiveAccess AccessAt(string? virtualPath);

    /// <summary>
    /// Checks permissions, file type and quota for a write of
    /// <paramref name="size"/> bytes to <paramref name="targetPath"/>.
    /// </summary>
    /// <param name="targetPath">Virtual path of the target file.</param>
    /// <param name="overwrite">True when an existing file is replaced.</param>
    /// <param name="size">Number of bytes that will be written.</param>
    void EnsureCanWrite(string targetPath, bool overwrite, long size);

    /// <summary>
    /// Drops the cached size of the quota scope after a write that did not go
    /// through <see cref="SaveUploaded"/> (chunked upload), so the next check
    /// re-measures instead of trusting a stale figure.
    /// </summary>
    /// <param name="virtualPath">Virtual path that was written to.</param>
    void InvalidateUsage(string virtualPath);
}
