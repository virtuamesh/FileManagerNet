namespace FileManagerNet.Services;

/// <summary>
/// Raised when a chunk does not start at the current end of the partial file;
/// carries how many bytes the server already has so the client can resume.
/// </summary>
public sealed class UploadOffsetMismatchException : IOException
{
    /// <summary>Number of bytes the server already received.</summary>
    public long Received { get; }

    /// <summary>Creates the exception with the server's current length.</summary>
    /// <param name="received">Bytes already stored for the session.</param>
    public UploadOffsetMismatchException(long received)
        : base("Chunk offset does not match the received length.")
        => Received = received;
}
