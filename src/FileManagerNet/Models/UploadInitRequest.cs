namespace FileManagerNet.Models;

/// <summary>Request payload that starts a chunked upload session.</summary>
internal sealed class UploadInitRequest
{
    /// <summary>Destination folder (virtual path).</summary>
    public string Path { get; set; } = "/";

    /// <summary>File name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Path relative to <see cref="Path"/>; folder uploads keep their
    /// hierarchy here (for example "photos/2024/a.jpg").</summary>
    public string? RelativePath { get; set; }

    /// <summary>Total file size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Client-side last-modified timestamp in milliseconds.</summary>
    public long LastModified { get; set; }
}
