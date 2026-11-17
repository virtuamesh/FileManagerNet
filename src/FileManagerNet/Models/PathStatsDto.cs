using System.Text.Json.Serialization;

namespace FileManagerNet.Models;

/// <summary>
/// Item counts and volume space for a path; used by the status bar.
/// </summary>
public sealed class PathStatsDto
{
    /// <summary>Virtual path the statistics were measured for.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    /// <summary>Number of files in scope.</summary>
    [JsonPropertyName("fileCount")]
    public long FileCount { get; set; }

    /// <summary>Number of folders in scope.</summary>
    [JsonPropertyName("folderCount")]
    public long FolderCount { get; set; }

    /// <summary>Total size of the files in scope, in bytes.</summary>
    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    /// <summary>Free space of the volume, in bytes; <c>null</c> when unknown.</summary>
    [JsonPropertyName("driveFreeBytes")]
    public long? DriveFreeBytes { get; set; }

    /// <summary>Total size of the volume, in bytes; <c>null</c> when unknown.</summary>
    [JsonPropertyName("driveTotalBytes")]
    public long? DriveTotalBytes { get; set; }
}
