namespace FileManagerNet.Services;

/// <summary>
/// Raised when a copy or move would overwrite existing items and the caller
/// has not yet decided whether to overwrite or keep both.
/// </summary>
public sealed class FileConflictException : IOException
{
    /// <summary>Names of the items that already exist in the target folder.</summary>
    public IReadOnlyList<string> Conflicts { get; }

    /// <summary>Creates the exception for a set of conflicting names.</summary>
    /// <param name="conflicts">Names that already exist in the target folder.</param>
    public FileConflictException(IReadOnlyList<string> conflicts)
        : base(conflicts.Count == 1
            ? $"'{conflicts[0]}' already exists in the target folder."
            : $"{conflicts.Count} items already exist in the target folder.")
        => Conflicts = conflicts;
}
