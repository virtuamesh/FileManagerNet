namespace FileManagerNet.Services;

/// <summary>Metadata persisted next to a chunked upload's partial file.</summary>
internal sealed class UploadSessionMetadata
{
    /// <summary>Full physical path the assembled file will be moved to.</summary>
    public string Target { get; set; } = "";

    /// <summary>Expected total size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Original file name, used in prompts and error messages.</summary>
    public string Name { get; set; } = "";

    /// <summary>UTC time the session was created.</summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;
}
