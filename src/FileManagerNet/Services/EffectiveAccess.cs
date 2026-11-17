using FileManagerNet.Models;

namespace FileManagerNet.Services;

/// <summary>
/// Effective access of one folder: the permissions, file-type filters and
/// quota that resulted from resolving its access-control entries.
/// </summary>
public sealed class EffectiveAccess
{
    /// <summary>Permissions granted on the folder.</summary>
    public FileManagerPermissions Permissions { get; init; } = FileManagerPermissions.Full;

    /// <summary>Allowed file types; empty when every type is allowed.</summary>
    public FileTypeFilter AllowedTypes { get; init; } = FileTypeFilter.Empty;

    /// <summary>Denied file types; empty when none are denied.</summary>
    public FileTypeFilter DeniedTypes { get; init; } = FileTypeFilter.Empty;

    /// <summary>Byte limit for the content below the folder; 0 = unlimited.</summary>
    public long QuotaBytes { get; init; }

    /// <summary>Folder the quota is measured on — the entry that defined it,
    /// which may be an ancestor of the folder being written to.</summary>
    public string QuotaPath { get; init; } = "/";

    /// <summary>True when all bits of <paramref name="permission"/> are granted.</summary>
    /// <param name="permission">Permission or combination to test.</param>
    public bool Has(FileManagerPermissions permission) => (Permissions & permission) == permission;

    /// <summary>True when a file may be listed, downloaded or written. Folders
    /// are always visible (their own entry decides what is inside them).</summary>
    /// <param name="fileName">File name to test, without any directory part.</param>
    public bool IsFileAllowed(string fileName)
    {
        if (DeniedTypes.Matches(fileName)) return false;
        if (!AllowedTypes.IsEmpty && !AllowedTypes.Matches(fileName)) return false;
        return true;
    }

    /// <summary>
    /// Converts the permissions into the flag names the browser consumes. The
    /// names mirror <see cref="FileManagerConfig"/> so the existing UI checks
    /// keep working unchanged.
    /// </summary>
    public Dictionary<string, bool> ToClientFlags()
    {
        var p = Permissions;
        bool Has(FileManagerPermissions f) => (p & f) == f;
        return new Dictionary<string, bool>
        {
            ["allowDownload"] = Has(FileManagerPermissions.Download),
            ["allowUpload"] = Has(FileManagerPermissions.Upload),
            ["allowDelete"] = Has(FileManagerPermissions.Delete),
            ["allowRename"] = Has(FileManagerPermissions.Rename),
            ["allowCreateFolder"] = Has(FileManagerPermissions.Create),
            ["allowCreateFile"] = Has(FileManagerPermissions.Create),
            ["allowCopyMove"] = Has(FileManagerPermissions.Copy) || Has(FileManagerPermissions.Cut),
            ["allowArchive"] = Has(FileManagerPermissions.Compress) || Has(FileManagerPermissions.Extract),
            ["allowPreview"] = Has(FileManagerPermissions.Preview),
            ["listFiles"] = Has(FileManagerPermissions.ListFiles),
            ["listSubfolders"] = Has(FileManagerPermissions.ListSubfolders),
        };
    }
}
