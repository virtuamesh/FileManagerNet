using System.Text.Json.Serialization;

namespace FileManagerNet.Models;

/// <summary>
/// A file or folder entry returned by the list, tree and search APIs.
/// <see cref="Path"/> is always a virtual, root-relative path beginning with "/".
/// </summary>
public sealed class FileSystemEntryDto
{
    /// <summary>Display name of the entry, without any directory part.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Virtual, root-relative path of the entry (for example "/docs/a.txt").</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    /// <summary>True for folders, false for files.</summary>
    [JsonPropertyName("isDirectory")]
    public bool IsDirectory { get; set; }

    /// <summary>File size in bytes; 0 for folders.</summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Last write time as a Unix timestamp in milliseconds.</summary>
    [JsonPropertyName("lastModified")]
    public long LastModified { get; set; }

    /// <summary>Lower-case extension without the dot (for example "txt"); empty for folders.</summary>
    [JsonPropertyName("extension")]
    public string Extension { get; set; } = "";

    /// <summary>True when a folder contains at least one child (used by the tree pane).</summary>
    [JsonPropertyName("hasChildren")]
    public bool HasChildren { get; set; }

    /// <summary>True when the file is marked read-only on disk.</summary>
    [JsonPropertyName("readOnly")]
    public bool ReadOnly { get; set; }
}
