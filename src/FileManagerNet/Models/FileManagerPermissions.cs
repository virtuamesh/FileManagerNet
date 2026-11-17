namespace FileManagerNet.Models;

/// <summary>
/// Operations that can be granted or denied on a folder. Combine values with
/// bitwise or, for example <c>ListFiles | Download</c>.
/// <para>
/// The control checks these on the server for every operation, whatever the
/// client claims, and the UI only offers what the current folder allows.
/// </para>
/// </summary>
[Flags]
public enum FileManagerPermissions
{
    /// <summary>No permission at all.</summary>
    None = 0,

    /// <summary>Subfolders appear in the list and in the tree.</summary>
    ListSubfolders = 1 << 0,

    /// <summary>Files appear in the list.</summary>
    ListFiles = 1 << 1,

    /// <summary>Create folders and files.</summary>
    Create = 1 << 2,

    /// <summary>Delete files and folders.</summary>
    Delete = 1 << 3,

    /// <summary>Rename files and folders.</summary>
    Rename = 1 << 4,

    /// <summary>
    /// Replace the content of an existing file (upload over it, extract over
    /// it). Creating a new file needs <see cref="Create"/> or <see cref="Upload"/>.
    /// </summary>
    Edit = 1 << 5,

    /// <summary>Upload files.</summary>
    Upload = 1 << 6,

    /// <summary>Download files.</summary>
    Download = 1 << 7,

    /// <summary>Create archives from files and folders.</summary>
    Compress = 1 << 8,

    /// <summary>Extract archives into this folder.</summary>
    Extract = 1 << 9,

    /// <summary>Take items out of this folder (cut).</summary>
    Cut = 1 << 10,

    /// <summary>Copy items out of this folder.</summary>
    Copy = 1 << 11,

    /// <summary>Write copied or moved items into this folder.</summary>
    Paste = 1 << 12,

    /// <summary>Open the preview / viewer.</summary>
    Preview = 1 << 13,

    /// <summary>Browse, open and download without changing anything.</summary>
    ReadOnly = ListSubfolders | ListFiles | Download | Preview,

    /// <summary>Every supported operation.</summary>
    Full = ReadOnly | Create | Delete | Rename | Edit | Upload | Compress | Extract | Cut | Copy | Paste,
}
