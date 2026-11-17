namespace FileManagerNet.Services;

/// <summary>
/// Raised when an upload would overwrite an existing file and the caller has
/// not chosen overwrite or keep-both.
/// </summary>
public sealed class UploadConflictException : IOException
{
    /// <summary>Name of the file that already exists.</summary>
    public string Name { get; }

    /// <summary>Creates the exception for the conflicting file name.</summary>
    /// <param name="name">Name of the existing file.</param>
    public UploadConflictException(string name) : base($"'{name}' already exists.") => Name = name;
}
